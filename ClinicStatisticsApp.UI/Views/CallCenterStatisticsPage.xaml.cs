using ClinicStatisticsApp.CallCenter.Services;
using ClinicStatisticsApp.Data;
using System.ComponentModel;
using System;
using System.Net.Http;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI.Views;

public partial class CallCenterStatisticsPage : UserControl
{
    private readonly bool _isGroupStatistics;
    private readonly AppDbContext _db;
    private readonly MangoDirectorySyncService _directorySyncService;
    private readonly MangoCallImportService _callImportService;
    private readonly CallCenterStatisticsService _statisticsService;
    private List<CallCenterEmployeeStatRow> _employeeRows = [];
    private string? _sortMember;
    private ListSortDirection _sortDirection;

    public CallCenterStatisticsPage(bool isGroupStatistics)
    {
        InitializeComponent();
        foreach (var column in StatisticsDataGrid.Columns) column.Width = DataGridLength.SizeToHeader;
        if (StatisticsDataGrid.Columns.Count > 0) StatisticsDataGrid.Columns[0].Width = DataGridLength.SizeToCells;
        _isGroupStatistics = isGroupStatistics;
        _db = DbContextFactory.Create();
        var api = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromSeconds(100) }, MangoApiOptionsLoader.Load());
        _directorySyncService = new MangoDirectorySyncService(_db, api);
        _callImportService = new MangoCallImportService(_db, api);
        _statisticsService = new CallCenterStatisticsService(_db);

        if (_isGroupStatistics)
            ConfigureGroupColumns();

        TitleTextBlock.Text = isGroupStatistics ? "Статистика групп" : "Статистика сотрудников";
        DescriptionTextBlock.Text = "Показатели рассчитываются по журналу звонков и тематикам Mango за выбранный период.";
        FromDatePicker.SelectedDate = DateTime.Today;
        ToDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadStatisticsAsync();
        Unloaded += (_, _) => _db.Dispose();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var from = (FromDatePicker.SelectedDate ?? DateTime.Today).Date;
        var to = (ToDatePicker.SelectedDate ?? from).Date.AddDays(1).AddTicks(-1);
        if (to < from) { MessageBox.Show("Дата окончания не может быть раньше даты начала.", "Период", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        try
        {
            SetBusy(true, "Синхронизация с Mango...");
            using var operation = App.Busy.Begin("Синхронизация звонков с Mango…");
            await _callImportService.EnsurePeriodImportedAsync(from, to);
            await LoadStatisticsAsync();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Ошибка синхронизации";
            MessageBox.Show($"Не удалось обновить данные из Mango.\n\n{ex.Message}", "Ошибка синхронизации", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { SetBusy(false, StatusTextBlock.Text); }
    }

    private async Task LoadStatisticsAsync()
    {
        var from = (FromDatePicker.SelectedDate ?? DateTime.Today).Date;
        var to = (ToDatePicker.SelectedDate ?? from).Date.AddDays(1).AddTicks(-1);
        if (_isGroupStatistics)
        {
            var rows = await _statisticsService.GetGroupStatsAsync(from, to);
            StatisticsDataGrid.ItemsSource = rows;
            StatusTextBlock.Text = $"Групп в отчёте: {rows.Count}";
        }
        else
        {
            var rows = await _statisticsService.GetEmployeeStatsAsync(from, to);
            _employeeRows = rows;
            DisplayEmployeeRows();
            StatusTextBlock.Text = $"Сотрудников в отчёте: {Math.Max(0, rows.Count - 1)}";
        }
    }

    private void StatisticsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (_isGroupStatistics)
            return;

        var sortMember = e.Column.SortMemberPath;
        if (string.IsNullOrWhiteSpace(sortMember) && e.Column is DataGridBoundColumn boundColumn && boundColumn.Binding is Binding binding)
            sortMember = binding.Path?.Path;
        if (string.IsNullOrWhiteSpace(sortMember))
            return;

        e.Handled = true;
        _sortDirection = string.Equals(_sortMember, sortMember, StringComparison.Ordinal)
            && _sortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        _sortMember = sortMember;

        foreach (var column in StatisticsDataGrid.Columns)
            column.SortDirection = null;
        e.Column.SortDirection = _sortDirection;
        DisplayEmployeeRows();
    }

    private void ConfigureGroupColumns()
    {
        // Statistics by groups is a separate source page in CallCenterStatisticsApp;
        // it deliberately uses the native compact DataGrid instead of the employee-table template.
        StatisticsDataGrid.ClearValue(DataGrid.CellStyleProperty);
        StatisticsDataGrid.ClearValue(DataGrid.ColumnHeaderStyleProperty);
        StatisticsDataGrid.ClearValue(DataGrid.RowStyleProperty);
        StatisticsDataGrid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        StatisticsDataGrid.RowHeight = 36;
        StatisticsDataGrid.Columns.Clear();
        StatisticsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Группа", Binding = new Binding(nameof(CallCenterGroupStatRow.GroupName)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        StatisticsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Входящие", Binding = new Binding(nameof(CallCenterGroupStatRow.IncomingCount)), Width = 130 });
        StatisticsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Пропущенные", Binding = new Binding(nameof(CallCenterGroupStatRow.MissedCount)), Width = 145 });
        StatisticsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Исходящие", Binding = new Binding(nameof(CallCenterGroupStatRow.OutgoingCount)), Width = 130 });
        StatisticsDataGrid.Columns.Add(new DataGridTextColumn { Header = "Без ответа", Binding = new Binding(nameof(CallCenterGroupStatRow.OutgoingNoAnswerCount)), Width = 130 });
    }

    private void DisplayEmployeeRows()
    {
        var total = _employeeRows.FirstOrDefault(x => x.IsTotal);
        IEnumerable<CallCenterEmployeeStatRow> rows = _employeeRows.Where(x => !x.IsTotal);

        if (!string.IsNullOrWhiteSpace(_sortMember))
        {
            var property = typeof(CallCenterEmployeeStatRow).GetProperty(_sortMember);
            if (property is not null)
            {
                rows = _sortDirection == ListSortDirection.Ascending
                    ? rows.OrderBy(x => property.GetValue(x))
                    : rows.OrderByDescending(x => property.GetValue(x));
            }
        }

        StatisticsDataGrid.ItemsSource = total is null ? rows.ToList() : rows.Append(total).ToList();
    }

    private void SetBusy(bool isBusy, string status)
    {
        RefreshButton.IsEnabled = !isBusy;
        FromDatePicker.IsEnabled = !isBusy;
        ToDatePicker.IsEnabled = !isBusy;
        RefreshButton.Content = isBusy ? "Обновление..." : "Обновить";
        StatusTextBlock.Text = status;
    }
}
