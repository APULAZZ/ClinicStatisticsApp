using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public partial class TasksPage : UserControl
{
    private readonly CurrentUserInfo _currentUser;
    private readonly WorkTaskService _service = new();
    private readonly UserService _userService = new();
    private readonly bool _canViewAll;
    private List<WorkTask> _tasks = [];

    public TasksPage(CurrentUserInfo currentUser)
    {
        InitializeComponent(); _currentUser = currentUser; _canViewAll = currentUser.RoleCode == "Admin";
        AccessTextBlock.Text = _canViewAll ? "Показаны все задачи" : "Ваши задачи и созданные вами";
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        _service.EnsureOverdueNotifications();
        var unreadCount = _service.GetUnreadCount(_currentUser.UserId);
        NotificationsButton.Content = unreadCount > 0 ? $"Уведомления ({unreadCount})" : "Уведомления";
        var status = (StatusFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "All";
        _tasks = _service.GetVisible(_currentUser.UserId, _canViewAll, status);
        var scope = (ScopeFilterComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (scope == "Created") _tasks = _tasks.Where(x => x.CreatedByUserId == _currentUser.UserId).ToList();
        if (scope == "Responsible") _tasks = _tasks.Where(x => x.ResponsibleUserId == _currentUser.UserId).ToList();
        BuildBoard();
    }

    private void BuildBoard()
    {
        BoardGrid.Children.Clear(); BoardGrid.ColumnDefinitions.Clear();
        var columns = new[] { ("New", "Новые"), ("InProgress", "В работе"), ("Paused", "На паузе"), ("Review", "На проверке"), ("Done", "Готово") };
        foreach (var _ in columns) BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        for (var i = 0; i < columns.Length; i++)
        {
            var (status, title) = columns[i];
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = $"{title}  ·  {_tasks.Count(x => x.Status == status)}", FontWeight = FontWeights.SemiBold, FontSize = 16, Foreground = Color("#172033"), Margin = new Thickness(0, 0, 0, 10) });
            foreach (var task in _tasks.Where(x => x.Status == status)) panel.Children.Add(TaskCard(task));
            var border = new Border { Child = panel, Margin = new Thickness(0, 0, 14, 0), Padding = new Thickness(10), Background = Color("#F1F5F9"), CornerRadius = new CornerRadius(10), MinHeight = 300 };
            Grid.SetColumn(border, i); BoardGrid.Children.Add(border);
        }
    }

    private UIElement TaskCard(WorkTask task)
    {
        var checklistDone = task.ChecklistItems.Count(x => x.IsCompleted);
        var dueText = task.DueAt is null ? "Без срока" : task.DueAt.Value < DateTime.Now && task.Status != "Done" ? $"Просрочено: {task.DueAt:dd.MM HH:mm}" : $"Срок: {task.DueAt:dd.MM HH:mm}";
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = task.Title, FontWeight = FontWeights.SemiBold, Foreground = Color("#172033"), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(task.Description)) content.Children.Add(new TextBlock { Text = task.Description, Foreground = Color("#64748B"), FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 38, Margin = new Thickness(0, 5, 0, 0) });
        content.Children.Add(new TextBlock { Text = $"{PriorityCaption(task.Priority)} · {dueText}", Foreground = task.DueAt < DateTime.Now && task.Status != "Done" ? Color("#DC2626") : Color("#475569"), FontSize = 11, Margin = new Thickness(0, 9, 0, 0) });
        if (task.ChecklistItems.Count > 0) content.Children.Add(new TextBlock { Text = $"Чек-лист: {checklistDone}/{task.ChecklistItems.Count}", Foreground = Color("#475569"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        var activeStage = task.StatusHistory.OrderByDescending(x => x.StartedAt).FirstOrDefault();
        if (activeStage != null) content.Children.Add(new TextBlock { Text = $"В статусе: {Duration(DateTime.UtcNow - activeStage.StartedAt)} · комментариев: {task.Comments.Count}", Foreground = Color("#475569"), FontSize = 11, Margin = new Thickness(0, 3, 0, 0) });
        if (task.Status == "Done" && !string.IsNullOrWhiteSpace(task.CompletionResult)) content.Children.Add(new TextBlock { Text = $"Результат: {task.CompletionResult}", Foreground = Color("#166534"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
        var button = new Button { Content = content, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 9), Background = Brushes.White, BorderBrush = Color("#E2E8F0"), Tag = task };
        button.Click += TaskCard_Click; return button;
    }

    private static string PriorityCaption(string priority) => priority switch { "High" => "Высокий приоритет", "Low" => "Низкий приоритет", _ => "Обычный приоритет" };
    private static Brush Color(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    private void CreateButton_Click(object sender, RoutedEventArgs e) => Edit(new WorkTask { ResponsibleUserId = _currentUser.UserId, DueAt = DateTime.Today.AddDays(1).AddHours(18) });
    private void TaskCard_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is WorkTask task) Edit(task); }
    private void Edit(WorkTask task)
    {
        var editor = new TaskEditorWindow(task, _userService.GetAll(), _currentUser.UserId, _canViewAll) { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;
        try { if (editor.DeleteRequested) _service.Delete(task.Id, _currentUser.UserId, _canViewAll); else { var saved = _service.Save(editor.Task, _currentUser.UserId, _canViewAll); _service.AddComment(saved.Id, editor.NewComment, _currentUser.UserId); } Refresh(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Задачи", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void RefreshButton_Click(object sender, RoutedEventArgs e) => Refresh();
    private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) Refresh(); }
    private void ScopeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (IsLoaded) Refresh(); }
    private void NotificationsButton_Click(object sender, RoutedEventArgs e)
    {
        var notifications = _service.GetNotifications(_currentUser.UserId);
        new TaskNotificationsWindow(notifications) { Owner = Window.GetWindow(this) }.ShowDialog();
        _service.MarkNotificationsRead(_currentUser.UserId); Refresh();
    }
    private static string Duration(TimeSpan duration) => duration.TotalDays >= 1 ? $"{(int)duration.TotalDays} д. {duration.Hours} ч." : $"{Math.Max(0, (int)duration.TotalHours)} ч. {duration.Minutes} мин.";
}
