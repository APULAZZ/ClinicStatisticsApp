using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public class CalendarService
{
    public List<CalendarEvent> GetEvents(DateTime from, DateTime to, int currentUserId)
    {
        using var db = DbContextFactory.Create();
        var sourceEvents = db.CalendarEvents.AsNoTracking()
            .Where(x => (x.CreatedByUserId == currentUserId || x.Participants.Any(p => p.UserId == currentUserId))
                && (x.StartsAt < to || x.RecurrenceType != "None")
                && (x.RecursUntil == null || x.RecursUntil >= from))
            .Include(x => x.Participants)
            .OrderBy(x => x.StartsAt)
            .ToList();
        return sourceEvents.SelectMany(x => ExpandOccurrences(x, from, to)).OrderBy(x => x.StartsAt).ToList();
    }

    public CalendarEvent Save(CalendarEvent calendarEvent, IEnumerable<int> participantUserIds, int currentUserId, bool canManageAll)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.Title))
            throw new InvalidOperationException("Укажите название события.");

        if (calendarEvent.EndsAt <= calendarEvent.StartsAt)
            throw new InvalidOperationException("Время окончания должно быть позже времени начала.");

        using var db = DbContextFactory.Create();
        if (calendarEvent.Id == 0)
        {
            calendarEvent.CreatedByUserId = currentUserId;
            calendarEvent.CreatedAt = DateTime.UtcNow;
            calendarEvent.Participants = participantUserIds.Distinct().Where(x => x != currentUserId)
                .Select(x => new CalendarEventParticipant { UserId = x }).ToList();
            db.CalendarEvents.Add(calendarEvent);
        }
        else
        {
            var stored = db.CalendarEvents.SingleOrDefault(x => x.Id == calendarEvent.Id)
                ?? throw new InvalidOperationException("Событие не найдено.");
            if (!canManageAll && stored.CreatedByUserId != currentUserId)
                throw new UnauthorizedAccessException("Можно изменять только созданные вами события.");

            stored.Title = calendarEvent.Title;
            stored.Description = calendarEvent.Description;
            stored.StartsAt = calendarEvent.StartsAt;
            stored.EndsAt = calendarEvent.EndsAt;
            stored.IsAllDay = calendarEvent.IsAllDay;
            stored.Color = calendarEvent.Color;
            stored.RecurrenceType = calendarEvent.RecurrenceType;
            stored.RecursUntil = calendarEvent.RecursUntil;
            stored.ReminderMinutes = calendarEvent.ReminderMinutes;
            var selectedIds = participantUserIds.Distinct().Where(x => x != currentUserId).ToHashSet();
            var existingParticipants = db.CalendarEventParticipants.Where(x => x.CalendarEventId == stored.Id).ToList();
            db.CalendarEventParticipants.RemoveRange(existingParticipants.Where(x => !selectedIds.Contains(x.UserId)));
            foreach (var userId in selectedIds.Except(existingParticipants.Select(x => x.UserId)))
                db.CalendarEventParticipants.Add(new CalendarEventParticipant { CalendarEventId = stored.Id, UserId = userId });
            calendarEvent = stored;
        }

        db.SaveChanges();
        return calendarEvent;
    }

    public void Delete(int eventId, int currentUserId, bool canManageAll)
    {
        using var db = DbContextFactory.Create();
        var calendarEvent = db.CalendarEvents.SingleOrDefault(x => x.Id == eventId)
            ?? throw new InvalidOperationException("Событие не найдено.");
        if (!canManageAll && calendarEvent.CreatedByUserId != currentUserId)
            throw new UnauthorizedAccessException("Можно удалять только созданные вами события.");

        db.CalendarEvents.Remove(calendarEvent);
        db.SaveChanges();
    }

    private static IEnumerable<CalendarEvent> ExpandOccurrences(CalendarEvent source, DateTime from, DateTime to)
    {
        var occurrenceStart = source.StartsAt;
        var duration = source.EndsAt - source.StartsAt;
        while (occurrenceStart < to)
        {
            if (source.RecursUntil.HasValue && occurrenceStart.Date > source.RecursUntil.Value.Date) yield break;
            var occurrenceEnd = occurrenceStart + duration;
            if (occurrenceEnd > from)
            {
                yield return new CalendarEvent
                {
                    Id = source.Id, Title = source.Title, Description = source.Description, StartsAt = occurrenceStart, EndsAt = occurrenceEnd,
                    IsAllDay = source.IsAllDay, Color = source.Color, RecurrenceType = source.RecurrenceType, RecursUntil = source.RecursUntil,
                    ReminderMinutes = source.ReminderMinutes, CreatedByUserId = source.CreatedByUserId, CreatedAt = source.CreatedAt, Participants = source.Participants
                };
            }
            occurrenceStart = source.RecurrenceType switch
            {
                "Daily" => occurrenceStart.AddDays(1),
                "Weekly" => occurrenceStart.AddDays(7),
                "Monthly" => occurrenceStart.AddMonths(1),
                _ => DateTime.MaxValue
            };
        }
    }
}
