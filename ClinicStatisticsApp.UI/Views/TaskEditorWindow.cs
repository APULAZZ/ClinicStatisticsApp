using ClinicStatisticsApp.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public sealed class TaskEditorWindow : Window
{
    private readonly WorkTask _source;
    private readonly int _currentUserId;
    private readonly TextBox _title = new(), _description = new(), _checklist = new(), _comment = new(), _result = new();
    private readonly ComboBox _status = new(), _priority = new(), _responsible = new();
    private readonly DatePicker _dueDate = new();
    public WorkTask Task { get; private set; }
    public string NewComment => _comment.Text.Trim();
    public bool DeleteRequested { get; private set; }

    public TaskEditorWindow(WorkTask source, IReadOnlyList<User> users, int currentUserId, bool canViewAll)
    {
        _source = source; _currentUserId = currentUserId; Task = source;
        Title = source.Id == 0 ? "Новая задача" : "Задача"; Width = 650; Height = 720; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Brushes.White;
        var canEditDetails = source.Id == 0 || source.CreatedByUserId == currentUserId || canViewAll;
        var root = new Grid { Margin = new Thickness(22) }; root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; var panel = new StackPanel(); scroll.Content = panel; Grid.SetRow(scroll, 0); root.Children.Add(scroll);
        panel.Children.Add(Label("Название")); _title.Text = source.Title; _title.IsReadOnly = !canEditDetails; panel.Children.Add(_title);
        panel.Children.Add(Label("Описание")); _description.Text = source.Description ?? ""; _description.AcceptsReturn = true; _description.TextWrapping = TextWrapping.Wrap; _description.Height = 86; _description.IsReadOnly = !canEditDetails; panel.Children.Add(_description);
        panel.Children.Add(Label("Статус")); AddOptions(_status, [("New", "Новая"), ("InProgress", "В работе"), ("Paused", "На паузе"), ("Review", "На проверке"), ("Done", "Готово")], source.Status); panel.Children.Add(_status);
        panel.Children.Add(Label("Приоритет")); AddOptions(_priority, [("Low", "Низкий"), ("Normal", "Обычный"), ("High", "Высокий")], source.Priority); _priority.IsEnabled = canEditDetails; panel.Children.Add(_priority);
        panel.Children.Add(Label("Ответственный")); _responsible.Items.Add(new PersonOption(null, "Не назначен")); foreach (var user in users.Where(x => x.IsActive)) _responsible.Items.Add(new PersonOption(user.Id, user.FullName)); _responsible.SelectedItem = _responsible.Items.Cast<PersonOption>().FirstOrDefault(x => x.Id == source.ResponsibleUserId) ?? _responsible.Items[0]; _responsible.IsEnabled = canEditDetails; panel.Children.Add(_responsible);
        panel.Children.Add(Label("Срок")); _dueDate.SelectedDate = source.DueAt?.Date; _dueDate.IsEnabled = canEditDetails; panel.Children.Add(_dueDate);
        panel.Children.Add(Label("Чек-лист — по одному пункту на строку; [x] означает выполнено")); _checklist.Text = string.Join(Environment.NewLine, source.ChecklistItems.OrderBy(x => x.SortOrder).Select(x => (x.IsCompleted ? "[x] " : "[ ] ") + x.Text)); _checklist.AcceptsReturn = true; _checklist.TextWrapping = TextWrapping.Wrap; _checklist.Height = 94; _checklist.IsReadOnly = !canEditDetails; panel.Children.Add(_checklist);
        if (source.Id != 0 && source.StatusHistory.Count > 0)
        {
            panel.Children.Add(Label("Ход выполнения"));
            var workTime = source.StatusHistory.Where(x => x.Status == "InProgress").Aggregate(TimeSpan.Zero, (sum, x) => sum + ((x.EndedAt ?? DateTime.UtcNow) - x.StartedAt));
            var pauses = source.StatusHistory.Where(x => x.Status == "Paused").ToList();
            var started = source.StatusHistory.Where(x => x.Status == "InProgress").OrderBy(x => x.StartedAt).FirstOrDefault();
            panel.Children.Add(new TextBlock { Text = $"Взята в работу: {(started is null ? "ещё не брали" : started.StartedAt.ToLocalTime().ToString("dd.MM.yyyy HH:mm"))}. Всего в работе: {FormatDuration(workTime)}. Пауз: {pauses.Count}.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#334155") });
            foreach (var stage in source.StatusHistory.OrderBy(x => x.StartedAt)) panel.Children.Add(new TextBlock { Text = $"• {StatusCaption(stage.Status)}: {stage.StartedAt.ToLocalTime():dd.MM HH:mm} — {(stage.EndedAt?.ToLocalTime().ToString("dd.MM HH:mm") ?? "сейчас")} ({FormatDuration((stage.EndedAt ?? DateTime.UtcNow) - stage.StartedAt)})", Foreground = Brush("#64748B"), FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });
        }
        panel.Children.Add(Label("Результат выполнения (заполняется при завершении)")); _result.Text = source.CompletionResult ?? ""; _result.AcceptsReturn = true; _result.TextWrapping = TextWrapping.Wrap; _result.Height = 58; panel.Children.Add(_result);
        if (source.Id != 0 && source.Comments.Count > 0) { panel.Children.Add(Label("История комментариев")); foreach (var comment in source.Comments.OrderByDescending(x => x.CreatedAt).Take(8)) panel.Children.Add(new TextBlock { Text = $"{comment.CreatedAt:dd.MM HH:mm} · Пользователь #{comment.AuthorUserId}: {comment.Text}", TextWrapping = TextWrapping.Wrap, Foreground = Brush("#475569"), Margin = new Thickness(0, 0, 0, 5) }); }
        if (source.Id != 0) { panel.Children.Add(Label("Комментарий / отчёт о выполнении")); _comment.AcceptsReturn = true; _comment.TextWrapping = TextWrapping.Wrap; _comment.Height = 65; panel.Children.Add(_comment); }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        if (source.Id != 0 && (canViewAll || source.CreatedByUserId == currentUserId)) { var delete = new Button { Content = "Удалить", Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0) }; delete.Click += (_, _) => { DeleteRequested = MessageBox.Show("Удалить задачу?", "Задачи", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes; if (DeleteRequested) { DialogResult = true; Close(); } }; buttons.Children.Add(delete); }
        var cancel = new Button { Content = "Отмена", Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(0, 0, 8, 0) }; cancel.Click += (_, _) => Close(); buttons.Children.Add(cancel);
        var save = new Button { Content = "Сохранить", Padding = new Thickness(14, 7, 14, 7), Background = Brush("#2563EB"), Foreground = Brushes.White, BorderBrush = Brush("#2563EB") }; save.Click += (_, _) => Save(); buttons.Children.Add(save); Grid.SetRow(buttons, 1); root.Children.Add(buttons); Content = root;
    }

    private void Save()
    {
        var status = ((Option)_status.SelectedItem).Value; var priority = ((Option)_priority.SelectedItem).Value; var responsible = ((PersonOption)_responsible.SelectedItem).Id;
        Task = new WorkTask { Id = _source.Id, Title = _title.Text.Trim(), Description = _description.Text.Trim(), Status = status, Priority = priority, DueAt = _dueDate.SelectedDate?.Add(_source.DueAt?.TimeOfDay ?? TimeSpan.FromHours(18)), ResponsibleUserId = responsible, CreatedByUserId = _source.CreatedByUserId, CreatedAt = _source.CreatedAt, CompletionResult = _result.Text.Trim(), ChecklistItems = ParseChecklist(_checklist.Text) };
        if (string.IsNullOrWhiteSpace(Task.Title)) { MessageBox.Show("Укажите название задачи.", "Задачи"); return; }
        DialogResult = true; Close();
    }
    private static List<WorkTaskChecklistItem> ParseChecklist(string text) => text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Select((line, index) => new WorkTaskChecklistItem { Text = line.Replace("[x]", "", StringComparison.OrdinalIgnoreCase).Replace("[ ]", "").Trim(), IsCompleted = line.TrimStart().StartsWith("[x]", StringComparison.OrdinalIgnoreCase), SortOrder = index }).Where(x => x.Text.Length > 0).ToList();
    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Foreground = Brush("#334155"), Margin = new Thickness(0, 12, 0, 4) };
    private static void AddOptions(ComboBox box, IEnumerable<(string Value, string Caption)> options, string selected) { foreach (var (value, caption) in options) box.Items.Add(new Option(value, caption)); box.SelectedItem = box.Items.Cast<Option>().First(x => x.Value == selected); }
    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    private static string StatusCaption(string status) => status switch { "New" => "Новая", "InProgress" => "В работе", "Paused" => "Пауза", "Review" => "На проверке", "Done" => "Завершена", _ => status };
    private static string FormatDuration(TimeSpan value) => value.TotalDays >= 1 ? $"{(int)value.TotalDays} д. {value.Hours} ч." : $"{Math.Max(0, (int)value.TotalHours)} ч. {value.Minutes} мин.";
    private sealed record Option(string Value, string Caption) { public override string ToString() => Caption; }
    private sealed record PersonOption(int? Id, string Name) { public override string ToString() => Name; }
}
