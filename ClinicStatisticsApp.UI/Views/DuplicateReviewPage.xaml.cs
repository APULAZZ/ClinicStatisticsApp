using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClinicStatisticsApp.UI.Views;

public partial class DuplicateReviewPage : UserControl
{
    private readonly CurrentUserInfo _user;
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly PatientDirectoryService _service;

    public DuplicateReviewPage(CurrentUserInfo user, string? initialQuery = null)
    {
        InitializeComponent();
        _user = user;
        _service = new PatientDirectoryService(_db);
        QueryTextBox.Text = initialQuery ?? string.Empty;
        Loaded += async (_, _) => await RefreshAsync();
        Unloaded += (_, _) => _db.Dispose();
    }

    private async Task RefreshAsync()
    {
        using var busy = App.Busy.Begin("Ищем совпадения пациентов…");
        var candidates = await _service.GetPotentialDuplicateGroupsAsync(QueryTextBox.Text);
        CandidatesGrid.ItemsSource = candidates;
        EmptyTextBlock.Visibility = candidates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void QueryTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await RefreshAsync(); }

    private async void AcceptButton_Click(object sender, RoutedEventArgs e)
    {
        if (CandidatesGrid.SelectedItem is not PotentialDuplicateGroupRow group) return;
        if (MessageBox.Show($"Создать единого CRM-пациента и связать все {group.CardCount} карточки?", "Подтвердить", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await _service.AcceptPotentialDuplicateGroupAsync(group, _user.UserId);
            await RefreshAsync();
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(ex.Message, "Объединение дублей", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void RejectButton_Click(object sender, RoutedEventArgs e)
    {
        if (CandidatesGrid.SelectedItem is not PotentialDuplicateGroupRow group) return;
        if (MessageBox.Show($"Исключить из очереди все {group.CardCount} карточки этой группы?", "Подтвердить", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await _service.RejectPotentialDuplicateGroupAsync(group, _user.UserId);
        await RefreshAsync();
    }
}
