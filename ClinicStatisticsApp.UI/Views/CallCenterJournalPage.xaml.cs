using ClinicStatisticsApp.CallCenter.Services;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public partial class CallCenterJournalPage : UserControl
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly MangoCallImportService _import;
    private bool _ready;
    private MultiChoiceFilter _employeeFilter = null!;
    private MultiChoiceFilter _groupFilter = null!;
    private TextBlock _topicCaption = null!;
    private Popup _topicPopup = null!;
    private StackPanel _topicChoices = null!;
    private CheckBox _allTopics = null!;
    private CheckBox _withoutTopics = null!;
    private readonly List<(int Id, CheckBox CheckBox)> _topicChecks = [];
    private bool _suppressChoices;
    private List<JournalRow> _journalRows = [];
    private string? _sortMember;
    private ListSortDirection _sortDirection;

    public CallCenterJournalPage()
    {
        InitializeComponent();
        CallsDataGrid.MouseDoubleClick += CallsDataGrid_MouseDoubleClick;
        CallsDataGrid.Sorting += CallsDataGrid_Sorting;
        var journalTextStyle = new Style(typeof(TextBlock));
        journalTextStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(10, 0, 10, 0)));
        journalTextStyle.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        foreach (var column in CallsDataGrid.Columns)
        {
            column.Width = DataGridLength.SizeToCells;
            if (column is DataGridTextColumn textColumn)
                textColumn.ElementStyle = journalTextStyle;
        }
        // The automatic content width makes long headers unreadable in an empty or short result set.
        // Keep the two informative columns wide enough for their captions, as in the standalone journal.
        CallsDataGrid.Columns[4].Width = 220;
        CallsDataGrid.Columns[6].Width = 120;
        CallsDataGrid.CellStyle = (Style)Resources["JournalCellStyle"];
        CallsDataGrid.ColumnHeaderStyle = (Style)Resources["JournalHeaderStyle"];
        CallsDataGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;
        _employeeFilter = ConfigureMultiChoiceFilter(EmployeeComboBox, "Все сотрудники", "Без сотрудников");
        _groupFilter = ConfigureMultiChoiceFilter(GroupComboBox, "Все группы", "Без групп");
        ConfigureTopicFilter();
        _import = new MangoCallImportService(_db, new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load()));
        PeriodComboBox.SelectedValue = "Today"; ApplyPeriod("Today");
        Loaded += async (_, _) => { await LoadFiltersAsync(); _ready = true; await LoadCallsAsync(); };
        Unloaded += (_, _) => _db.Dispose();
    }

    private async Task LoadFiltersAsync()
    {
        var employees = await _db.CallCenterEmployees.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new FilterOption(x.Id, x.FullName)).ToListAsync();
        var groups = await _db.CallCenterGroups.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new FilterOption(x.Id, x.Name)).ToListAsync();
        var topics = await _db.CallCenterTopics.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).Select(x => new FilterOption(x.Id, x.Name)).ToListAsync();
        ConfigureChoices(_employeeFilter, employees); ConfigureChoices(_groupFilter, groups); ConfigureTopicChoices(topics);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        try { using var busy = App.Busy.Begin("Синхронизация звонков с Mango…"); SetBusy(true); await _import.EnsurePeriodImportedAsync(from, to); await LoadFiltersAsync(); await LoadCallsAsync(); StatusTextBlock.Text += " · синхронизировано с Mango"; }
        catch (Exception ex) { MessageBox.Show($"Не удалось обновить журнал из Mango.\n\n{ex.Message}", "Ошибка синхронизации", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { SetBusy(false); }
    }
    private async void QuickPeriodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PeriodComboBox.SelectedValue is not string period) return;
        var custom = period == "Custom"; CustomPeriodPanel.Visibility = custom ? Visibility.Visible : Visibility.Collapsed; PeriodHintTextBlock.Visibility = custom ? Visibility.Collapsed : Visibility.Visible;
        if (!custom) ApplyPeriod(period); if (_ready) await LoadCallsAsync();
    }
    private async void PeriodChanged(object sender, SelectionChangedEventArgs e) { if (_ready && PeriodComboBox.SelectedValue as string == "Custom") await LoadCallsAsync(); }
    private async void FilterChanged(object sender, SelectionChangedEventArgs e) { if (_ready) await LoadCallsAsync(); }
    private async void SearchTextChanged(object sender, TextChangedEventArgs e) { if (_ready) await LoadCallsAsync(); }
    private async void ResetButton_Click(object sender, RoutedEventArgs e) { SelectAll(_employeeFilter); SelectAll(_groupFilter); SelectAllTopics(); DirectionComboBox.SelectedIndex = DurationComboBox.SelectedIndex = 0; SearchTextBox.Clear(); await LoadCallsAsync(); }

    private void ApplyPeriod(string period)
    {
        var today = DateTime.Today; (DateTime from, DateTime to) = period switch { "Yesterday" => (today.AddDays(-1), today.AddDays(-1)), "SevenDays" => (today.AddDays(-6), today), "Week" => (today.AddDays(-((int)today.DayOfWeek + 6) % 7), today), "Month" => (new DateTime(today.Year, today.Month, 1), today), "ThirtyDays" => (today.AddDays(-29), today), _ => (today, today) };
        FromDatePicker.SelectedDate = from; ToDatePicker.SelectedDate = to; PeriodHintTextBlock.Text = $"{from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
    }
    private async Task LoadCallsAsync()
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        var query = _db.CallCenterCallRecords.AsNoTracking().Include(x => x.Employee).Include(x => x.Group).Include(x => x.Topic).Where(x => x.CallDateTime >= from && x.CallDateTime <= to);
        var employeeIds = SelectedIds(_employeeFilter); var groupIds = SelectedIds(_groupFilter); var topicIds = _topicChecks.Where(x => x.CheckBox.IsChecked == true).Select(x => x.Id).ToList();
        // "Все" means every call, including entries for which MANGO did not provide a
        // directory reference.  Applying the selected identifiers in that state hid
        // such entries and could leave the journal visually empty after a successful import.
        if (_employeeFilter.Without.IsChecked == true) query = query.Where(x => x.EmployeeId == null);
        else if (_employeeFilter.All.IsChecked != true && employeeIds.Count > 0) query = query.Where(x => x.EmployeeId.HasValue && employeeIds.Contains(x.EmployeeId.Value));

        if (_groupFilter.Without.IsChecked == true) query = query.Where(x => x.GroupId == null);
        else if (_groupFilter.All.IsChecked != true && groupIds.Count > 0) query = query.Where(x => x.GroupId.HasValue && groupIds.Contains(x.GroupId.Value));

        if (_withoutTopics.IsChecked == true) query = query.Where(x => x.TopicId == null);
        else if (_allTopics.IsChecked != true && topicIds.Count > 0) query = query.Where(x => x.TopicId.HasValue && topicIds.Contains(x.TopicId.Value));
        var direction = (DirectionComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString(); if (direction == "Входящие") query = query.Where(x => x.IsIncoming); if (direction == "Исходящие") query = query.Where(x => x.IsOutgoing);
        var duration = (DurationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (duration == "До минуты") query = query.Where(x => (x.DurationSeconds ?? 0) < 60);
        if (duration == "1–5 минут") query = query.Where(x => (x.DurationSeconds ?? 0) >= 60 && (x.DurationSeconds ?? 0) < 300);
        if (duration == "От 5 минут") query = query.Where(x => (x.DurationSeconds ?? 0) >= 300);
        var search = SearchTextBox.Text.Trim(); if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => (x.ExternalPhoneNumber ?? "").Contains(search) || (x.Employee != null && x.Employee.FullName.Contains(search)) || (x.Topic != null && x.Topic.Name.Contains(search)));
        var rows = await query.OrderByDescending(x => x.CallDateTime).Select(x => new JournalRow(x.Id, x.CallDateTime, x.Employee == null ? "—" : x.Employee.FullName, x.Group == null ? "—" : x.Group.Name, x.ExternalPhoneNumber ?? "—", x.Topic == null ? "—" : x.Topic.Name, x.IsIncoming ? "Входящий" : x.IsOutgoing ? "Исходящий" : x.Direction, x.DurationSeconds.HasValue ? TimeSpan.FromSeconds(x.DurationSeconds.Value).ToString(@"m\:ss") : "—", x.DurationSeconds ?? 0, false)).ToListAsync();
        _journalRows = rows; DisplayJournalRows(); StatusTextBlock.Text = $"Записей: {rows.Count}";
    }
    private bool TryGetPeriod(out DateTime from, out DateTime to) { from = (FromDatePicker.SelectedDate ?? DateTime.Today).Date; to = (ToDatePicker.SelectedDate ?? from).Date.AddDays(1).AddTicks(-1); return to >= from; }
    private void SetBusy(bool busy) { RefreshButton.IsEnabled = !busy; RefreshButton.Content = busy ? "Обновление…" : "Обновить"; }

    private MultiChoiceFilter ConfigureMultiChoiceFilter(ComboBox source, string allText, string withoutText)
    {
        source.Visibility = Visibility.Collapsed;
        var container = (StackPanel)source.Parent;
        var button = CreateFilterButton(allText);
        container.Children.Add(button);
        var filter = new MultiChoiceFilter { AllText = allText, WithoutText = withoutText, Caption = (TextBlock)button.Content, All = new CheckBox { Content = allText, Margin = new Thickness(4, 3, 4, 6) }, Without = new CheckBox { Content = withoutText, Margin = new Thickness(4, 3, 4, 8) }, ChoicesPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) } };
        var content = new StackPanel { Width = 250, Margin = new Thickness(10) };
        filter.All.Checked += (_, _) => AllChanged(filter); filter.All.Unchecked += (_, _) => AllChanged(filter);
        filter.Without.Checked += (_, _) => WithoutChanged(filter); filter.Without.Unchecked += (_, _) => WithoutChanged(filter);
        content.Children.Add(filter.All); content.Children.Add(filter.Without); content.Children.Add(new Separator()); content.Children.Add(new ScrollViewer { Content = filter.ChoicesPanel, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        filter.Popup = CreatePopup(button, content); button.Click += (_, _) => filter.Popup.IsOpen = !filter.Popup.IsOpen;
        return filter;
    }

    private void ConfigureTopicFilter()
    {
        TopicComboBox.Visibility = Visibility.Collapsed;
        var container = (StackPanel)TopicComboBox.Parent;
        var button = CreateFilterButton("Все тематики"); container.Children.Add(button); _topicCaption = (TextBlock)button.Content;
        _allTopics = new CheckBox { Content = "Все тематики", Margin = new Thickness(4, 3, 4, 6) }; _withoutTopics = new CheckBox { Content = "Без тематик", Margin = new Thickness(4, 3, 4, 8) }; _topicChoices = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        _allTopics.Checked += (_, _) => AllTopicsChanged(); _allTopics.Unchecked += (_, _) => AllTopicsChanged(); _withoutTopics.Checked += (_, _) => WithoutTopicsChanged(); _withoutTopics.Unchecked += (_, _) => WithoutTopicsChanged();
        var content = new StackPanel { Width = 250, Margin = new Thickness(10) }; content.Children.Add(_allTopics); content.Children.Add(_withoutTopics); content.Children.Add(new Separator()); content.Children.Add(new ScrollViewer { Content = _topicChoices, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        _topicPopup = CreatePopup(button, content); button.Click += (_, _) => _topicPopup.IsOpen = !_topicPopup.IsOpen;
    }

    private static Button CreateFilterButton(string caption) => new() { Width = 180, Height = 34, Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(201, 215, 234)), BorderThickness = new Thickness(1), Padding = new Thickness(8, 0, 8, 0), HorizontalContentAlignment = HorizontalAlignment.Left, Content = new TextBlock { Text = caption, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), VerticalAlignment = VerticalAlignment.Center } };
    private static Popup CreatePopup(Button button, UIElement content) => new() { PlacementTarget = button, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(201, 215, 234)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), Child = content } };

    private void ConfigureChoices(MultiChoiceFilter filter, IEnumerable<FilterOption> options)
    {
        _suppressChoices = true; filter.ChoicesPanel.Children.Clear(); filter.Choices.Clear();
        foreach (var optionGroup in options.GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            var check = new CheckBox { Content = optionGroup.Key, Margin = new Thickness(4, 3, 4, 3) };
            check.Checked += (_, _) => ChoiceChanged(filter);
            check.Unchecked += (_, _) => ChoiceChanged(filter);
            filter.ChoicesPanel.Children.Add(check);
            foreach (var option in optionGroup)
                filter.Choices.Add((option.Id, check));
        }
        _suppressChoices = false; SelectAll(filter);
    }
    private void SelectAll(MultiChoiceFilter filter) { _suppressChoices = true; filter.Without.IsChecked = false; filter.All.IsChecked = true; foreach (var (_, check) in filter.Choices) check.IsChecked = true; _suppressChoices = false; UpdateCaption(filter); }
    private void AllChanged(MultiChoiceFilter filter) { if (_suppressChoices) return; _suppressChoices = true; filter.Without.IsChecked = false; foreach (var (_, check) in filter.Choices) check.IsChecked = filter.All.IsChecked == true; _suppressChoices = false; UpdateCaption(filter); if (ReferenceEquals(filter, _groupFilter) && _ready) _ = ConfigureEmployeesForSelectedGroupsAsync(); }
    private void WithoutChanged(MultiChoiceFilter filter) { if (_suppressChoices) return; _suppressChoices = true; if (filter.Without.IsChecked == true) { filter.All.IsChecked = false; foreach (var (_, check) in filter.Choices) check.IsChecked = false; } _suppressChoices = false; UpdateCaption(filter); if (ReferenceEquals(filter, _groupFilter) && _ready) _ = ConfigureEmployeesForSelectedGroupsAsync(); }
    private void ChoiceChanged(MultiChoiceFilter filter) { if (_suppressChoices) return; _suppressChoices = true; filter.Without.IsChecked = false; filter.All.IsChecked = filter.Choices.Count > 0 && filter.Choices.All(x => x.CheckBox.IsChecked == true); _suppressChoices = false; UpdateCaption(filter); if (ReferenceEquals(filter, _groupFilter) && _ready) _ = ConfigureEmployeesForSelectedGroupsAsync(); }
    private static List<int> SelectedIds(MultiChoiceFilter filter) => filter.Choices.Where(x => x.CheckBox.IsChecked == true).Select(x => x.Id).ToList();

    private async Task ConfigureEmployeesForSelectedGroupsAsync()
    {
        var employees = await _db.CallCenterEmployees.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName).Select(x => new FilterOption(x.Id, x.FullName)).ToListAsync();
        var selectedGroupIds = SelectedIds(_groupFilter);
        var groupFilterIsActive = _groupFilter.All.IsChecked != true && _groupFilter.Without.IsChecked != true;
        if (groupFilterIsActive && selectedGroupIds.Count > 0)
        {
            var isCallCenterSelected = await _db.CallCenterGroups.AsNoTracking().AnyAsync(x => selectedGroupIds.Contains(x.Id) && x.Name == "Коллцентр");
            if (isCallCenterSelected)
                employees = employees.Where(x => x.Name.StartsWith("КЦ ", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Name, "Зоя Ершова", StringComparison.OrdinalIgnoreCase)).ToList();
        }
        ConfigureChoices(_employeeFilter, employees);
    }
    private static void UpdateCaption(MultiChoiceFilter filter) { if (filter.Without.IsChecked == true) { filter.Caption.Text = filter.WithoutText; return; } var checks = filter.Choices.Select(x => x.CheckBox).Distinct().ToList(); var count = checks.Count(x => x.IsChecked == true); filter.Caption.Text = count == 0 ? "Выберите значения" : count == checks.Count ? filter.AllText : $"Выбрано: {count}"; }

    private void ConfigureTopicChoices(IEnumerable<FilterOption> topics)
    {
        _suppressChoices = true; _topicChoices.Children.Clear(); _topicChecks.Clear();
        foreach (var item in topics.GroupBy(x => CallCenterTopicCatalog.GetDisplayName(x.Name), StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key)) { var check = new CheckBox { Content = item.Key, Margin = new Thickness(4, 3, 4, 3) }; check.Checked += (_, _) => TopicChoiceChanged(); check.Unchecked += (_, _) => TopicChoiceChanged(); _topicChoices.Children.Add(check); foreach (var entry in item) _topicChecks.Add((entry.Id, check)); }
        _suppressChoices = false; SelectAllTopics();
    }
    private void SelectAllTopics() { _suppressChoices = true; _withoutTopics.IsChecked = false; _allTopics.IsChecked = true; foreach (var (_, check) in _topicChecks) check.IsChecked = true; _suppressChoices = false; UpdateTopicCaption(); }
    private void AllTopicsChanged() { if (_suppressChoices) return; _suppressChoices = true; _withoutTopics.IsChecked = false; foreach (var (_, check) in _topicChecks) check.IsChecked = _allTopics.IsChecked == true; _suppressChoices = false; UpdateTopicCaption(); }
    private void WithoutTopicsChanged() { if (_suppressChoices) return; _suppressChoices = true; if (_withoutTopics.IsChecked == true) { _allTopics.IsChecked = false; foreach (var (_, check) in _topicChecks) check.IsChecked = false; } _suppressChoices = false; UpdateTopicCaption(); }
    private void TopicChoiceChanged() { if (_suppressChoices) return; _suppressChoices = true; _withoutTopics.IsChecked = false; _allTopics.IsChecked = _topicChecks.Count > 0 && _topicChecks.All(x => x.CheckBox.IsChecked == true); _suppressChoices = false; UpdateTopicCaption(); }
    private void UpdateTopicCaption() { if (_withoutTopics.IsChecked == true) { _topicCaption.Text = "Без тематик"; return; } var count = _topicChecks.Select(x => x.CheckBox).Distinct().Count(x => x.IsChecked == true); var total = _topicChecks.Select(x => x.CheckBox).Distinct().Count(); _topicCaption.Text = count == 0 ? "Выберите тематики" : count == total ? "Все тематики" : $"Тематик выбрано: {count}"; }
    private sealed record FilterOption(int Id, string Name);
    private sealed class MultiChoiceFilter { public string AllText { get; init; } = string.Empty; public string WithoutText { get; init; } = string.Empty; public TextBlock Caption { get; init; } = null!; public CheckBox All { get; init; } = null!; public CheckBox Without { get; init; } = null!; public StackPanel ChoicesPanel { get; init; } = null!; public Popup Popup { get; set; } = null!; public List<(int Id, CheckBox CheckBox)> Choices { get; } = []; }
    private async void CallsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (CallsDataGrid.SelectedItem is not JournalRow row || row.IsTotal) return;
        var api = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load());
        var window = new CallDetailsWindow(_db, api) { Owner = Window.GetWindow(this) };
        await window.LoadAsync(row.Id);
        window.ShowDialog();
    }

    private void CallsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        var member = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(member) && e.Column is DataGridBoundColumn bound && bound.Binding is System.Windows.Data.Binding binding) member = binding.Path?.Path;
        if (string.IsNullOrWhiteSpace(member)) return;
        e.Handled = true;
        _sortDirection = _sortMember == member && _sortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        _sortMember = member;
        DisplayJournalRows();
    }

    private void DisplayJournalRows()
    {
        IEnumerable<JournalRow> rows = _journalRows;
        if (_sortMember != null)
        {
            var property = typeof(JournalRow).GetProperty(_sortMember);
            if (property != null) rows = _sortDirection == ListSortDirection.Ascending ? rows.OrderBy(x => property.GetValue(x)) : rows.OrderByDescending(x => property.GetValue(x));
        }
        var result = rows.ToList();
        result.Add(new JournalRow(0, DateTime.MinValue, $"Всего: {_journalRows.Count:N0}", "—", "—", $"С тематикой: {_journalRows.Count(x => x.TopicName != "—"):N0}", "—", _journalRows.Sum(x => x.DurationSeconds) > 0 ? TimeSpan.FromSeconds(_journalRows.Sum(x => x.DurationSeconds)).ToString(@"h\:mm\:ss") : "—", 0, true));
        CallsDataGrid.ItemsSource = result;
    }

    private sealed record JournalRow(int Id, DateTime CallDateTime, string EmployeeName, string GroupName, string PhoneNumber, string TopicName, string Direction, string Duration, int DurationSeconds, bool IsTotal = false);
}
