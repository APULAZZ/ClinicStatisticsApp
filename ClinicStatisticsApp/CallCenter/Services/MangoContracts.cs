namespace ClinicStatisticsApp.CallCenter.Services;

public class MangoApiOptions
{
    public string BaseUrl { get; set; } = "https://app.mango-office.ru";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSalt { get; set; } = string.Empty;
}

public class MangoCallDto
{
    public string? CallId { get; set; }
    public DateTime CallDateTime { get; set; }
    public string? Direction { get; set; }
    public string? EmployeeMangoId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeExtension { get; set; }
    public string? GroupMangoId { get; set; }
    public string? GroupName { get; set; }
    public string? TopicMangoId { get; set; }
    public string? TopicName { get; set; }
    public string? RecordingId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? StatusCode { get; set; }
    public string? StatusText { get; set; }
    public int? DurationSeconds { get; set; }
    public int? TalkDurationSeconds { get; set; }
    public int? WaitDurationSeconds { get; set; }
    public int? CallEndReason { get; set; }
    public string RawJson { get; set; } = string.Empty;
}

public sealed class MangoUserDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Extension { get; set; }
}

public sealed class MangoGroupDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public sealed class MangoTopicDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int? Category { get; set; }
    public bool IsFolder { get; set; }
}
public sealed record MangoRecordingCategoryDto(string? RecordingId, int? CategoryId, string? CategoryName);
public sealed record MangoRecordingFile(byte[] Content, string ContentType);
public sealed record MangoCallSearchProgress(string Message, int FoundCount, DateTime From, DateTime To);

public interface IMangoApiClient
{
    Task<List<MangoUserDto>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<List<MangoGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<List<MangoTopicDto>> GetTopicsAsync(CancellationToken cancellationToken = default);
    Task<List<MangoCallDto>> GetCallsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<List<MangoCallDto>> GetRecentCallsByPhonesAsync(IEnumerable<string?> phoneNumbers, int maxResults = 5, int lookbackDays = 30, IProgress<MangoCallSearchProgress>? progress = null, CancellationToken cancellationToken = default);
    Task<string?> GetCallTopicIdAsync(string entryId, CancellationToken cancellationToken = default);
    Task<MangoTopicDto?> GetCallTopicAsync(string entryId, CancellationToken cancellationToken = default);
    Task<MangoRecordingFile> GetRecordingAsync(string recordingId, bool forDownload, CancellationToken cancellationToken = default);
}
