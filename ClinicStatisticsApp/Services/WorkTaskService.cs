using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public class WorkTaskService
{
    public List<WorkTask> GetVisible(int userId, bool canViewAll, string? status = null)
    {
        using var db = DbContextFactory.Create();
        var query = db.WorkTasks.AsNoTracking().Include(x => x.ChecklistItems).Include(x => x.Comments).Include(x => x.StatusHistory).AsQueryable();
        if (!canViewAll) query = query.Where(x => x.CreatedByUserId == userId || x.ResponsibleUserId == userId);
        if (status == "Overdue") query = query.Where(x => x.Status != "Done" && x.DueAt != null && x.DueAt < DateTime.Now);
        else if (!string.IsNullOrWhiteSpace(status) && status != "All") query = query.Where(x => x.Status == status);
        return query.OrderBy(x => x.Status).ThenBy(x => x.DueAt == null).ThenBy(x => x.DueAt).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public WorkTask Save(WorkTask task, int currentUserId, bool canViewAll)
    {
        if (string.IsNullOrWhiteSpace(task.Title)) throw new InvalidOperationException("Укажите название задачи.");
        using var db = DbContextFactory.Create();
        int? notifyUserId = null; string? notificationMessage = null;
        if (task.Id == 0)
        {
            task.CreatedByUserId = currentUserId; task.CreatedAt = DateTime.UtcNow;
            task.CompletedAt = task.Status == "Done" ? DateTime.UtcNow : null;
            task.StatusHistory.Add(new WorkTaskStatusHistory { Status = task.Status, StartedAt = DateTime.UtcNow, ChangedByUserId = currentUserId }); db.WorkTasks.Add(task);
            if (task.ResponsibleUserId.HasValue && task.ResponsibleUserId != currentUserId) { notifyUserId = task.ResponsibleUserId; notificationMessage = $"Вам назначена задача: {task.Title}"; }
        }
        else
        {
            var stored = db.WorkTasks.Include(x => x.ChecklistItems).SingleOrDefault(x => x.Id == task.Id) ?? throw new InvalidOperationException("Задача не найдена.");
            if (!canViewAll && stored.CreatedByUserId != currentUserId && stored.ResponsibleUserId != currentUserId) throw new UnauthorizedAccessException("Нет прав на изменение этой задачи.");
            var isCreator = stored.CreatedByUserId == currentUserId || canViewAll;
            var oldResponsibleUserId = stored.ResponsibleUserId;
            if (isCreator) { stored.Title = task.Title; stored.Description = task.Description; stored.Priority = task.Priority; stored.DueAt = task.DueAt; stored.ResponsibleUserId = task.ResponsibleUserId; }
            if (isCreator && task.ResponsibleUserId.HasValue && task.ResponsibleUserId != oldResponsibleUserId && task.ResponsibleUserId != currentUserId) { notifyUserId = task.ResponsibleUserId; notificationMessage = $"Вам назначена задача: {task.Title}"; }
            if (stored.Status != task.Status)
            {
                var openStage = db.WorkTaskStatusHistory.Where(x => x.WorkTaskId == stored.Id && x.EndedAt == null).OrderByDescending(x => x.StartedAt).FirstOrDefault();
                if (openStage != null) openStage.EndedAt = DateTime.UtcNow;
                db.WorkTaskStatusHistory.Add(new WorkTaskStatusHistory { WorkTaskId = stored.Id, Status = task.Status, StartedAt = DateTime.UtcNow, ChangedByUserId = currentUserId });
            }
            stored.Status = task.Status;
            stored.CompletionResult = task.Status == "Done" ? task.CompletionResult : null;
            stored.CompletedAt = task.Status == "Done" ? stored.CompletedAt ?? DateTime.UtcNow : null;
            db.WorkTaskChecklistItems.RemoveRange(stored.ChecklistItems);
            stored.ChecklistItems = task.ChecklistItems.Select((x, i) => new WorkTaskChecklistItem { Text = x.Text, IsCompleted = x.IsCompleted, SortOrder = i }).ToList(); task = stored;
        }
        db.SaveChanges();
        if (notifyUserId.HasValue) AddNotification(notifyUserId.Value, task.Id, "Assigned", notificationMessage!);
        return task;
    }

    public void AddComment(int taskId, string text, int currentUserId)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        using var db = DbContextFactory.Create();
        var task = db.WorkTasks.SingleOrDefault(x => x.Id == taskId) ?? throw new InvalidOperationException("Задача не найдена.");
        if (task.CreatedByUserId != currentUserId && task.ResponsibleUserId != currentUserId) throw new UnauthorizedAccessException("Нет доступа к комментариям этой задачи.");
        db.WorkTaskComments.Add(new WorkTaskComment { WorkTaskId = taskId, AuthorUserId = currentUserId, Text = text.Trim(), CreatedAt = DateTime.UtcNow }); db.SaveChanges();
        foreach (var userId in new[] { task.CreatedByUserId, task.ResponsibleUserId }.Where(x => x.HasValue).Select(x => x!.Value).Where(x => x != currentUserId).Distinct())
            AddNotification(userId, taskId, "Comment", $"Новый комментарий к задаче: {task.Title}");
    }

    public List<WorkTaskNotification> GetNotifications(int userId) { using var db = DbContextFactory.Create(); return db.WorkTaskNotifications.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.ReadAt != null).ThenByDescending(x => x.CreatedAt).Take(50).ToList(); }
    public int GetUnreadCount(int userId) { using var db = DbContextFactory.Create(); return db.WorkTaskNotifications.Count(x => x.UserId == userId && x.ReadAt == null); }
    public void MarkNotificationsRead(int userId) { using var db = DbContextFactory.Create(); foreach (var notification in db.WorkTaskNotifications.Where(x => x.UserId == userId && x.ReadAt == null)) notification.ReadAt = DateTime.UtcNow; db.SaveChanges(); }
    public void EnsureOverdueNotifications()
    {
        using var db = DbContextFactory.Create();
        var overdue = db.WorkTasks.Where(x => x.Status != "Done" && x.DueAt != null && x.DueAt < DateTime.Now).Select(x => new { x.Id, x.Title, x.CreatedByUserId, x.ResponsibleUserId }).ToList();
        foreach (var task in overdue) foreach (var userId in new[] { task.CreatedByUserId, task.ResponsibleUserId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct()) if (!db.WorkTaskNotifications.Any(x => x.UserId == userId && x.WorkTaskId == task.Id && x.Type == "Overdue")) db.WorkTaskNotifications.Add(new WorkTaskNotification { UserId = userId, WorkTaskId = task.Id, Type = "Overdue", Message = $"Просрочена задача: {task.Title}", CreatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }
    private static void AddNotification(int userId, int taskId, string type, string message) { using var db = DbContextFactory.Create(); db.WorkTaskNotifications.Add(new WorkTaskNotification { UserId = userId, WorkTaskId = taskId, Type = type, Message = message, CreatedAt = DateTime.UtcNow }); db.SaveChanges(); }

    public void Delete(int taskId, int currentUserId, bool canViewAll)
    {
        using var db = DbContextFactory.Create();
        var task = db.WorkTasks.SingleOrDefault(x => x.Id == taskId) ?? throw new InvalidOperationException("Задача не найдена.");
        if (!canViewAll && task.CreatedByUserId != currentUserId) throw new UnauthorizedAccessException("Удалять можно только созданные вами задачи.");
        db.WorkTasks.Remove(task); db.SaveChanges();
    }
}
