using ClinicStatisticsApp.Services;
using ClinicStatisticsApp.Data;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI.Views;

public partial class ImplantFunnelPage : UserControl
{
    private readonly ImplantFunnelService _service = new();
    private CancellationTokenSource? _phoneImportCancellation;

    public ImplantFunnelPage()
    {
        InitializeComponent();
        FromDatePicker.SelectedDate = DateTime.Today.AddMonths(-6);
        ToDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadDashboardAsync();
    }

    private async void ImportLeadsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", Title = "Выберите выгрузку колл-трекинга" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            ImportLeadsButton.IsEnabled = false;
            StatusTextBlock.Text = "Читаем заявки и звонки из файла…";
            var leads = ReadLeads(dialog.FileName);
            var imported = await _service.ImportAsync(System.IO.Path.GetFileName(dialog.FileName), leads);
            FileStatusTextBlock.Text = $"Выбран файл: {System.IO.Path.GetFileName(dialog.FileName)} · найдено лидов: {leads.Count:N0} · добавлено новых: {imported:N0}.";
            StatusTextBlock.Text = $"Готово: в файле найдено {leads.Count:N0} лидов, добавлено новых {imported:N0}.";
            await LoadDashboardAsync();
        }
        catch (IOException)
        {
            StatusTextBlock.Text = "Не удалось прочитать файл: закройте его в Excel (или другой программе) и повторите загрузку.";
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Не удалось загрузить файл: {ex.Message}"; }
        finally { ImportLeadsButton.IsEnabled = true; }
    }

    private async void UpdateWarehouseButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        try
        {
            UpdateWarehouseButton.IsEnabled = false;
            StatusTextBlock.Text = "Загружаем журнал MedM, записи и оплаты. Firebird используется только для чтения…";
            var events = await _service.ImportMedmEventsAsync(from, to, new Progress<string>(x => StatusTextBlock.Text = x));
            await using var metadataDb = DbContextFactory.Create();
            var sourceIds = await metadataDb.ClinicDataSources.AsNoTracking().Where(x => !x.IsTest).Select(x => x.Id).ToListAsync();
            var result = await new CrmAnalyticsWarehouseService().ImportAsync(from, to.AddDays(90), sourceIds, new Progress<string>(x => StatusTextBlock.Text = x));
            StatusTextBlock.Text = $"Обновлено: событий MedM — {events.Sources.Sum(x => x.Events):N0}, источников записей/оплат — {result.Sources.Count}, ошибок — {events.Sources.Count(x => x.Error is not null) + result.Sources.Count(x => x.Error is not null)}.";
            await LoadDashboardAsync();
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Не удалось обновить медицинские данные: {ex.Message}"; }
        finally { UpdateWarehouseButton.IsEnabled = true; }
    }

    private async void UpdateMangoButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        try
        {
            UpdateMangoButton.IsEnabled = false;
            MangoImportLogTextBlock.Text = string.Empty;
            StatusTextBlock.Text = "MANGO: начинаем загрузку звонков и тематик…";
            var progress = new Progress<string>(message =>
            {
                StatusTextBlock.Text = message;
                MangoImportLogTextBlock.Text += (MangoImportLogTextBlock.Text.Length == 0 ? string.Empty : Environment.NewLine) + message;
            });
            var result = await _service.ImportMangoAsync(from, to, progress);
            MangoStatusTextBlock.Text = $"MANGO обновлён: лидов с тегом — {result.WithTag:N0} из {result.Leads:N0}; запись — {result.Booked:N0}; не запись — {result.NotBooked:N0}; сброс — {result.Dropped:N0}; без тега — {result.Unclassified:N0}.";
            StatusTextBlock.Text = "MANGO: загрузка и сопоставление завершены. Отчёт можно строить из CRM.";
            await LoadDashboardAsync();
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Не удалось обновить MANGO: {ex.Message}";
        }
        finally
        {
            UpdateMangoButton.IsEnabled = true;
        }
    }

    private async void UpdatePhonesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_phoneImportCancellation is not null) return;
        try
        {
            UpdatePhonesButton.IsEnabled = false;
            StopPhonesButton.IsEnabled = true;
            _phoneImportCancellation = new CancellationTokenSource();
            PhoneImportLogTextBlock.Text = "";
            StatusTextBlock.Text = "Обновляем карточки и индекс телефонов. Firebird используется только для чтения…";
            var progress = new Progress<string>(message =>
            {
                StatusTextBlock.Text = message;
                PhoneImportLogTextBlock.Text += (PhoneImportLogTextBlock.Text.Length == 0 ? "" : Environment.NewLine) + message;
            });
            var result = await _service.ImportPatientPhonesAsync(progress, _phoneImportCancellation.Token);
            PhoneIndexStatusTextBlock.Text = $"Индекс телефонов обновлён: карточек — {result.Sources.Sum(x => x.Cards):N0}; источников с ошибками — {result.Sources.Count(x => x.Error is not null)}.";
            var errors = result.Sources
                .Where(x => !string.IsNullOrWhiteSpace(x.Error))
                .Select(x => $"Источник {x.ClinicDataSourceId}: {x.Error}")
                .ToList();
            StatusTextBlock.Text = errors.Count == 0
                ? $"Карточки и телефоны обновлены: карточек — {result.Sources.Sum(x => x.Cards):N0}, ошибок — 0."
                : $"Карточки и телефоны обновлены: карточек — {result.Sources.Sum(x => x.Cards):N0}, ошибок — {errors.Count}. Причины: {string.Join(" | ", errors)}";
            await LoadDashboardAsync();
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Обновление карточек и телефонов остановлено пользователем. Уже завершённые филиалы сохранены в CRM.";
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Не удалось обновить карточки и телефоны: {ex.Message}"; }
        finally
        {
            _phoneImportCancellation?.Dispose();
            _phoneImportCancellation = null;
            UpdatePhonesButton.IsEnabled = true;
            StopPhonesButton.IsEnabled = false;
        }
    }

    private void StopPhonesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_phoneImportCancellation is null) return;
        StopPhonesButton.IsEnabled = false;
        StatusTextBlock.Text = "Запрошена остановка: завершаем текущее безопасное чтение…";
        _phoneImportCancellation.Cancel();
    }

    private async void ShowReportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShowReportButton.IsEnabled = false;
            StatusTextBlock.Text = "Строим отчёт из уже сохранённых данных CRM…";
            if (await LoadDashboardAsync()) StatusTextBlock.Text = "Отчёт построен из сохранённых данных CRM.";
        }
        finally { ShowReportButton.IsEnabled = true; }
    }

    private async void ComparePeriodsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        try
        {
            ComparePeriodsButton.IsEnabled = false;
            StatusTextBlock.Text = "Сравниваем периоды по сохранённым данным CRM…";
            var granularity = ComparisonGranularityComboBox.SelectedIndex == 1
                ? ImplantFunnelComparisonGranularity.Year
                : ImplantFunnelComparisonGranularity.Month;
            var rows = await _service.GetComparisonAsync(from, to, granularity, new Progress<string>(message => StatusTextBlock.Text = message));
            ComparisonFunnelsItemsControl.ItemsSource = rows;
            ComparisonGrid.ItemsSource = rows;
            StatusTextBlock.Text = $"Сравнение готово: периодов — {rows.Count:N0}.";
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Не удалось сравнить периоды: {ex.Message}"; }
        finally { ComparePeriodsButton.IsEnabled = true; }
    }

    private async void SaveBudgetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        if (!decimal.TryParse(BudgetTextBox.Text, NumberStyles.Number, CultureInfo.GetCultureInfo("ru-RU"), out var amount) || amount < 0)
        {
            StatusTextBlock.Text = "Введите бюджет числом, не меньше нуля.";
            return;
        }
        await _service.SaveBudgetAsync(from, to, amount);
        StatusTextBlock.Text = "Бюджет сохранён для выбранного периода.";
        await LoadDashboardAsync();
    }

    private async Task<bool> LoadDashboardAsync()
    {
        if (!TryGetPeriod(out var from, out var to)) return false;
        try
        {
            var dashboard = await _service.GetDashboardAsync(from, to);
            LeadCountText.Text = dashboard.LeadCount.ToString("N0");
            MangoBookedCountText.Text = dashboard.MangoBookedCount.ToString("N0");
            AppointmentCountText.Text = dashboard.AppointmentCount.ToString("N0");
            AttendedCountText.Text = dashboard.NotMarkedNoShowCount.ToString("N0");
            PaymentText.Text = dashboard.PaymentTotal.ToString("N0");
            CostLeadText.Text = dashboard.CostPerLead.ToString("N0");
            CostVisitText.Text = dashboard.CostPerVisit.ToString("N0");
            RevenueToBudgetText.Text = !dashboard.BudgetConfigured ? "Бюджет не указан" : dashboard.Budget == 0 ? "—" : dashboard.RevenueToBudgetPercent.ToString("N0") + "%";
            BudgetTextBox.Text = dashboard.Budget.ToString("N0");
            StagesItemsControl.ItemsSource = dashboard.Stages;
            DynamicsItemsControl.ItemsSource = dashboard.Dynamics;
            MangoReconciliationText.Text = $"По Mango: запись — {dashboard.MangoBookedCount:N0}; не запись — {dashboard.MangoNotBookedCount:N0}; сброс — {dashboard.MangoDroppedCount:N0}; без классификации — {dashboard.MangoWithoutTagCount:N0}. Запись есть в Mango, но пока не подтверждена MedM: {dashboard.MangoBookingWithoutMedm:N0}.";
            var branchRows = dashboard.Branches.ToList();
            branchRows.Add(new ImplantFunnelBranchRow(
                "Всего",
                branchRows.Sum(x => x.Appointments),
                branchRows.Sum(x => x.NotMarkedNoShow),
                branchRows.Sum(x => x.PaymentTotal)));
            BranchesGrid.ItemsSource = branchRows;
            FunnelNoteText.Text = $"Неявок: {dashboard.NoShowCount:N0}; снятых записей: {dashboard.CancelledCount:N0}; средний чек: {dashboard.AverageCheck:N0}.";
            return true;
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Данные загружены, но отчёт не удалось построить: {ex.Message}"; return false; }
    }

    private bool TryGetPeriod(out DateTime from, out DateTime to)
    {
        from = FromDatePicker.SelectedDate ?? DateTime.Today.AddMonths(-6);
        to = ToDatePicker.SelectedDate ?? DateTime.Today;
        if (from.Date <= to.Date) return true;
        StatusTextBlock.Text = "Дата начала периода не может быть позже даты окончания.";
        return false;
    }

    private static List<ImplantLeadInput> ReadLeads(string fileName)
    {
        // Excel commonly keeps an editable workbook open. Shared read access
        // allows import in that normal case without ever changing the file.
        using var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var leads = new List<ImplantLeadInput>();
        var forms = workbook.Worksheets.FirstOrDefault(x => x.Name.Equals("Заявки", StringComparison.OrdinalIgnoreCase));
        if (forms is not null)
        {
            foreach (var row in forms.RowsUsed().Skip(1))
            {
                var phone = row.Cell(3).GetFormattedString();
                if (ImplantFunnelService.NormalizePhone(phone) is null || !TryReadDate(row.Cell(5), out var occurredAt)) continue;
                leads.Add(new($"form:{row.Cell(4).GetFormattedString()}:{row.RowNumber()}", "Заявка", occurredAt, phone, null, $"{row.Cell(2).GetFormattedString()} · {row.Cell(8).GetFormattedString()}"));
            }
        }
        var calls = workbook.Worksheets.FirstOrDefault(x => x.Name.Equals("Звонки", StringComparison.OrdinalIgnoreCase));
        if (calls is not null)
        {
            foreach (var row in calls.RowsUsed().Skip(3))
            {
                var operatorName = row.Cell(5).GetFormattedString();
                var phone = row.Cell(3).GetFormattedString();
                if (!operatorName.Contains("коллцентр", StringComparison.OrdinalIgnoreCase) || ImplantFunnelService.NormalizePhone(phone) is null || !TryReadDate(row.Cell(1), out var occurredAt)) continue;
                leads.Add(new($"call:{occurredAt:O}:{phone}:{operatorName}", "Звонок", occurredAt, phone, operatorName, $"Длительность: {row.Cell(6).GetFormattedString()} сек."));
            }
        }
        return leads;
    }

    private static bool TryReadDate(IXLCell cell, out DateTime value)
    {
        if (cell.TryGetValue<DateTime>(out value)) return true;
        return DateTime.TryParse(cell.GetFormattedString(), CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.AllowWhiteSpaces, out value);
    }
}
