namespace ClinicStatisticsApp.CallCenter.Models;

public class CallCenterEmployee
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public string? MangoUserId { get; set; }
    public string? MangoUserKey { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CallCenterEmployeeGroup> EmployeeGroups { get; set; } = new List<CallCenterEmployeeGroup>();
    public ICollection<CallCenterCallRecord> CallRecords { get; set; } = new List<CallCenterCallRecord>();
}

public class CallCenterGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MangoGroupId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CallCenterEmployeeGroup> EmployeeGroups { get; set; } = new List<CallCenterEmployeeGroup>();
    public ICollection<CallCenterCallRecord> CallRecords { get; set; } = new List<CallCenterCallRecord>();
}

public class CallCenterTopic
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? MangoTopicId { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<CallCenterCallRecord> CallRecords { get; set; } = new List<CallCenterCallRecord>();
}

public class CallCenterEmployeeGroup
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public CallCenterEmployee? Employee { get; set; }
    public int GroupId { get; set; }
    public CallCenterGroup? Group { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}

public class CallCenterCallRecord
{
    public int Id { get; set; }
    public string MangoCallId { get; set; } = string.Empty;
    public DateTime CallDateTime { get; set; }
    public int? EmployeeId { get; set; }
    public CallCenterEmployee? Employee { get; set; }
    public int? GroupId { get; set; }
    public CallCenterGroup? Group { get; set; }
    public int? TopicId { get; set; }
    public CallCenterTopic? Topic { get; set; }
    public string? RecordingId { get; set; }
    public string? ExternalPhoneNumber { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string? StatusCode { get; set; }
    public string? StatusText { get; set; }
    public int? DurationSeconds { get; set; }
    public int? TalkDurationSeconds { get; set; }
    public int? WaitDurationSeconds { get; set; }
    public bool IsIncoming { get; set; }
    public bool IsOutgoing { get; set; }
    public bool IsAnswered { get; set; }
    public bool IsMissedIncoming { get; set; }
    public bool IsOutgoingNoAnswer { get; set; }
    public string? RawJson { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.Now;
}

public class CallCenterSyncLog
{
    public int Id { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public int ImportedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorText { get; set; }
}

public class CallCenterSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public class CallCenterStatusRule
{
    public int Id { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public string? StatusText { get; set; }
    public bool CountAsAnswered { get; set; }
    public bool CountAsMissedIncoming { get; set; }
    public bool CountAsOutgoingNoAnswer { get; set; }
}
