using ClinicStatisticsApp.CallCenter.Services;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public partial class CallCenterStatisticsPage : UserControl
{
    private readonly bool _isGroupStatistics;
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly MangoCallImportService _import;
    private readonly CallCenterStatisticsService _statistics;
    private MultiChoiceFilter _employees = null!, _groups = null!, _topics = null!;
    private bool _suppress, _filtersLoaded, _loaded;
    private DateTime _periodFrom, _periodTo;
    private List<CallCenterEmployeeStatRow> _rows = [];
    private string? _sortMember;
    private ListSortDirection _sortDirection;

    public CallCenterStatisticsPage(bool isGroupStatistics)
    {
        InitializeComponent();
        _isGroupStatistics = isGroupStatistics;
        var api = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load());
        _import = new MangoCallImportService(_db, api);
        _statistics = new CallCenterStatisticsService(_db);
        _employees = CreateFilter(EmployeeComboBox, "Все сотрудники", "Без сотрудников");
        _groups = CreateFilter(GroupComboBox, "Все группы", "Без групп");
        _topics = CreateFilter(TopicComboBox, "Все тематики", "Без тематик", includeIgnoreTopics: true);
        PeriodComboBox.SelectedValue = "Today";
        ApplyPeriod("Today");
        Loaded += async (_, _) => await LoadPageSafelyAsync();
        Unloaded += (_, _) => _db.Dispose();
    }

    private async Task LoadAsync()
    {
        if (!_filtersLoaded) await LoadFiltersAsync();
        if (_loaded) return;
        await LoadDataAsync();
        _loaded = true;
    }

    private async Task LoadPageSafelyAsync()
    {
        try
        {
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            // A page can be unloaded while its first database read is still pending.
            // This is normal during navigation and must not terminate the application.
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка загрузки статистики", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task LoadFiltersAsync()
    {
        var employees = await _db.CallCenterEmployees.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new Item(x.Id, x.FullName)).ToListAsync();
        var groups = await _db.CallCenterGroups.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new Item(x.Id, x.Name)).ToListAsync();
        var topics = await _db.CallCenterTopics.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new Item(x.Id, x.Name)).ToListAsync();
        SetChoices(_groups, groups);
        SelectDefaultGroup("Коллцентр");
        SetChoices(_topics, topics);
        await UpdateEmployeesForGroupsAsync();
        _filtersLoaded = true;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_filtersLoaded) await LoadFiltersAsync();
        await LoadDataAsync(synchronize: true);
    }

    private async Task LoadDataAsync(bool synchronize = false)
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        using var busy = App.Busy.Begin(synchronize ? "Синхронизация и расчёт статистики…" : "Формируем статистику сотрудников…");
        try
        {
            RefreshButton.IsEnabled = false;
            if (synchronize) await _import.EnsurePeriodImportedAsync(from, to);
            if (_isGroupStatistics)
            {
                StatsDataGrid.ItemsSource = await _statistics.GetGroupStatsAsync(from, to);
                SummaryTextBlock.Text = "Статистика групп";
                return;
            }
            var filter = new CallCenterEmployeeStatisticsFilter
            {
                EmployeeIds = _employees.All.IsChecked == true ? null : SelectedIds(_employees),
                WithoutEmployees = _employees.Without.IsChecked == true,
                GroupIds = SelectedIds(_groups),
                WithoutGroups = _groups.Without.IsChecked == true,
                LimitEmployeesToGroups = _groups.All.IsChecked != true && _groups.Without.IsChecked != true,
                TopicIds = _topics.Ignore?.IsChecked == true ? null : SelectedIds(_topics),
                WithoutTopics = _topics.Without.IsChecked == true,
                IgnoreTopics = _topics.Ignore?.IsChecked == true
            };
            _rows = await _statistics.GetEmployeeStatsAsync(from, to, filter);
            DisplayRows();
            SummaryTextBlock.Text = $"Сотрудников в отчёте: {_rows.Count(x => !x.IsTotal):N0}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка синхронизации", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { RefreshButton.IsEnabled = true; }
    }

    private void PeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PeriodComboBox.SelectedValue is not string period) return;
        var custom = period == "Custom";
        CustomPeriodPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed;
        PeriodHintTextBlock.Visibility = custom ? Visibility.Collapsed : Visibility.Visible;
        if (!custom) ApplyPeriod(period);
    }

    private void ApplyPeriod(string period)
    {
        var today = DateTime.Today;
        _periodFrom = period switch { "Yesterday" => today.AddDays(-1), "Week" => today.AddDays(-((int)today.DayOfWeek + 6) % 7), "SevenDays" => today.AddDays(-6), "Month" => new DateTime(today.Year, today.Month, 1), "ThirtyDays" => today.AddDays(-29), _ => today };
        _periodTo = period == "Yesterday" ? today.AddDays(-1) : today;
        FromDatePicker.SelectedDate = _periodFrom; ToDatePicker.SelectedDate = _periodTo;
        PeriodHintTextBlock.Text = $"{_periodFrom:dd.MM.yyyy} — {_periodTo:dd.MM.yyyy}";
    }

    private MultiChoiceFilter CreateFilter(ComboBox combo, string allText, string withoutText, bool includeIgnoreTopics = false)
    {
        combo.Visibility = Visibility.Collapsed;
        var host = (StackPanel)combo.Parent;
        var caption = new TextBlock { Text = allText, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), VerticalAlignment = VerticalAlignment.Center };
        var button = new Button { Width = combo.Width, Height = combo.Height, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(201, 215, 234)), BorderThickness = new Thickness(1), Padding = new Thickness(8, 0, 8, 0), HorizontalContentAlignment = HorizontalAlignment.Left, Content = caption };
        var all = new CheckBox { Content = allText, Margin = new Thickness(4, 3, 4, 6) };
        var without = new CheckBox { Content = withoutText, Margin = new Thickness(4, 3, 4, 8) };
        var choices = new StackPanel(); var content = new StackPanel { Margin = new Thickness(8) };
        content.Children.Add(all); content.Children.Add(without);
        var filter = new MultiChoiceFilter(allText, withoutText, caption, all, without, choices);
        if (includeIgnoreTopics) { filter.Ignore = new CheckBox { Content = "Без учёта тематик", Margin = new Thickness(4, 3, 4, 8) }; content.Children.Add(filter.Ignore); filter.Ignore.Checked += (_, _) => Changed(filter); filter.Ignore.Unchecked += (_, _) => Changed(filter); }
        content.Children.Add(new Separator()); content.Children.Add(new ScrollViewer { Content = choices, MaxHeight = 310, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinWidth = combo.Width });
        var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(201, 215, 234)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), Child = content } };
        button.Click += (_, _) => popup.IsOpen = !popup.IsOpen; all.Checked += (_, _) => AllChanged(filter); all.Unchecked += (_, _) => AllChanged(filter); without.Checked += (_, _) => Changed(filter); without.Unchecked += (_, _) => Changed(filter);
        host.Children.Add(button); host.Children.Add(popup); return filter;
    }

    private void SetChoices(MultiChoiceFilter filter, IEnumerable<Item> items)
    {
        _suppress = true; filter.Choices.Children.Clear(); filter.Items.Clear();
        foreach (var item in items.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(x => new Item(x.First().Id, x.First().Name)))
        { var check = new CheckBox { Content = item.Name, IsChecked = true, Margin = new Thickness(4, 3, 4, 3) }; check.Checked += (_, _) => Changed(filter); check.Unchecked += (_, _) => Changed(filter); filter.Choices.Children.Add(check); filter.Items.Add((item.Id, check)); }
        filter.All.IsChecked = true; filter.Without.IsChecked = false; _suppress = false; UpdateCaption(filter);
    }

    private void AllChanged(MultiChoiceFilter filter) { if (_suppress) return; _suppress = true; filter.Without.IsChecked = false; if (filter.Ignore != null) filter.Ignore.IsChecked = false; foreach (var (_, c) in filter.Items) c.IsChecked = filter.All.IsChecked == true; _suppress = false; UpdateCaption(filter); if (filter == _groups) _ = UpdateEmployeesForGroupsAsync(); }
    private void Changed(MultiChoiceFilter filter) { if (_suppress) return; _suppress = true; if (filter.Without.IsChecked == true || filter.Ignore?.IsChecked == true) { filter.All.IsChecked = false; foreach (var (_, c) in filter.Items) c.IsChecked = false; } else { filter.All.IsChecked = filter.Items.Count > 0 && filter.Items.All(x => x.Check.IsChecked == true); } _suppress = false; UpdateCaption(filter); if (filter == _groups) _ = UpdateEmployeesForGroupsAsync(); }
    private async Task UpdateEmployeesForGroupsAsync() { var people = await _db.CallCenterEmployees.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new Item(x.Id, x.FullName)).ToListAsync(); var cc = _groups.Items.Any(x => x.Check.IsChecked == true && string.Equals(x.Check.Content?.ToString(), "Коллцентр", StringComparison.OrdinalIgnoreCase)); if (_groups.All.IsChecked != true && cc) people = people.Where(x => x.Name.StartsWith("КЦ ", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name, "Зоя Ершова", StringComparison.OrdinalIgnoreCase)).ToList(); SetChoices(_employees, people); }
    private void SelectDefaultGroup(string name) { _suppress = true; _groups.All.IsChecked = false; foreach (var (_, c) in _groups.Items) c.IsChecked = string.Equals(c.Content?.ToString(), name, StringComparison.OrdinalIgnoreCase); _suppress = false; UpdateCaption(_groups); }
    private static IReadOnlyCollection<int>? SelectedIds(MultiChoiceFilter f) => f.Without.IsChecked == true || f.Ignore?.IsChecked == true ? null : f.Items.Where(x => x.Check.IsChecked == true).Select(x => x.Id).ToList();
    private static void UpdateCaption(MultiChoiceFilter f) { if (f.Ignore?.IsChecked == true) f.Caption.Text = "Без учёта тематик"; else if (f.Without.IsChecked == true) f.Caption.Text = f.WithoutText; else if (f.All.IsChecked == true) f.Caption.Text = f.AllText; else { var n = f.Items.Count(x => x.Check.IsChecked == true); f.Caption.Text = n == 0 ? "Не выбрано" : n == 1 ? f.Items.First(x => x.Check.IsChecked == true).Check.Content?.ToString() ?? "Выбрано" : $"Выбрано: {n}"; } }
    private bool TryGetPeriod(out DateTime from, out DateTime to) { from = default; to = default; if (FromDatePicker.SelectedDate is not DateTime fd || ToDatePicker.SelectedDate is not DateTime td || !TimeOnly.TryParse(FromTimeTextBox.Text, out var ft) || !TimeOnly.TryParse(ToTimeTextBox.Text, out var tt)) return false; from = fd.Date.Add(ft.ToTimeSpan()); to = td.Date.Add(tt.ToTimeSpan()).AddSeconds(59); return to >= from; }
    private void StatsDataGrid_Sorting(object sender, DataGridSortingEventArgs e) { if (_isGroupStatistics) return; var member = e.Column.SortMemberPath; if (string.IsNullOrWhiteSpace(member) && e.Column is DataGridBoundColumn b && b.Binding is Binding binding) member = binding.Path?.Path; if (string.IsNullOrWhiteSpace(member)) return; e.Handled = true; _sortDirection = _sortMember == member && _sortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending; _sortMember = member; DisplayRows(); }
    private void DisplayRows() { var total = _rows.FirstOrDefault(x => x.IsTotal); IEnumerable<CallCenterEmployeeStatRow> rows = _rows.Where(x => !x.IsTotal); if (_sortMember != null) { var p = typeof(CallCenterEmployeeStatRow).GetProperty(_sortMember); if (p != null) rows = _sortDirection == ListSortDirection.Ascending ? rows.OrderBy(x => p.GetValue(x)) : rows.OrderByDescending(x => p.GetValue(x)); } StatsDataGrid.ItemsSource = total == null ? rows.ToList() : rows.Append(total).ToList(); }
    private sealed record Item(int Id, string Name);
    private sealed class MultiChoiceFilter(string allText, string withoutText, TextBlock caption, CheckBox all, CheckBox without, StackPanel choices) { public string AllText { get; } = allText; public string WithoutText { get; } = withoutText; public TextBlock Caption { get; } = caption; public CheckBox All { get; } = all; public CheckBox Without { get; } = without; public CheckBox? Ignore { get; set; } public StackPanel Choices { get; } = choices; public List<(int Id, CheckBox Check)> Items { get; } = []; }
}
