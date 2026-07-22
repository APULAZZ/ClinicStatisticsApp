namespace ClinicStatisticsApp.Models;

public class CalendarEvent
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsAllDay { get; set; }
    public string Color { get; set; } = "#2563EB";
    public string RecurrenceType { get; set; } = "None";
    public DateTime? RecursUntil { get; set; }
    public int? ReminderMinutes { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CalendarEventParticipant> Participants { get; set; } = [];
}

public class CalendarEventParticipant
{
    public int CalendarEventId { get; set; }
    public int UserId { get; set; }
    public CalendarEvent CalendarEvent { get; set; } = null!;
}
