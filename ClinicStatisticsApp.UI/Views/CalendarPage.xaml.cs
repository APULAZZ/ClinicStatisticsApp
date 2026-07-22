using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Media;

namespace ClinicStatisticsApp.UI.Views;

public partial class CalendarPage : UserControl
{
    private readonly CurrentUserInfo _currentUser;
    private readonly CalendarService _calendarService = new();
    private DateTime _cursor = DateTime.Today;
    private CalendarView _view = CalendarView.Week;
    private List<CalendarEvent> _events = [];
    private readonly CultureInfo _culture = CultureInfo.GetCultureInfo("ru-RU");
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly HashSet<string> _shownReminderOccurrences = [];

    public CalendarPage(CurrentUserInfo currentUser)
    {
        InitializeComponent(); _currentUser = currentUser;
        _reminderTimer.Tick += (_, _) => CheckReminders();
        Loaded += (_, _) => { Refresh(); CheckReminders(); _reminderTimer.Start(); };
        Unloaded += (_, _) => _reminderTimer.Stop();
    }
    private bool CanManageAll => string.Equals(_currentUser.FullName.Trim(), "Сергей Елисеенко", StringComparison.OrdinalIgnoreCase);
    private void Refresh()
    {
        var (from, to) = GetRange();
        _events = _calendarService.GetEvents(from, to, _currentUser.UserId);
        PeriodTextBlock.Text = GetPeriodCaption(from, to);
        SetViewButtons();
        CalendarGrid.Children.Clear(); CalendarGrid.RowDefinitions.Clear(); CalendarGrid.ColumnDefinitions.Clear();
        if (_view == CalendarView.Month) BuildMonth(from); else if (_view == CalendarView.Year) BuildYear(); else BuildSchedule(from, to);
    }
    private (DateTime From, DateTime To) GetRange() => _view switch
    {
        CalendarView.Day => (_cursor.Date, _cursor.Date.AddDays(1)),
        CalendarView.Week => (StartOfWeek(_cursor), StartOfWeek(_cursor).AddDays(7)),
        CalendarView.Month => (new DateTime(_cursor.Year, _cursor.Month, 1), new DateTime(_cursor.Year, _cursor.Month, 1).AddMonths(1)),
        _ => (new DateTime(_cursor.Year, 1, 1), new DateTime(_cursor.Year + 1, 1, 1))
    };
    private string GetPeriodCaption(DateTime from, DateTime to) => _view switch
    {
        CalendarView.Day => $"{_culture.TextInfo.ToTitleCase(from.ToString("dddd, d MMMM", _culture))}",
        CalendarView.Week => $"{from:d MMMM} — {to.AddDays(-1):d MMMM yyyy}",
        CalendarView.Month => _culture.TextInfo.ToTitleCase(from.ToString("MMMM yyyy", _culture)),
        _ => from.Year.ToString()
    };
    private static DateTime StartOfWeek(DateTime date) { var offset = ((int)date.DayOfWeek + 6) % 7; return date.Date.AddDays(-offset); }
    private void BuildSchedule(DateTime from, DateTime to)
    {
        var days = _view == CalendarView.Day ? 1 : 7;
        CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
        for (var i = 0; i < days; i++) CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
        for (var hour = 7; hour <= 21; hour++) CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) });
        for (var day = 0; day < days; day++)
        {
            var date = from.AddDays(day); var header = new Button { Content = $"{_culture.TextInfo.ToTitleCase(date.ToString("ddd", _culture))}\n{date:dd}", FontWeight = FontWeights.SemiBold, Background = date.Date == DateTime.Today ? Brush("#DBEAFE") : Brushes.White, BorderBrush = Brush("#E2E8F0"), Tag = date };
            header.Click += DateHeader_Click; Grid.SetColumn(header, day + 1); CalendarGrid.Children.Add(header);
        }
        for (var hour = 7; hour <= 21; hour++)
        {
            var label = new TextBlock { Text = $"{hour:00}:00", Foreground = Brush("#64748B"), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 8, 0), TextAlignment = TextAlignment.Right };
            Grid.SetRow(label, hour - 6); CalendarGrid.Children.Add(label);
            for (var day = 0; day < days; day++)
            {
                var cell = new Border { BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(0, 1, 1, 0), Background = Brushes.White, Tag = from.AddDays(day).AddHours(hour) };
                cell.MouseLeftButtonUp += Cell_MouseLeftButtonUp; Grid.SetRow(cell, hour - 6); Grid.SetColumn(cell, day + 1); CalendarGrid.Children.Add(cell);
            }
        }
        foreach (var item in _events.Where(x => !x.IsAllDay))
        {
            var dayOffset = (item.StartsAt.Date - from.Date).Days; if (dayOffset < 0 || dayOffset >= days) continue;
            var startHour = Math.Clamp(item.StartsAt.Hour, 7, 21); var span = Math.Max(1, (int)Math.Ceiling((item.EndsAt - item.StartsAt).TotalHours));
            var eventButton = EventButton(item, showTime: true); Grid.SetRow(eventButton, startHour - 6); Grid.SetColumn(eventButton, dayOffset + 1); Grid.SetRowSpan(eventButton, Math.Min(span, 22 - startHour)); CalendarGrid.Children.Add(eventButton);
        }
        var allDay = _events.Where(x => x.IsAllDay).ToList(); if (allDay.Count > 0) HintTextBlock.Text = $"Личный календарь · событий на весь день: {string.Join(", ", allDay.Select(x => x.Title))}";
    }
    private void BuildMonth(DateTime from)
    {
        for (var c = 0; c < 7; c++) CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition());
        CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) }); for (var r = 0; r < 6; r++) CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(125) });
        for (var c = 0; c < 7; c++) { var caption = new TextBlock { Text = _culture.DateTimeFormat.AbbreviatedDayNames[(c + 1) % 7], TextAlignment = TextAlignment.Center, FontWeight = FontWeights.SemiBold, Foreground = Brush("#475569") }; Grid.SetColumn(caption, c); CalendarGrid.Children.Add(caption); }
        var first = StartOfWeek(from); for (var i = 0; i < 42; i++)
        {
            var date = first.AddDays(i); var cell = new Border { Background = date.Month == from.Month ? Brushes.White : Brush("#F1F5F9"), BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1, 1, 0, 0), Padding = new Thickness(8), Tag = date };
            cell.MouseLeftButtonUp += Cell_MouseLeftButtonUp; var panel = new StackPanel(); panel.Children.Add(new TextBlock { Text = date.Day.ToString(), FontWeight = date.Date == DateTime.Today ? FontWeights.Bold : FontWeights.Normal, Foreground = date.Date == DateTime.Today ? Brush("#2563EB") : Brush("#334155") });
            foreach (var item in _events.Where(x => x.StartsAt.Date <= date.Date && x.EndsAt.Date >= date.Date).Take(3)) panel.Children.Add(EventButton(item, false));
            cell.Child = panel; Grid.SetColumn(cell, i % 7); Grid.SetRow(cell, i / 7 + 1); CalendarGrid.Children.Add(cell);
        }
    }
    private void BuildYear()
    {
        for (var c = 0; c < 3; c++) CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (var r = 0; r < 4; r++) CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(240) });
        for (var month = 1; month <= 12; month++) { var overview = BuildMonthOverview(new DateTime(_cursor.Year, month, 1)); Grid.SetColumn(overview, (month - 1) % 3); Grid.SetRow(overview, (month - 1) / 3); CalendarGrid.Children.Add(overview); }
    }
    private UIElement BuildMonthOverview(DateTime month)
    {
        var border = new Border { Margin = new Thickness(6), Padding = new Thickness(12), Background = Brushes.White, BorderBrush = Brush("#E2E8F0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8) }; var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = _culture.TextInfo.ToTitleCase(month.ToString("MMMM", _culture)), FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(0, 0, 0, 8) }); var grid = new UniformGrid { Columns = 7 };
        foreach (var dayName in new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" }) grid.Children.Add(new TextBlock { Text = dayName, TextAlignment = TextAlignment.Center, Foreground = Brush("#64748B"), FontSize = 11 });
        var first = StartOfWeek(month); for (var i = 0; i < 42; i++) { var date = first.AddDays(i); var button = new Button { Content = date.Day.ToString(), Padding = new Thickness(2), BorderThickness = new Thickness(0), Background = _events.Any(x => x.StartsAt.Date <= date.Date && x.EndsAt.Date >= date.Date) ? Brush("#DBEAFE") : Brushes.Transparent, Foreground = date.Month == month.Month ? Brush("#334155") : Brush("#94A3B8"), Tag = date }; button.Click += MiniDate_Click; grid.Children.Add(button); }
        panel.Children.Add(grid); border.Child = panel; return border;
    }
    private Button EventButton(CalendarEvent item, bool showTime) { var text = showTime ? $"{item.StartsAt:HH:mm} {item.Title}" : item.Title; var button = new Button { Content = text, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(0, 3, 0, 0), Background = Brush(item.Color), Foreground = Brushes.White, BorderThickness = new Thickness(0), FontSize = 11, ToolTip = string.IsNullOrWhiteSpace(item.Description) ? item.Title : $"{item.Title}\n{item.Description}", Tag = item }; button.Click += EventButton_Click; return button; }
    private static Brush Brush(string color) => (Brush)new BrushConverter().ConvertFromString(color)!;
    private void ViewButton_Click(object sender, RoutedEventArgs e) { _view = Enum.Parse<CalendarView>(((Button)sender).Tag.ToString()!); Refresh(); }
    private void PreviousButton_Click(object sender, RoutedEventArgs e) { _cursor = _view switch { CalendarView.Day => _cursor.AddDays(-1), CalendarView.Week => _cursor.AddDays(-7), CalendarView.Month => _cursor.AddMonths(-1), _ => _cursor.AddYears(-1) }; Refresh(); }
    private void NextButton_Click(object sender, RoutedEventArgs e) { _cursor = _view switch { CalendarView.Day => _cursor.AddDays(1), CalendarView.Week => _cursor.AddDays(7), CalendarView.Month => _cursor.AddMonths(1), _ => _cursor.AddYears(1) }; Refresh(); }
    private void TodayButton_Click(object sender, RoutedEventArgs e) { _cursor = DateTime.Today; Refresh(); }
    private void CreateButton_Click(object sender, RoutedEventArgs e) => EditEvent(new CalendarEvent { StartsAt = _cursor.Date.AddHours(9), EndsAt = _cursor.Date.AddHours(10), Color = "#2563EB" });
    private void EventButton_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is CalendarEvent item) EditEvent(item); }
    private void DateHeader_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is DateTime date) { _cursor = date; _view = CalendarView.Day; Refresh(); } }
    private void MiniDate_Click(object sender, RoutedEventArgs e) { if (((Button)sender).Tag is DateTime date) { _cursor = date; _view = CalendarView.Month; Refresh(); } }
    private void Cell_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { if (e.OriginalSource != sender || ((FrameworkElement)sender).Tag is not DateTime date) return; EditEvent(new CalendarEvent { StartsAt = date, EndsAt = date.AddHours(1), Color = "#2563EB" }); }
    private void EditEvent(CalendarEvent item)
    {
        var window = new CalendarEventWindow(item, _currentUser.UserId, CanManageAll) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() != true) return;
        try { if (window.DeleteRequested) _calendarService.Delete(item.Id, _currentUser.UserId, CanManageAll); else _calendarService.Save(item, window.ParticipantIds, _currentUser.UserId, CanManageAll); Refresh(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Календарь", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private void SetViewButtons() { foreach (var button in new[] { DayButton, WeekButton, MonthButton, YearButton }) { button.Background = button.Tag?.ToString() == _view.ToString() ? Brush("#DBEAFE") : Brushes.Transparent; button.FontWeight = button.Tag?.ToString() == _view.ToString() ? FontWeights.SemiBold : FontWeights.Normal; } }
    private void CheckReminders()
    {
        try
        {
            var now = DateTime.Now;
            var dueEvents = _calendarService.GetEvents(now.AddDays(-1), now.AddHours(2), _currentUser.UserId)
                .Where(x => x.ReminderMinutes.HasValue && x.StartsAt.AddMinutes(-x.ReminderMinutes.Value) <= now && x.StartsAt.AddMinutes(-x.ReminderMinutes.Value) > now.AddSeconds(-35));
            foreach (var item in dueEvents)
            {
                var key = $"{item.Id}:{item.StartsAt.Ticks}";
                if (!_shownReminderOccurrences.Add(key)) continue;
                SystemSounds.Asterisk.Play();
                MessageBox.Show($"{item.Title}\nНачало: {item.StartsAt:dd.MM.yyyy HH:mm}", "Напоминание", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch { /* Напоминание не должно мешать работе календаря при временной недоступности БД. */ }
    }
    private enum CalendarView { Day, Week, Month, Year }
}
