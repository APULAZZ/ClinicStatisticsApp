using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.CallCenter.Services;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public partial class CallCenterAnnualSummaryPage : UserControl
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly MangoCallImportService _import;
    private bool _loaded;
    private bool _loading;

    public CallCenterAnnualSummaryPage()
    {
        InitializeComponent();
        _import = new MangoCallImportService(_db, new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load()));
        YearComboBox.ItemsSource = Enumerable.Range(DateTime.Today.Year - 2, 5).ToList(); YearComboBox.SelectedItem = DateTime.Today.Year;
        MonthComboBox.ItemsSource = Enumerable.Range(1, 12).Select(x => new MonthOption(x, new DateTime(2000, x, 1).ToString("MMMM"))).ToList(); MonthComboBox.DisplayMemberPath = nameof(MonthOption.Name); MonthComboBox.SelectedValuePath = nameof(MonthOption.Number); MonthComboBox.SelectedValue = DateTime.Today.Month;
        Unloaded += (_, _) => _db.Dispose();
    }

    public async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try { _loaded = true; await LoadDataAsync(); }
        finally { _loading = false; }
    }
    private async void PeriodChanged(object sender, SelectionChangedEventArgs e) { if (_loaded) await LoadDataAsync(); }
    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        var start = new DateTime(Year, Month, 1); if (start > DateTime.Today) { await LoadDataAsync(); return; }
        try { using var busy = App.Busy.Begin("Синхронизация выбранного месяца Mango и обновление сводных…"); await _import.EnsurePeriodImportedAsync(start, Min(start.AddMonths(1), DateTime.Today.AddDays(1))); await LoadDataAsync(); }
        catch (Exception ex) { MessageBox.Show($"Не удалось обновить сводные данные.\n\n{ex.Message}", "Ошибка синхронизации", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private int Year => YearComboBox.SelectedItem is int year ? year : DateTime.Today.Year;
    private int Month => MonthComboBox.SelectedValue is int month ? month : DateTime.Today.Month;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private async Task LoadDataAsync()
    {
        var from = new DateTime(Year, 1, 1); var to = from.AddYears(1);
        var calls = await _db.CallCenterCallRecords.AsNoTracking().Include(x => x.Employee).Include(x => x.Topic).Where(x => x.CallDateTime >= from && x.CallDateTime < to).ToListAsync();
        calls = calls.Where(x => x.Employee is not null && IsCallCenterEmployee(x.Employee.FullName)).ToList();
        var summary = Enumerable.Range(1, 12).Select(month => BuildSummary(month, calls.Where(x => x.CallDateTime.Month == month).ToList())).ToList();
        AnnualGrid.ItemsSource = summary;
        var current = calls.Where(x => x.CallDateTime.Month == Month).ToList();
        var employees = calls.Select(x => x.Employee!.FullName).Distinct().OrderBy(x => x).ToList();
        EmployeeGrid.ItemsSource = employees.Select(name => BuildEmployee(name, current.Where(x => x.Employee!.FullName == name).ToList())).ToList();
        KefGrid.ItemsSource = employees.Select(name => new KefRow(name, summary.Select(x => x.ForEmployee(name, calls)).ToArray())).ToList();
        AttendanceGrid.ItemsSource = employees.Select(name => new ManualScoreRow(name)).ToList();
        PhoneScoreGrid.ItemsSource = employees.Select(name => new ManualScoreRow(name)).ToList();
        ConfigureColumns();
    }

    private void ConfigureColumns()
    {
        if (AnnualGrid.Columns.Count == 0) AddColumns(AnnualGrid, ("Месяц", "Month"), ("Принятые входящие", "Incoming"), ("ПЕРК + ПЛАН", "Booked"), ("ПЕРК", "Perk"), ("ПЛАН", "Plan"), ("Пропущенные", "Missed"), ("Перевод", "Transfers"), ("Сброс", "Drops"));
        if (EmployeeGrid.Columns.Count == 0) AddColumns(EmployeeGrid, ("Сотрудник", "EmployeeName"), ("Принятые", "Incoming"), ("ПЕРК", "Perk"), ("ПЛАН", "Plan"), ("Всего записано", "Booked"), ("Перевод", "Transfers"), ("Сброс", "Drops"), ("КЭФ, %", "Kef"));
        if (KefGrid.Columns.Count == 0) AddColumns(KefGrid, ("Сотрудник", "EmployeeName"), ("Средний КЭФ, %", "Average"));
        if (AttendanceGrid.Columns.Count == 0) AddColumns(AttendanceGrid, ("Сотрудник", "EmployeeName"), ("Итого", "Total"));
        if (PhoneScoreGrid.Columns.Count == 0) AddColumns(PhoneScoreGrid, ("Сотрудник", "EmployeeName"), ("Итого", "Total"));
    }

    private static void AddColumns(DataGrid grid, params (string Header, string Property)[] columns)
    {
        grid.AutoGenerateColumns = false; grid.Columns.Clear();
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        grid.GridLinesVisibility = DataGridGridLinesVisibility.All;
        grid.HorizontalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        grid.VerticalGridLinesBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
        grid.CanUserSortColumns = false; grid.RowHeaderWidth = 0; grid.Background = Brushes.White;
        for (var index = 0; index < columns.Length; index++)
        {
            var (header, property) = columns[index];
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = header, Binding = new System.Windows.Data.Binding(property), Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                CellStyle = CreateCellStyle(index, index == 0), HeaderStyle = CreateHeaderStyle(index)
            });
        }
    }

    private static Style CreateHeaderStyle(int index)
    {
        var style = new Style(typeof(DataGridColumnHeader));
        style.Setters.Add(new Setter(Control.BackgroundProperty, BrushFor(index, true)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"))));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(.5)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 5, 4, 5)));
        return style;
    }

    private static Style CreateCellStyle(int index, bool alignLeft)
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(Control.BackgroundProperty, BrushFor(index, false)));
        style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"))));
        style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(.5)));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 3, 4, 3)));
        style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, alignLeft ? HorizontalAlignment.Left : HorizontalAlignment.Center));
        return style;
    }

    private static Brush BrushFor(int index, bool header)
    {
        var color = index == 0 ? (header ? "#D5E5F3" : "#F4F8FC")
            : index % 2 == 0 ? (header ? "#DCEAF7" : "#EAF1F8") : (header ? "#C7DCEF" : "#DCEAF7");
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
    private static SummaryRow BuildSummary(int month, IReadOnlyList<CallCenterCallRecord> calls) => new(new DateTime(2000, month, 1).ToString("MMMM"), calls.Count(x => x.IsIncoming && x.IsAnswered), calls.Count(x => Kind(x, CallCenterTopicKind.Perk)), calls.Count(x => Kind(x, CallCenterTopicKind.Plan)), calls.Count(x => x.IsMissedIncoming), calls.Count(IsTransfer), calls.Count(x => Kind(x, CallCenterTopicKind.Drop)));
    private static EmployeeRow BuildEmployee(string name, IReadOnlyList<CallCenterCallRecord> calls) { var incoming = calls.Count(x => x.IsIncoming && x.IsAnswered); var perk = calls.Count(x => Kind(x, CallCenterTopicKind.Perk)); var plan = calls.Count(x => Kind(x, CallCenterTopicKind.Plan)); var transfers = calls.Count(IsTransfer); var drops = calls.Count(x => Kind(x, CallCenterTopicKind.Drop)); return new(name, incoming, perk, plan, transfers, drops); }
    private static bool Kind(CallCenterCallRecord call, CallCenterTopicKind kind) => call.Topic is not null && CallCenterTopicCatalog.GetKind(call.Topic.Name) == kind;
    private static bool IsTransfer(CallCenterCallRecord call) => call.Topic is not null && CallCenterTopicCatalog.IsTransferTopic(call.Topic.Name);
    private static bool IsCallCenterEmployee(string name) => name.StartsWith("КЦ ", StringComparison.OrdinalIgnoreCase) || name.Equals("Зоя Ершова", StringComparison.OrdinalIgnoreCase);
    private sealed record MonthOption(int Number, string Name);
    private sealed record SummaryRow(string Month, int Incoming, int Perk, int Plan, int Missed, int Transfers, int Drops) { public int Booked => Perk + Plan; public double ForEmployee(string name, IReadOnlyList<CallCenterCallRecord> all) { var c = all.Where(x => x.Employee!.FullName == name && x.CallDateTime.Month == DateTime.Parse($"1 {Month} 2000").Month).ToList(); return BuildEmployee(name, c).Kef; } }
    private sealed record EmployeeRow(string EmployeeName, int Incoming, int Perk, int Plan, int Transfers, int Drops) { public int Booked => Perk + Plan; public double Kef => Incoming - Transfers - Drops <= 0 ? 0 : Math.Round(100d * Booked / (Incoming - Transfers - Drops), 2); }
    private sealed record KefRow(string EmployeeName, double[] Values) { public double Average => Math.Round(Values.Where(x => x > 0).DefaultIfEmpty().Average(), 2); }
    private sealed record ManualScoreRow(string EmployeeName) { public double Total { get; set; } }
}
