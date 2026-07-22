using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClinicStatisticsApp.CallCenter.Services;

/// <summary>Client for MANGO REST API. Credentials are supplied at runtime and never persisted by this class.</summary>
public sealed class MangoApiClient(HttpClient httpClient, MangoApiOptions options) : IMangoApiClient
{
    private static readonly MangoRequestPacer RequestPacer = new();
    public async Task<List<MangoUserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(await SendAsync("/vpbx/config/users/request", new { }, cancellationToken));
        if (!document.RootElement.TryGetProperty("users", out var users) || users.ValueKind != JsonValueKind.Array) return [];
        return users.EnumerateArray().Select(x => new MangoUserDto
        {
            Id = StringAt(x, "general", "user_id") ?? StringAt(x, "telephony", "extension"),
            Name = StringAt(x, "general", "name"), Extension = StringAt(x, "telephony", "extension")
        }).Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList();
    }

    public async Task<List<MangoGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(await SendAsync("/vpbx/config/groups/request", new { }, cancellationToken));
        if (!document.RootElement.TryGetProperty("groups", out var groups) || groups.ValueKind != JsonValueKind.Array) return [];
        return groups.EnumerateArray().Select(x => new MangoGroupDto { Id = StringAt(x, "id"), Name = StringAt(x, "name") }).Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList();
    }

    public async Task<List<MangoTopicDto>> GetTopicsAsync(CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(await SendAsync("/vpbx/cc/tags/", new { }, cancellationToken));
        if (!document.RootElement.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array) return [];
        return tags.EnumerateArray().Select(x => new MangoTopicDto { Id = StringAt(x, "id"), Name = StringAt(x, "name") }).Where(x => !string.IsNullOrWhiteSpace(x.Id)).ToList();
    }

    public async Task<List<MangoCallDto>> GetCallsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var calls = new List<MangoCallDto>();
        const int pageSize = 1000;
        for (var offset = 0; offset < 50_000; offset += pageSize)
        {
            var keyResponse = await SendAsync("/vpbx/stats/calls/request", new { start_date = from.ToString("dd.MM.yyyy HH:mm:ss"), end_date = to.ToString("dd.MM.yyyy HH:mm:ss"), limit = pageSize.ToString(), offset = offset.ToString() }, cancellationToken);
            using var keyDocument = JsonDocument.Parse(keyResponse);
            if (!keyDocument.RootElement.TryGetProperty("key", out var keyElement) || string.IsNullOrWhiteSpace(keyElement.GetString())) throw new InvalidOperationException("MANGO не вернул ключ запроса статистики звонков.");
            var data = await PollAsync(keyElement.GetString()!, cancellationToken);
            var page = ParseCalls(data);
            calls.AddRange(page);
            if (page.Count < pageSize) break;
            // Следующая страница проходит через общий ограничитель запросов.
        }
        return calls.GroupBy(x => x.CallId, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
    }

    public async Task<string?> GetCallTopicIdAsync(string entryId, CancellationToken cancellationToken = default)
    {
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var document = JsonDocument.Parse(await SendAsync("/vpbx/cc/call/", new { entry_id = entryId }, requestTimeout.Token));
        if (!document.RootElement.TryGetProperty("call", out var call)) return null;
        return FindTagId(call);
    }

    public async Task<MangoTopicDto?> GetCallTopicAsync(string entryId, CancellationToken cancellationToken = default)
    {
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var document = JsonDocument.Parse(await SendAsync("/vpbx/cc/call/", new { entry_id = entryId }, requestTimeout.Token));
        if (!document.RootElement.TryGetProperty("call", out var call)) return null;
        var tag = FindTag(call);
        return tag.Id is null && tag.Name is null ? null : new MangoTopicDto { Id = tag.Id, Name = tag.Name };
    }

    public async Task<MangoRecordingFile> GetRecordingAsync(string recordingId, bool forDownload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSalt))
            throw new InvalidOperationException("Не заданы ключи MANGO API в appsettings.Local.json.");

        const string endpoint = "/vpbx/queries/recording/post/";
        var json = JsonSerializer.Serialize(new { recording_id = recordingId, action = forDownload ? "download" : "play" });
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["vpbx_api_key"] = options.ApiKey,
            ["sign"] = Sign(json),
            ["json"] = json
        });
        using var response = await httpClient.PostAsync($"{GetApiRoot()}{endpoint}", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new MangoRecordingFile(await response.Content.ReadAsByteArrayAsync(cancellationToken), response.Content.Headers.ContentType?.MediaType ?? "audio/mpeg");
    }

    private async Task<string> PollAsync(string key, CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(750), token);
        for (var i = 0; i < 30; i++)
        {
            if (i > 0) await Task.Delay(TimeSpan.FromSeconds(1), token);
            var json = await PostAsync("/vpbx/stats/calls/result/", new { key }, token, checkResult: false);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("status", out var status)) return json;
            if (string.Equals(status.GetString(), "complete", StringComparison.OrdinalIgnoreCase)) return json;
            if (string.Equals(status.GetString(), "error", StringComparison.OrdinalIgnoreCase) || string.Equals(status.GetString(), "not-found", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("MANGO не смог подготовить статистику звонков.");
        }
        throw new TimeoutException("Превышено время ожидания статистики звонков MANGO.");
    }

    private Task<string> SendAsync(string endpoint, object payload, CancellationToken token) => PostAsync(endpoint, payload, token, checkResult: true);

    private async Task<string> PostAsync(string endpoint, object payload, CancellationToken token, bool checkResult)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.ApiSalt)) throw new InvalidOperationException("Не заданы ключи MANGO API в appsettings.Local.json.");
        var json = JsonSerializer.Serialize(payload);
        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        requestTimeout.CancelAfter(TimeSpan.FromMinutes(2));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await RequestPacer.WaitAsync(requestTimeout.Token);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["vpbx_api_key"] = options.ApiKey, ["sign"] = Sign(json), ["json"] = json });
            using var response = await httpClient.PostAsync($"{GetApiRoot()}{endpoint}", content, requestTimeout.Token);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (attempt == 4)
                    throw new InvalidOperationException("MANGO временно ограничил количество запросов. Повторите обновление через несколько минут.");

                var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * (attempt + 1));
                await Task.Delay(delay, requestTimeout.Token);
                continue;
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(requestTimeout.Token);
            if (checkResult)
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("result", out var result) && result.TryGetInt32(out var code) && code != 1000) throw new InvalidOperationException($"MANGO вернул ошибку {code} для {endpoint}.");
            }
            return body;
        }

        throw new InvalidOperationException("Не удалось получить ответ MANGO после повторных попыток.");
    }

    private string Sign(string json)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(options.ApiKey + json + options.ApiSalt))).ToLowerInvariant();
    }

    private string GetApiRoot()
    {
        var root = options.BaseUrl.TrimEnd('/');
        return root.EndsWith("/vpbx", StringComparison.OrdinalIgnoreCase)
            ? root[..^5]
            : root;
    }

    private sealed class MangoRequestPacer
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private DateTime _nextRequestAtUtc = DateTime.MinValue;

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken);
            try
            {
                var delay = _nextRequestAtUtc - DateTime.UtcNow;
                if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
                _nextRequestAtUtc = DateTime.UtcNow.AddMilliseconds(1_000);
            }
            finally { _gate.Release(); }
        }
    }

    private static List<MangoCallDto> ParseCalls(string json)
    {
        using var document = JsonDocument.Parse(json); var result = new List<MangoCallDto>();
        if (!document.RootElement.TryGetProperty("data", out var blocks) || blocks.ValueKind != JsonValueKind.Array) return result;
        foreach (var block in blocks.EnumerateArray()) if (block.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var item in list.EnumerateArray()) { var dto = ParseCall(item); if (dto != null) result.Add(dto); }
        return result;
    }

    private static MangoCallDto? ParseCall(JsonElement item)
    {
        var id = StringAt(item, "entry_id"); if (string.IsNullOrWhiteSpace(id)) return null;
        var type = IntAt(item, "context_type"); var direction = type == 1 ? "incoming" : type == 2 ? "outgoing" : type == 3 ? "internal" : "unknown";
        var employeeId = default(string); var employeeName = default(string); var extension = default(string); var groupId = default(string); var groupName = default(string); var recording = default(string); int? endReason = null;
        if (item.TryGetProperty("context_calls", out var calls) && calls.ValueKind == JsonValueKind.Array) foreach (var call in calls.EnumerateArray())
        {
            var callType = StringAt(call, "call_type");
            recording ??= FirstValue(call, "recording_id"); endReason ??= IntAt(call, "call_end_reason");
            if (callType == "group") { groupId ??= StringAt(call, "call_abonent_id"); groupName ??= StringAt(call, "call_abonent_info"); if (call.TryGetProperty("members", out var members) && members.ValueKind == JsonValueKind.Array) foreach (var member in members.EnumerateArray()) if (StringAt(member, "call_type") == "user" && (employeeId is null || (LongAt(member, "call_answer_time") ?? 0) > 0)) { employeeId = StringAt(member, "call_abonent_id"); employeeName = StringAt(member, "call_abonent_info"); extension = StringAt(member, "call_abonent_extension"); recording ??= FirstValue(member, "recording_id"); if ((LongAt(member, "call_answer_time") ?? 0) > 0) break; } }
            else if (callType == "user" && employeeId is null) { employeeId = StringAt(call, "call_abonent_id"); employeeName = StringAt(call, "call_abonent_info"); extension = StringAt(call, "call_abonent_extension"); }
            else if (direction == "outgoing" && callType == "number") extension ??= StringAt(call, "call_abonent_extension");
        }
        if (direction is "outgoing" or "internal") { employeeId = StringAt(item, "caller_id") ?? employeeId; employeeName = StringAt(item, "caller_name") ?? employeeName; }
        var timestamp = LongAt(item, "context_start_time");
        return new MangoCallDto { CallId = id, CallDateTime = timestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(timestamp.Value).LocalDateTime : DateTime.MinValue, Direction = direction, EmployeeMangoId = employeeId, EmployeeName = employeeName, EmployeeExtension = extension, GroupMangoId = groupId, GroupName = groupName, TopicMangoId = FirstValue(item, "tag_id"), RecordingId = recording, PhoneNumber = direction == "incoming" ? StringAt(item, "caller_number") : StringAt(item, "called_number"), StatusCode = IntAt(item, "context_status")?.ToString(), StatusText = IntAt(item, "context_status") == 1 ? "successful" : IntAt(item, "context_status") == 0 ? "unsuccessful" : null, DurationSeconds = IntAt(item, "duration"), TalkDurationSeconds = IntAt(item, "talk_duration"), CallEndReason = endReason, RawJson = item.GetRawText() };
    }

    private static string? StringAt(JsonElement item, string name) => item.TryGetProperty(name, out var value) ? value.ValueKind switch { JsonValueKind.String => value.GetString(), JsonValueKind.Number => value.ToString(), _ => null } : null;
    private static string? StringAt(JsonElement item, string parent, string name) => item.TryGetProperty(parent, out var node) ? StringAt(node, name) : null;
    private static int? IntAt(JsonElement item, string name) => int.TryParse(StringAt(item, name), out var value) ? value : null;
    private static long? LongAt(JsonElement item, string name) => long.TryParse(StringAt(item, name), out var value) ? value : null;
    private static string? FirstValue(JsonElement item, string name) => item.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array ? array.EnumerateArray().Select(x => x.ValueKind is JsonValueKind.String or JsonValueKind.Number ? x.ToString() : null).FirstOrDefault(x => x != null) : StringAt(item, name);
    private static string? FindTagId(JsonElement node) => FindTag(node).Id;

    private static (string? Id, string? Name) FindTag(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array)
            return node.EnumerateArray().Select(FindTag).FirstOrDefault(x => x.Id is not null || x.Name is not null);
        if (node.ValueKind != JsonValueKind.Object)
            return node.ValueKind == JsonValueKind.String ? (null, node.GetString()?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()) : (null, null);
        string? id = null; string? name = null;
        foreach (var property in node.EnumerateObject())
        {
            if (property.Name is "tag_id" or "id")
            {
                var tag = ReadTagValue(property.Value, stringValuesAreNames: false);
                id ??= tag.Id;
                name ??= tag.Name;
            }
            if (property.Name is "name" or "title" or "tag_name" or "topic_name" or "theme_name")
            {
                var tag = ReadTagValue(property.Value, stringValuesAreNames: true);
                id ??= tag.Id;
                name ??= tag.Name;
            }
        }
        if (id is not null || name is not null) return (id, name);
        foreach (var property in node.EnumerateObject())
        {
            var nested = FindTag(property.Value);
            if (nested.Id is not null || nested.Name is not null) return nested;
        }
        return (null, null);
    }

    private static (string? Id, string? Name) ReadTagValue(JsonElement value, bool stringValuesAreNames)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                var result = ReadTagValue(item, stringValuesAreNames);
                if (result.Id is not null || result.Name is not null) return result;
            }
            return (null, null);
        }

        if (value.ValueKind == JsonValueKind.Number) return (value.ToString(), null);
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return stringValuesAreNames
                ? (null, text?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
                : (text, null);
        }

        if (value.ValueKind != JsonValueKind.Object) return (null, null);
        foreach (var property in value.EnumerateObject())
        {
            if (property.Name is "id" or "tag_id")
            {
                var result = ReadTagValue(property.Value, false);
                if (result.Id is not null) return result;
            }
        }
        return (null, null);
    }
}
