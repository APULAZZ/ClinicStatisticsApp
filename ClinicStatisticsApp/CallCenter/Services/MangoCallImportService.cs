using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace ClinicStatisticsApp.CallCenter.Services;

/// <summary>Imports calls idempotently: the MANGO entry id is the unique key.</summary>
public sealed class MangoCallImportService(AppDbContext db, IMangoApiClient api)
{
    public Task EnsurePeriodImportedAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        => EnsurePeriodImportedAsync(from, to, progress: null, cancellationToken);

    public async Task EnsurePeriodImportedAsync(DateTime from, DateTime to, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var dayEnd = day.AddDays(1).AddSeconds(-1);
            var done = day != DateTime.Today && await db.CallCenterSyncLogs.AsNoTracking().AnyAsync(x =>
                x.SyncType == "Calls" && x.IsSuccess && x.PeriodFrom <= day && x.PeriodTo >= dayEnd, cancellationToken);
            if (done)
            {
                progress?.Report($"{day:dd.MM.yyyy}: используем локально сохранённые данные.");
                continue;
            }

            progress?.Report($"{day:dd.MM.yyyy}: обновляем данные из MANGO…");
            await ImportCallsAsync(day, dayEnd, progress, cancellationToken);
        }
    }

    public Task ImportCallsAsync(DateTime from, DateTime to, CancellationToken cancellationToken)
        => ImportCallsAsync(from, to, progress: null, cancellationToken);

    /// <param name="includeCall">Optional local filter. MANGO still returns the day's list,
    /// but only matching calls are stored and have their tag details requested.</param>
    public async Task ImportCallsAsync(DateTime from, DateTime to, IProgress<string>? progress = null, CancellationToken cancellationToken = default, Func<MangoCallDto, bool>? includeCall = null)
    {
        var log = new CallCenterSyncLog { SyncType = "Calls", StartedAt = DateTime.Now, PeriodFrom = from, PeriodTo = to };
        db.CallCenterSyncLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            progress?.Report($"Получаем звонки за {from:dd.MM.yyyy}…");
            var calls = await api.GetCallsAsync(from, to, cancellationToken);
            var selectedCalls = includeCall is null ? calls : calls.Where(includeCall).ToList();
            progress?.Report(includeCall is null
                ? $"Получено звонков: {calls.Count:N0}. Сохраняем данные…"
                : $"Получено звонков MANGO: {calls.Count:N0}; соответствует файлу №1: {selectedCalls.Count:N0}. Сохраняем только их…");
            var employees = await db.CallCenterEmployees.ToListAsync(cancellationToken);
            var groups = await db.CallCenterGroups.ToListAsync(cancellationToken);
            var topics = await db.CallCenterTopics.ToListAsync(cancellationToken);
            var linkedTopicsCount = 0;
            var topicLookupRequests = 0;
            var mangoTopicsReceived = 0;
            var known = await db.CallCenterCallRecords.Where(x => x.CallDateTime >= from.AddDays(-1) && x.CallDateTime <= to.AddDays(1))
                .ToDictionaryAsync(x => x.MangoCallId, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var topicLookupBudget = Stopwatch.StartNew();

            foreach (var dto in selectedCalls.Where(x => !string.IsNullOrWhiteSpace(x.CallId)))
            {
                known.TryGetValue(dto.CallId!, out var existingEntity);
                // The daily call list already contains tag_id.  Once the tag directory is
                // synchronized, a separate request for every call is unnecessary and makes
                // a monthly import much slower.
                if (string.IsNullOrWhiteSpace(dto.TopicMangoId) &&
                    (existingEntity == null || existingEntity.TopicId == null) &&
                    IsCallCenterCandidateSafe(dto) &&
                    topicLookupBudget.Elapsed < TimeSpan.FromSeconds(90) &&
                    IsAnswered(dto))
                {
                    try
                    {
                        topicLookupRequests++;
                        var callTopic = await api.GetCallTopicAsync(dto.CallId!, cancellationToken);
                        dto.TopicMangoId = callTopic?.Id;
                        dto.TopicName = callTopic?.Name;
                        if (!string.IsNullOrWhiteSpace(dto.TopicMangoId) || !string.IsNullOrWhiteSpace(dto.TopicName))
                            mangoTopicsReceived++;
                        if (topicLookupRequests % 25 == 0)
                            progress?.Report($"Уточняем тематики в MANGO: {topicLookupRequests:N0} запросов…");
                    }
                    catch when (!cancellationToken.IsCancellationRequested) { }
                }
                var employee = await FindEmployeeAsync(employees, dto, cancellationToken);
                var group = await FindGroupAsync(groups, dto, cancellationToken);
                var topic = FindTopic(topics, dto);
                if (topic is not null) linkedTopicsCount++;
                if (existingEntity == null)
                {
                    existingEntity = new CallCenterCallRecord { MangoCallId = dto.CallId! };
                    db.CallCenterCallRecords.Add(existingEntity);
                    known.Add(dto.CallId!, existingEntity);
                    log.ImportedCount++;
                }
                else log.UpdatedCount++;
                Apply(existingEntity, dto, employee, group, topic);
            }
            log.IsSuccess = true;
            progress?.Report($"Сохранено: новых {log.ImportedCount:N0}, обновлено {log.UpdatedCount:N0}. Тематики: запросов {topicLookupRequests:N0}, получено от MANGO {mangoTopicsReceived:N0}, сопоставлено {linkedTopicsCount:N0}.");
        }
        catch (Exception ex) { log.ErrorText = ex.ToString(); throw; }
        finally { log.FinishedAt = DateTime.Now; await db.SaveChangesAsync(CancellationToken.None); }
    }

    private async Task<CallCenterEmployee?> FindEmployeeAsync(List<CallCenterEmployee> list, MangoCallDto dto, CancellationToken token)
    {
        var entity = list.FirstOrDefault(x => x.MangoUserId == dto.EmployeeMangoId) ?? list.FirstOrDefault(x => x.Extension == dto.EmployeeExtension);
        if (entity != null) return entity;
        if (string.IsNullOrWhiteSpace(dto.EmployeeMangoId) && string.IsNullOrWhiteSpace(dto.EmployeeExtension) && string.IsNullOrWhiteSpace(dto.EmployeeName)) return null;
        entity = new CallCenterEmployee { MangoUserId = dto.EmployeeMangoId, Extension = dto.EmployeeExtension, FullName = dto.EmployeeName ?? dto.EmployeeExtension ?? dto.EmployeeMangoId ?? "Неизвестный сотрудник", IsActive = true };
        db.CallCenterEmployees.Add(entity); list.Add(entity); await db.SaveChangesAsync(token); return entity;
    }

    private async Task<CallCenterGroup?> FindGroupAsync(List<CallCenterGroup> list, MangoCallDto dto, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(dto.GroupMangoId)) return null;
        var entity = list.FirstOrDefault(x => x.MangoGroupId == dto.GroupMangoId);
        if (entity != null) return entity;
        entity = new CallCenterGroup { MangoGroupId = dto.GroupMangoId, Name = dto.GroupName ?? $"Группа {dto.GroupMangoId}", IsActive = true };
        db.CallCenterGroups.Add(entity); list.Add(entity); await db.SaveChangesAsync(token); return entity;
    }

    private static void Apply(CallCenterCallRecord target, MangoCallDto dto, CallCenterEmployee? employee, CallCenterGroup? group, CallCenterTopic? topic)
    {
        if (dto.CallDateTime != DateTime.MinValue) target.CallDateTime = dto.CallDateTime;
        target.EmployeeId = employee?.Id ?? target.EmployeeId; target.GroupId = group?.Id ?? target.GroupId; target.TopicId = topic?.Id ?? target.TopicId;
        target.ExternalPhoneNumber = dto.PhoneNumber ?? target.ExternalPhoneNumber; target.Direction = dto.Direction ?? target.Direction; target.StatusCode = dto.StatusCode ?? target.StatusCode; target.StatusText = dto.StatusText ?? target.StatusText;
        target.RecordingId = dto.RecordingId ?? target.RecordingId; target.DurationSeconds = dto.DurationSeconds ?? target.DurationSeconds; target.TalkDurationSeconds = dto.TalkDurationSeconds ?? target.TalkDurationSeconds;
        target.WaitDurationSeconds = dto.DurationSeconds.HasValue && dto.TalkDurationSeconds.HasValue ? Math.Max(0, dto.DurationSeconds.Value - dto.TalkDurationSeconds.Value) : target.WaitDurationSeconds;
        target.IsIncoming = string.Equals(dto.Direction, "incoming", StringComparison.OrdinalIgnoreCase); target.IsOutgoing = string.Equals(dto.Direction, "outgoing", StringComparison.OrdinalIgnoreCase);
        target.IsAnswered = IsAnswered(dto); target.IsMissedIncoming = target.IsIncoming && !target.IsAnswered; target.IsOutgoingNoAnswer = target.IsOutgoing && !target.IsAnswered; target.RawJson = dto.RawJson; target.ImportedAt = DateTime.Now;
    }

    private static CallCenterTopic? FindTopic(List<CallCenterTopic> topics, MangoCallDto dto)
    {
        var byId = topics.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.MangoTopicId) && string.Equals(x.MangoTopicId, dto.TopicMangoId, StringComparison.OrdinalIgnoreCase));
        if (byId is not null) return byId;
        var byName = !string.IsNullOrWhiteSpace(dto.TopicName) ? topics.FirstOrDefault(x => string.Equals(x.Name, dto.TopicName, StringComparison.OrdinalIgnoreCase)) : null;
        if (byName is not null) return byName;
        if (string.IsNullOrWhiteSpace(dto.RawJson)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(dto.RawJson);
            var name = FindTopicName(document.RootElement);
            return string.IsNullOrWhiteSpace(name) ? null : topics.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        catch (System.Text.Json.JsonException) { return null; }
    }

    private static string? FindTopicName(System.Text.Json.JsonElement node)
    {
        if (node.ValueKind == System.Text.Json.JsonValueKind.Array)
            return node.EnumerateArray().Select(FindTopicName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        if (node.ValueKind != System.Text.Json.JsonValueKind.Object) return node.ValueKind == System.Text.Json.JsonValueKind.String ? node.GetString()?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() : null;
        foreach (var property in node.EnumerateObject())
        {
            var key = property.Name.ToLowerInvariant();
            if (key is "name" or "title" or "tag_name" or "topic_name" or "theme_name")
                return property.Value.ValueKind == System.Text.Json.JsonValueKind.String ? property.Value.GetString() : property.Value.ToString();
            if (key.Contains("tag") || key.Contains("topic") || key.Contains("theme"))
            {
                var found = FindTopicName(property.Value);
                if (!string.IsNullOrWhiteSpace(found)) return found;
            }
        }
        return null;
    }

    private static bool IsAnswered(MangoCallDto dto) => dto.StatusCode == "1" || string.Equals(dto.StatusText, "successful", StringComparison.OrdinalIgnoreCase);

    private static bool IsCallCenterCandidate(MangoCallDto dto)
    {
        if (string.Equals(dto.GroupName, "Коллцентр", StringComparison.OrdinalIgnoreCase))
            return true;

        return dto.EmployeeName?.StartsWith("КЦ ", StringComparison.OrdinalIgnoreCase) == true ||
               string.Equals(dto.EmployeeName, "Зоя Ершова", StringComparison.OrdinalIgnoreCase);
    }

    // Unicode escape sequences deliberately protect this business rule from
    // accidental source-file encoding conversion during project transfers.
    private static bool IsCallCenterCandidateSafe(MangoCallDto dto)
    {
        const string callCenterGroup = "\u041a\u043e\u043b\u043b\u0446\u0435\u043d\u0442\u0440";
        const string callCenterPrefix = "\u041a\u0426 ";
        const string zoiaErshova = "\u0417\u043e\u044f \u0415\u0440\u0448\u043e\u0432\u0430";

        return string.Equals(dto.GroupName, callCenterGroup, StringComparison.OrdinalIgnoreCase)
               || dto.EmployeeName?.StartsWith(callCenterPrefix, StringComparison.OrdinalIgnoreCase) == true
               || string.Equals(dto.EmployeeName, zoiaErshova, StringComparison.OrdinalIgnoreCase);
    }
}
