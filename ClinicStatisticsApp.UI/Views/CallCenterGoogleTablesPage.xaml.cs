using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.CallCenter.Services;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public partial class CallCenterGoogleTablesPage : UserControl
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly MangoCallImportService _import;
    private readonly CallCenterAnnualSummaryPage _annualSummaryPage;
    private bool _loaded;
    private bool _loading;
    private List<ManualEntry> _manualEntries = [];

    public CallCenterGoogleTablesPage()
    {
        InitializeComponent();
        _annualSummaryPage = new CallCenterAnnualSummaryPage();
        AnnualSummaryHost.Content = _annualSummaryPage;
        _import = new MangoCallImportService(_db, new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load()));
        MonthComboBox.ItemsSource = Enumerable.Range(1, 12).Select(x => new MonthOption(x, new DateTime(2000, x, 1).ToString("MMMM"))).ToList();
        MonthComboBox.DisplayMemberPath = nameof(MonthOption.Name); MonthComboBox.SelectedValuePath = nameof(MonthOption.Number);
        YearComboBox.ItemsSource = Enumerable.Range(DateTime.Today.Year - 2, 5).ToList();
        MonthComboBox.SelectedValue = DateTime.Today.Month; YearComboBox.SelectedItem = DateTime.Today.Year;
        Unloaded += (_, _) => _db.Dispose();
    }

    public async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try { await LoadCoreAsync(); }
        finally { _loading = false; }
    }

    private async Task LoadCoreAsync()
    {
        if (!_loaded)
        {
            var employees = await _db.CallCenterEmployees.AsNoTracking().Where(x => x.IsActive).Select(x => x.FullName).ToListAsync();
            employees = employees.Where(IsCallCenterEmployee).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
            employees.Insert(0, "Все сотрудники");
            EmployeeComboBox.ItemsSource = employees; EmployeeComboBox.SelectedIndex = 0;
            ManualEmployeeComboBox.ItemsSource = employees.Skip(1).ToList();
            ManualTypeComboBox.ItemsSource = new[] { "График админов КЦ", "Норма часов", "Отпуск", "Отгул" };
            ManualTypeComboBox.SelectedIndex = 0; ManualDatePicker.SelectedDate = DateTime.Today;
            var setting = await _db.CallCenterSettings.FirstOrDefaultAsync(x => x.Key == "ManualCallCenterTables");
            _manualEntries = setting is null ? [] : JsonSerializer.Deserialize<List<ManualEntry>>(setting.Value) ?? [];
            BuildManualTable(); _loaded = true;
        }
        await LoadRowsAsync();
        await _annualSummaryPage.LoadAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetMonth(out var from, out var to)) return;
        try
        {
            using var operation = App.Busy.Begin("Синхронизация звонков и расчёт рабочих таблиц…");
            await _import.EnsurePeriodImportedAsync(from, to);
            await LoadRowsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось обновить данные из Mango.\n\n{ex.Message}", "Ошибка синхронизации", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void AddManualEntryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ManualDatePicker.SelectedDate is not DateTime date || ManualTypeComboBox.SelectedItem is not string type || ManualEmployeeComboBox.SelectedItem is not string employee) return;
        _manualEntries.Add(new ManualEntry { Date = date.Date, Type = type, EmployeeName = employee, Value = ManualValueTextBox.Text.Trim() });
        var setting = await _db.CallCenterSettings.FirstOrDefaultAsync(x => x.Key == "ManualCallCenterTables");
        if (setting is null) { setting = new CallCenterSetting { Key = "ManualCallCenterTables" }; _db.CallCenterSettings.Add(setting); }
        setting.Value = JsonSerializer.Serialize(_manualEntries); await _db.SaveChangesAsync();
        ManualValueTextBox.Clear(); BuildManualTable();
    }

    private async void PeriodChanged(object sender, SelectionChangedEventArgs e) { if (_loaded) await LoadRowsAsync(); }

    private async Task LoadRowsAsync()
    {
        if (!TryGetMonth(out var from, out var to)) return;
        var selectedEmployee = EmployeeComboBox.SelectedItem as string ?? "Все сотрудники";
        var calls = await _db.CallCenterCallRecords.AsNoTracking().Include(x => x.Employee).Include(x => x.Topic)
            .Where(x => x.CallDateTime >= from && x.CallDateTime < to && x.EmployeeId.HasValue).ToListAsync();
        var filtered = calls.Where(x => x.Employee is not null && IsCallCenterEmployee(x.Employee.FullName) &&
            (selectedEmployee == "Все сотрудники" || string.Equals(selectedEmployee, x.Employee.FullName, StringComparison.OrdinalIgnoreCase))).ToList();
        var rows = filtered.GroupBy(x => new { Date = x.CallDateTime.Date, Name = x.Employee!.FullName })
            .Select(g => new DailyRow(g.Key.Date, g.Key.Name, g.Count(x => x.IsIncoming && x.IsAnswered), g.Count(x => x.IsOutgoing),
                g.Count(x => IsKind(x, CallCenterTopicKind.Perk)), g.Count(x => IsKind(x, CallCenterTopicKind.Plan)),
                g.Count(x => IsKind(x, CallCenterTopicKind.NoAppointment)), g.Count(x => IsKind(x, CallCenterTopicKind.Drop)),
                g.Count(x => IsTransfer(x)))).OrderBy(x => x.Date).ThenBy(x => x.EmployeeName).ToList();
        BuildDailyTable(rows); BuildClinicTable(filtered, from, to);
    }

    private void BuildDailyTable(IReadOnlyList<DailyRow> rows)
    {
        var columns = new[] { "Дата", "Сотрудник", "Входящие", "Исходящие", "ПЕРК", "ПЛАН", "Незапись", "Сбросы", "Переводы", "% записи", "% незаписи" };
        var values = rows.Select(x => new[] { x.Date.ToString("dd.MM.yyyy"), x.EmployeeName, x.Incoming.ToString(), x.Outgoing.ToString(), x.Perk.ToString(), x.Plan.ToString(), x.NoAppointment.ToString(), x.Drop.ToString(), x.Transfers.ToString(), x.Percent(x.Perk + x.Plan).ToString("N1") + "%", x.Percent(x.NoAppointment).ToString("N1") + "%" });
        BuildGrid(DailyTableGrid, columns, values, new[] { 92d, 150, 78, 78, 62, 62, 78, 70, 78, 82, 92 });
    }

    private void BuildClinicTable(IReadOnlyList<CallCenterCallRecord> calls, DateTime from, DateTime to)
    {
        var clinics = CallCenterTopicCatalog.Clinics;
        var headers = new List<string> { "Дата", "Сотрудник" };
        foreach (var clinic in clinics) { headers.Add($"{clinic}\nПЕРК"); headers.Add($"{clinic}\nПЛАН"); }
        headers.Add("Итого\nПЕРК"); headers.Add("Итого\nПЛАН");
        var data = new List<string[]>();
        var employees = calls.Select(x => x.Employee!.FullName).Distinct().OrderBy(x => x).ToList();
        for (var day = from.Date; day < to; day = day.AddDays(1)) foreach (var employee in employees)
        {
            var employeeCalls = calls.Where(x => x.CallDateTime.Date == day && x.Employee!.FullName == employee).ToList();
            var values = new List<string> { day.ToString("dd.MM.yyyy"), employee }; var totalPerk = 0; var totalPlan = 0;
            foreach (var clinic in clinics)
            {
                var perk = employeeCalls.Count(x => IsKind(x, CallCenterTopicKind.Perk) && Clinic(x) == clinic);
                var plan = employeeCalls.Count(x => IsKind(x, CallCenterTopicKind.Plan) && Clinic(x) == clinic);
                totalPerk += perk; totalPlan += plan; values.Add(perk.ToString()); values.Add(plan.ToString());
            }
            values.Add(totalPerk.ToString()); values.Add(totalPlan.ToString()); data.Add(values.ToArray());
        }
        var widths = new List<double> { 78, 118 }; widths.AddRange(Enumerable.Repeat(62d, clinics.Count * 2)); widths.Add(62); widths.Add(62);
        BuildGrid(ClinicTableGrid, headers, data, widths, true);
    }

    private void BuildManualTable()
    {
        BuildGrid(ManualTableGrid, new[] { "Дата", "Тип", "Сотрудник", "Значение / комментарий" },
            _manualEntries.OrderByDescending(x => x.Date).ThenBy(x => x.EmployeeName).Select(x => new[] { x.Date.ToString("dd.MM.yyyy"), x.Type, x.EmployeeName, x.Value }), new[] { 110d, 180, 190, 280 });
    }

    private static void BuildGrid(Grid grid, IReadOnlyList<string> headers, IEnumerable<string[]> rows, IReadOnlyList<double> widths, bool clinicColors = false)
    {
        grid.Children.Clear(); grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
        for (var i = 0; i < headers.Count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i]) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < headers.Count; i++) AddCell(grid, headers[i], i, 0, i % 2 == 0 ? "#DCEAF7" : "#C7DCEF", true, false);
        var index = 1;
        foreach (var row in rows) { grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); for (var i = 0; i < headers.Count; i++) AddCell(grid, row[i], i, index, i < 2 ? "#F4F8FC" : (i % 4 < 2 ? "#EAF1F8" : "#DCEAF7"), false, i == 1); index++; }
    }

    private static void AddCell(Grid grid, string text, int column, int row, string color, bool header, bool left)
    {
        var border = new Border { BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8")), BorderThickness = new Thickness(.5), Padding = new Thickness(4, 3, 4, 3), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) };
        border.Child = new TextBlock { Text = text, TextAlignment = left && !header ? TextAlignment.Left : TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontWeight = header ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(border, column); Grid.SetRow(border, row); grid.Children.Add(border);
    }

    private bool TryGetMonth(out DateTime from, out DateTime to) { from = default; to = default; if (MonthComboBox.SelectedValue is not int month || YearComboBox.SelectedItem is not int year) return false; from = new DateTime(year, month, 1); to = from.AddMonths(1); return true; }
    private static bool IsCallCenterEmployee(string name) => name.StartsWith("КЦ ", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Зоя Ершова", StringComparison.OrdinalIgnoreCase);
    private static bool IsKind(CallCenterCallRecord call, CallCenterTopicKind kind) => call.Topic is not null && CallCenterTopicCatalog.GetKind(call.Topic.Name) == kind;
    private static bool IsTransfer(CallCenterCallRecord call) => call.Topic is not null && CallCenterTopicCatalog.IsTransferTopic(call.Topic.Name);
    private static string Clinic(CallCenterCallRecord call) => call.Topic is not null && CallCenterTopicCatalog.TryGetClinic(call.Topic.Name, out var clinic) ? clinic : string.Empty;
    private sealed record MonthOption(int Number, string Name);
    private sealed record DailyRow(DateTime Date, string EmployeeName, int Incoming, int Outgoing, int Perk, int Plan, int NoAppointment, int Drop, int Transfers) { public double Percent(int value) => Incoming + Outgoing == 0 ? 0 : 100d * value / (Incoming + Outgoing); }
    private sealed class ManualEntry { public DateTime Date { get; init; } public string Type { get; init; } = string.Empty; public string EmployeeName { get; init; } = string.Empty; public string Value { get; init; } = string.Empty; }
}
