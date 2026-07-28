using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using ClinicStatisticsApp.CallCenter.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http;

namespace ClinicStatisticsApp.UI.Views;

public partial class PatientDirectoryPage : UserControl
{
    private readonly CurrentUserInfo _currentUser;
    private readonly AppDbContext _db = DbContextFactory.Create();
    private readonly PatientDirectoryService _service;
    private PatientSearchRow? _selected;
    private PatientCardDetails? _details;
    private CancellationTokenSource? _recentCallsCancellation;

    public PatientDirectoryPage(CurrentUserInfo currentUser, string? initialSearch = null)
    {
        InitializeComponent();
        _currentUser = currentUser; _service = new PatientDirectoryService(_db);
        SearchTextBox.Text = initialSearch ?? string.Empty;
        Loaded += async (_, _) => await LoadBranchesAsync();
        Unloaded += (_, _) => { _recentCallsCancellation?.Cancel(); _db.Dispose(); };
    }

    private async Task LoadBranchesAsync()
    {
        var branches = await _db.Branches.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        branches.Insert(0, new Branch { Id = 0, Name = "Все филиалы" });
        BranchComboBox.ItemsSource = branches; BranchComboBox.SelectedIndex = 0;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        using var busy = App.Busy.Begin("Ищем пациентов…");
        int? branchId = BranchComboBox.SelectedValue is int id && id > 0 ? id : null;
        PatientsGrid.ItemsSource = await _service.SearchAsync(SearchTextBox.Text, branchId);
    }

    private async Task LoadDetailsAsync()
    {
        if (_selected is null) return;
        MergeCrmProfilesButton.IsEnabled = _selected.CrmPersonId is not null;
        _details = await _service.GetCardAsync(_selected.CardId);
        if (_details is null) return;
        var card = _details.Card;
        PatientNameTextBlock.Text = string.Join(" ", new[] { card.LastName, card.FirstName, card.MiddleName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        PatientInfoTextBlock.Text = $"{card.Branch?.Name} · карта {card.SourceCardNumber ?? "—"}\nТелефон: {card.MobilePhone ?? "—"}\nE-mail: {card.Email ?? "—"}\n{(_selected.CrmPersonId is null ? "Единый CRM-пациент ещё не создан" : "Единый CRM-пациент создан")}";
        LinkedCardsGrid.ItemsSource = _details.LinkedCards; CandidatesGrid.ItemsSource = _details.Candidates; TasksItems.ItemsSource = _details.Tasks; ActivityItems.ItemsSource = _details.Activity;
        CreatePersonButton.IsEnabled = _selected.CrmPersonId is null; FindDuplicatesButton.IsEnabled = true; LinkCallsButton.IsEnabled = _selected.CrmPersonId is not null; CreateTaskButton.IsEnabled = _selected.CrmPersonId is not null; AcceptCandidateButton.IsEnabled = _details.Candidates.Count > 0; OpenCallButton.IsEnabled = _details.Activity.Any(x => x.ActivityType == "MangoCall");
        RecentCallsButton.IsEnabled = GetPatientPhones().Count > 0 && _recentCallsCancellation is null;
        RecentCallsGrid.ItemsSource = null;
        RecentCallsStatusTextBlock.Text = GetPatientPhones().Count == 0
            ? "В карточке нет телефона для поиска в MANGO."
            : "Нажмите «Последние звонки»: поиск идёт по всем телефонам объединённой карточки за последние 30 дней.";
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e) => await SearchAsync();
    private async void SearchTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await SearchAsync(); }
    private async void PatientsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _recentCallsCancellation?.Cancel();
        _selected = PatientsGrid.SelectedItem as PatientSearchRow;
        try { await LoadDetailsAsync(); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Карточка пациента", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private async void PatientsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_selected is null) return;
        await LoadDetailsAsync();
        if (_details is not null) new PatientDossierWindow(_details, _selected, _currentUser) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
    private async void ActivityItems_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ActivityItems.SelectedItem is not CrmActivityLink { ActivityType: "MangoCall" } activity || !int.TryParse(activity.ExternalId, out var callId)) return;
        var api = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load());
        var window = new CallDetailsWindow(_db, api) { Owner = Window.GetWindow(this) };
        await window.LoadAsync(callId);
        if (window.IsVisible) window.ShowDialog();
    }
    private async void OpenCallButton_Click(object sender, RoutedEventArgs e)
    {
        var call = ActivityItems.SelectedItem as CrmActivityLink ?? _details?.Activity.FirstOrDefault(x => x.ActivityType == "MangoCall");
        if (call is null || !int.TryParse(call.ExternalId, out var callId)) return;
        var window = new CallDetailsWindow(_db, new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load())) { Owner = Window.GetWindow(this) };
        await window.LoadAsync(callId); if (window.IsVisible) window.ShowDialog();
    }
    private async void CreatePersonButton_Click(object sender, RoutedEventArgs e) { if (_selected is null) return; await _service.EnsureCrmPersonAsync(_selected.CardId, _currentUser.UserId); await SearchAsync(); await LoadDetailsAsync(); }
    private async void MergeCrmProfilesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected?.CrmPersonId is not int personId) return;
        var window = new CrmProfileMergeWindow(personId, _currentUser) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            await SearchAsync();
            await LoadDetailsAsync();
        }
    }

    private async void FindDuplicatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        try
        {
            if (_selected.CrmPersonId is not null || _details?.Card.CrmPersonId is not null)
            {
                var cards = _details?.LinkedCards.Count ?? 0;
                MessageBox.Show($"Эта карточка уже входит в единого CRM-пациента. Справа показаны все связанные филиальные карты: {cards}.", "Дубли пациентов", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            WorkspaceNavigator.Navigate(new DuplicateReviewPage(_currentUser, _details is null ? _selected.FullName : $"{_details.Card.LastName} {_details.Card.FirstName}"));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Поиск дублей", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
    private async void AcceptCandidateButton_Click(object sender, RoutedEventArgs e) { if (_selected is null || CandidatesGrid.SelectedItem is not PatientMatchCandidate candidate) return; if (MessageBox.Show("Связать карточку с выбранным CRM-пациентом?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return; await _service.LinkToCandidateAsync(_selected.CardId, candidate.Id, _currentUser.UserId); await SearchAsync(); await LoadDetailsAsync(); }
    private async void LinkCallsButton_Click(object sender, RoutedEventArgs e) { if (_selected?.CrmPersonId is not int personId) return; var count = await _service.LinkMangoCallsAsync(personId); MessageBox.Show(count == 0 ? "Подходящих новых звонков не найдено." : $"Связано звонков Mango: {count}.", "Коммуникации"); await LoadDetailsAsync(); }
    private async void RecentCallsButton_Click(object sender, RoutedEventArgs e)
    {
        var phones = GetPatientPhones();
        if (phones.Count == 0) return;

        _recentCallsCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _recentCallsCancellation = cancellation;
        RecentCallsButton.IsEnabled = false;
        StopRecentCallsButton.Visibility = Visibility.Visible;
        StopRecentCallsButton.IsEnabled = true;
        RecentCallsGrid.ItemsSource = null;
        RecentCallsStatusTextBlock.Text = "MANGO: начинаем поиск последних звонков…";

        try
        {
            var progress = new Progress<MangoCallSearchProgress>(x =>
                RecentCallsStatusTextBlock.Text = $"{x.Message} найдено: {x.FoundCount}.");
            var client = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(2) }, MangoApiOptionsLoader.Load());
            var calls = await client.GetRecentCallsByPhonesAsync(phones, maxResults: 5, lookbackDays: 30, progress, cancellation.Token);
            RecentCallsGrid.ItemsSource = calls.Select(x => new RecentMangoCallRow(
                x.CallDateTime,
                x.Direction switch { "incoming" => "Входящий", "outgoing" => "Исходящий", "internal" => "Внутренний", _ => "Не определён" },
                string.IsNullOrWhiteSpace(x.EmployeeName) ? "Не определён" : x.EmployeeName!)).ToList();
            RecentCallsStatusTextBlock.Text = calls.Count == 0
                ? "За последние 30 дней звонки по телефону карточки в MANGO не найдены."
                : $"Найдено последних звонков: {calls.Count}. Период поиска: последние 30 дней.";
        }
        catch (OperationCanceledException)
        {
            RecentCallsStatusTextBlock.Text = "Поиск звонков остановлен пользователем.";
        }
        catch (Exception ex)
        {
            RecentCallsStatusTextBlock.Text = "Не удалось получить звонки из MANGO.";
            MessageBox.Show(ex.Message, "Последние звонки MANGO", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (ReferenceEquals(_recentCallsCancellation, cancellation)) _recentCallsCancellation = null;
            cancellation.Dispose();
            StopRecentCallsButton.IsEnabled = false;
            StopRecentCallsButton.Visibility = Visibility.Collapsed;
            RecentCallsButton.IsEnabled = GetPatientPhones().Count > 0;
        }
    }
    private void StopRecentCallsButton_Click(object sender, RoutedEventArgs e)
    {
        StopRecentCallsButton.IsEnabled = false;
        RecentCallsStatusTextBlock.Text = "Останавливаем запрос к MANGO…";
        _recentCallsCancellation?.Cancel();
    }
    private List<string> GetPatientPhones() => _details is null ? [] : _details.LinkedCards
        .Append(_details.Card)
        .Select(x => x.MobilePhone)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Cast<string>()
        .Distinct(StringComparer.Ordinal)
        .ToList();
    private async void CreateTaskButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selected?.CrmPersonId is not int personId) return;
        var task = new WorkTask { CrmPersonId = personId, ResponsibleUserId = _currentUser.UserId, DueAt = DateTime.Today.AddDays(1).AddHours(18) };
        var editor = new TaskEditorWindow(task, new UserService().GetAll(), _currentUser.UserId, _currentUser.RoleCode == "Admin") { Owner = Window.GetWindow(this) };
        if (editor.ShowDialog() != true) return;
        var service = new WorkTaskService();
        service.Save(editor.Task, _currentUser.UserId, _currentUser.RoleCode == "Admin");
        await LoadDetailsAsync();
    }
}

internal sealed record RecentMangoCallRow(DateTime OccurredAt, string DirectionName, string EmployeeName);
