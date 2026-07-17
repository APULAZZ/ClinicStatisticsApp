using ClinicStatisticsApp.CallCenter.Services;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI.Views;

public partial class CallCenterSettingsPage : UserControl
{
    private readonly MangoApiOptions _options = MangoApiOptionsLoader.Load();
    private readonly IMangoApiClient _api;
    public CallCenterSettingsPage()
    {
        InitializeComponent(); _api = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, _options); EndpointTextBlock.Text = $"Адрес: {_options.BaseUrl}"; CredentialsTextBlock.Text = string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ApiSalt) ? "Ключи API не настроены." : "Ключи API настроены.";
    }
    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        try { using var busy = App.Busy.Begin("Проверяем подключение к Mango…"); TestButton.IsEnabled = false; StatusTextBlock.Text = "Проверяем подключение…"; var users = await _api.GetUsersAsync(); StatusTextBlock.Text = $"Подключение работает. Получено сотрудников: {users.Count}."; }
        catch (Exception ex) { StatusTextBlock.Text = $"Не удалось подключиться: {ex.Message}"; }
        finally { TestButton.IsEnabled = true; }
    }
}
