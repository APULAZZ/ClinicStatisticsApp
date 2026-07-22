using ClinicStatisticsApp.CallCenter.Services;
using ClinicStatisticsApp.Data;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI.Views;

public partial class CallCenterImportPage : UserControl
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly MangoSynchronizationService _synchronization;

    public CallCenterImportPage()
    {
        InitializeComponent();
        var api = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load());
        _synchronization = new MangoSynchronizationService(new MangoDirectorySyncService(_db, api), new MangoCallImportService(_db, api));
        FromDatePicker.SelectedDate = DateTime.Today.AddDays(-6);
        ToDatePicker.SelectedDate = DateTime.Today;
        Unloaded += (_, _) => _db.Dispose();
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (FromDatePicker.SelectedDate is not DateTime from || ToDatePicker.SelectedDate is not DateTime to || to < from)
            return;

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var progress = new Progress<string>(message =>
        {
            StatusTextBlock.Text = message;
            App.Busy.Report(message);
        });

        try
        {
            using var busy = App.Busy.Begin("Подготавливаем синхронизацию с Mango…");
            RunButton.IsEnabled = false;
            StatusTextBlock.Text = "Подготавливаем синхронизацию…";
            StatusTextBlock.Text = await _synchronization.SynchronizeAsync(
                from.Date,
                to.Date.AddDays(1).AddSeconds(-1),
                SyncEmployeesCheckBox.IsChecked == true,
                SyncTopicsCheckBox.IsChecked == true,
                progress,
                deadline.Token);
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            const string message = "Синхронизация остановлена: Mango не ответил за 5 минут. Попробуйте меньший период.";
            StatusTextBlock.Text = message;
            MessageBox.Show(message, "Ошибка синхронизации", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = "Синхронизация не завершена.";
            MessageBox.Show(ex.Message, "Ошибка синхронизации", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            RunButton.IsEnabled = true;
        }
    }
}
