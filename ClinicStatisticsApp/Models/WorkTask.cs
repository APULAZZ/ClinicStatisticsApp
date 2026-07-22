namespace ClinicStatisticsApp.Models;

public class WorkTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "New";
    public string Priority { get; set; } = "Normal";
    public DateTime? DueAt { get; set; }
    public int CreatedByUserId { get; set; }
    public int? ResponsibleUserId { get; set; }
    public int? CrmPersonId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? CompletionResult { get; set; }
    public List<WorkTaskChecklistItem> ChecklistItems { get; set; } = [];
    public List<WorkTaskComment> Comments { get; set; } = [];
    public List<WorkTaskStatusHistory> StatusHistory { get; set; } = [];
}

public class WorkTaskComment
{
    public int Id { get; set; }
    public int WorkTaskId { get; set; }
    public int AuthorUserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public WorkTask WorkTask { get; set; } = null!;
}

public class WorkTaskStatusHistory
{
    public int Id { get; set; }
    public int WorkTaskId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int ChangedByUserId { get; set; }
    public WorkTask WorkTask { get; set; } = null!;
}

public class WorkTaskNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int WorkTaskId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public WorkTask WorkTask { get; set; } = null!;
}

public class WorkTaskChecklistItem
{
    public int Id { get; set; }
    public int WorkTaskId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
    public WorkTask WorkTask { get; set; } = null!;
}
