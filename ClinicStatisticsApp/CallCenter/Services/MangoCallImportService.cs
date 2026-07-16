using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.CallCenter.Services;

/// <summary>Imports calls idempotently: the MANGO entry id is the unique key.</summary>
public sealed class MangoCallImportService(AppDbContext db, IMangoApiClient api)
{
    public async Task EnsurePeriodImportedAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            var dayEnd = day.AddDays(1).AddSeconds(-1);
            var done = day != DateTime.Today && await db.CallCenterSyncLogs.AsNoTracking().AnyAsync(x =>
                x.SyncType == "Calls" && x.IsSuccess && x.PeriodFrom <= day && x.PeriodTo >= dayEnd, cancellationToken);
            if (!done) await ImportCallsAsync(day, dayEnd, cancellationToken);
        }
    }

    public async Task ImportCallsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var log = new CallCenterSyncLog { SyncType = "Calls", StartedAt = DateTime.Now, PeriodFrom = from, PeriodTo = to };
        db.CallCenterSyncLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var calls = await api.GetCallsAsync(from, to, cancellationToken);
            var employees = await db.CallCenterEmployees.ToListAsync(cancellationToken);
            var groups = await db.CallCenterGroups.ToListAsync(cancellationToken);
            var topics = await db.CallCenterTopics.ToListAsync(cancellationToken);
            var known = await db.CallCenterCallRecords.Where(x => x.CallDateTime >= from.AddDays(-1) && x.CallDateTime <= to.AddDays(1))
                .ToDictionaryAsync(x => x.MangoCallId, StringComparer.OrdinalIgnoreCase, cancellationToken);

            foreach (var dto in calls.Where(x => !string.IsNullOrWhiteSpace(x.CallId)))
            {
                if (string.IsNullOrWhiteSpace(dto.TopicMangoId) && IsAnswered(dto))
                {
                    try { dto.TopicMangoId = await api.GetCallTopicIdAsync(dto.CallId!, cancellationToken); }
                    catch when (!cancellationToken.IsCancellationRequested) { }
                }
                var employee = await FindEmployeeAsync(employees, dto, cancellationToken);
                var group = await FindGroupAsync(groups, dto, cancellationToken);
                var topic = topics.FirstOrDefault(x => x.MangoTopicId == dto.TopicMangoId);
                if (!known.TryGetValue(dto.CallId!, out var entity))
                {
                    entity = new CallCenterCallRecord { MangoCallId = dto.CallId! };
                    db.CallCenterCallRecords.Add(entity);
                    known.Add(dto.CallId!, entity);
                    log.ImportedCount++;
                }
                else log.UpdatedCount++;
                Apply(entity, dto, employee, group, topic);
            }
            log.IsSuccess = true;
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

    private static bool IsAnswered(MangoCallDto dto) => dto.StatusCode == "1" || string.Equals(dto.StatusText, "successful", StringComparison.OrdinalIgnoreCase);
}
