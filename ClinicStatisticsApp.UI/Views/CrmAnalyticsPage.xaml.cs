using System.Windows.Controls;
using System.Windows;
using ClinicStatisticsApp.Services;
using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.UI.Views;

public partial class CrmAnalyticsPage : UserControl
{
    public CrmAnalyticsPage(int selectedTab)
    {
        InitializeComponent();
        AnalyticsTabs.SelectedIndex = selectedTab;
        FromDatePicker.SelectedDate = DateTime.Today.AddYears(-3);
        ToDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadSourcesAsync();
    }

    private async Task LoadSourcesAsync()
    {
        var configured = FirebirdClinicOptionsLoader.Load().Select(x => x.ClinicDataSourceId).ToHashSet();
        await using var db = DbContextFactory.Create();
        var sources = await db.ClinicDataSources.AsNoTracking().Where(x => configured.Contains(x.Id)).OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.IsTest }).ToListAsync();
        var options = new List<AnalyticsSourceOption>();
        options.Add(new AnalyticsSourceOption("Все подключённые источники", null));
        options.AddRange(sources.Select(x => new AnalyticsSourceOption($"{x.Name}{(x.IsTest ? " (тест)" : "")}", [x.Id])));
        SourceComboBox.ItemsSource = options;
        SourceComboBox.SelectedItem = options.FirstOrDefault(x => x.SourceIds?.Count == 1 && sources.Any(source => source.Id == x.SourceIds.First() && source.IsTest))
            ?? options.FirstOrDefault();
        await LoadSummaryAsync();
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (FromDatePicker.SelectedDate is not DateTime from || ToDatePicker.SelectedDate is not DateTime to) return;
        ImportButton.IsEnabled = false;
        ImportButton.Content = "Идёт обновление…";
        StatusTextBlock.Text = "Импортируем данные. Firebird используется только для чтения…";
        try
        {
            var progress = new Progress<string>(message => StatusTextBlock.Text = message);
            var selected = SourceComboBox.SelectedItem as AnalyticsSourceOption;
            var result = await new CrmAnalyticsWarehouseService().ImportAsync(from, to, selected?.SourceIds, progress);
            var failed = result.Sources.Where(x => x.Error is not null).ToList();
            StatusTextBlock.Text = failed.Count == 0
                ? $"Готово: оплат — {result.Sources.Sum(x => x.Payments)}, записей — {result.Sources.Sum(x => x.Appointments)}, источников — {result.Sources.Count}."
                : $"Импорт завершён с ошибками в {failed.Count} источн. Успешно: оплат — {result.Sources.Sum(x => x.Payments)}, записей — {result.Sources.Sum(x => x.Appointments)}.";
            await LoadSummaryAsync();
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Ошибка импорта: {ex.Message}"; }
        finally { ImportButton.Content = "Обновить данные из Firebird"; ImportButton.IsEnabled = true; }
    }
    private async Task LoadSummaryAsync()
    {
        try
        {
            var selected = SourceComboBox.SelectedItem as AnalyticsSourceOption;
            var summary = await new CrmAnalyticsWarehouseService().GetSummaryAsync(selected?.SourceIds);
            var conversion = summary.UniquePatients == 0 ? 0m : 100m * summary.AttendedPatientCount / summary.UniquePatients;
            FunnelSummaryTextBlock.Text = $"Воронка записей: записаны — {summary.UniquePatients:N0} → пришли — {summary.AttendedPatientCount:N0} ({conversion:N1}%) → ближайшая запись — {summary.UpcomingPatientCount:N0}. Неявка — {summary.NoShowPatientCount:N0} пациентов.";
            AppointmentsSummaryTextBlock.Text = $"Записей: {summary.AppointmentCount:N0} · пациентов: {summary.UniquePatients:N0} · неявок: {summary.NoShowCount:N0} · врачей: {summary.DoctorCount:N0} · кабинетов: {summary.RoomCount:N0}";
            FinanceSummaryTextBlock.Text = $"Оплат: {summary.PaymentCount:N0} · оплачено: {summary.TotalPaid:N2} · средний чек: {(summary.PaymentCount == 0 ? 0m : summary.TotalPaid / summary.PaymentCount):N2}";
            RetentionSummaryTextBlock.Text = $"Пациентов без записи более 6 месяцев: {summary.InactivePatientCount:N0}. Очередь для возврата будет формироваться на следующем этапе.";
        }
        catch { FunnelSummaryTextBlock.Text = AppointmentsSummaryTextBlock.Text = FinanceSummaryTextBlock.Text = RetentionSummaryTextBlock.Text = "Данные ещё не импортированы для выбранного источника."; }
    }
    private sealed record AnalyticsSourceOption(string Name, IReadOnlyCollection<int>? SourceIds);
}
