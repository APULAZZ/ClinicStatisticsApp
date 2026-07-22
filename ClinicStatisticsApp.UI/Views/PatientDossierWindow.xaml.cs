using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.CallCenter.Services;
using System.Windows;
using System.Text.Json;
using System.Windows.Input;
using System.Globalization;
using System.Net.Http;

namespace ClinicStatisticsApp.UI.Views;

public partial class PatientDossierWindow : Window
{
    private readonly PatientCardDetails _details;
    private readonly CurrentUserInfo _currentUser;
    private readonly AppDbContext _db = DbContextFactory.Create();
    private bool _forceRefresh;
    private bool _isLoading;
    private List<ProfileRow> _profiles = []; private List<VisitRow> _visits = []; private List<ServiceRow> _services = []; private List<WorkRow> _works = []; private List<PaymentRow> _payments = []; private List<AppointmentRow> _appointments = [];
    public PatientDossierWindow(PatientCardDetails details, PatientSearchRow selected, CurrentUserInfo currentUser)
    {
        InitializeComponent();
        _details = details;
        _currentUser = currentUser;
        var card = details.Card;
        PatientNameTextBlock.Text = selected.FullName;
        SummaryTextBlock.Text = $"Основная CRM-карта: {selected.CardNumber ?? "—"} · источник данных: только чтение · связанных карт: {details.LinkedCards.Count}";
        CardsGrid.ItemsSource = details.LinkedCards;
        ActivityItems.ItemsSource = details.Activity;
        SourceFilterComboBox.ItemsSource = details.LinkedCards.Select(x => new SourceFilter($"{x.Branch?.Name} · карта {x.SourceCardNumber}", x.Branch?.Name ?? "")).Prepend(new SourceFilter("Все филиалы", "")).ToList();
        SourceFilterComboBox.SelectedIndex = 0;
        Loaded += async (_, _) => { await new PatientDossierSnapshotService().EnsureStorageAsync(); await new CrmPatientNoteService().EnsureStorageAsync(); await LoadCrmJournalAsync(); await LoadFirebirdDetailsAsync(); };
        Closed += (_, _) => _db.Dispose();
    }

    private async Task LoadFirebirdDetailsAsync()
    {
        if (_isLoading) return;
        _isLoading = true;
        RefreshButton.IsEnabled = false;
        RefreshButton.Content = _forceRefresh ? "Обновление…" : "Открытие…";
        LoadingTextBlock.Text = _forceRefresh
            ? "Идёт обновление из источников Firebird. Рабочие базы не изменяются."
            : "Проверяем сохранённый снимок карточки…";
        LoadingTextBlock.Visibility = Visibility.Visible;
        try
        {
            await LoadDossierDataAsync();
        }
        catch (Exception ex)
        {
            SourceStatusTextBlock.Text = $"Не удалось открыть досье: {ex.Message}";
        }
        finally
        {
            LoadingTextBlock.Visibility = Visibility.Collapsed;
            RefreshButton.Content = "Обновить из источников";
            RefreshButton.IsEnabled = true;
            _isLoading = false;
        }
    }

    private async Task LoadDossierDataAsync()
    {
        var snapshotService = new PatientDossierSnapshotService();
        if (!_forceRefresh)
        {
            var cached = await snapshotService.GetAsync(_details.LinkedCards.First().Id);
            if (cached is { IsSuccess: true } && !string.IsNullOrWhiteSpace(cached.PayloadJson))
            {
                DossierCache? cache;
                try { cache = JsonSerializer.Deserialize<DossierCache>(cached.PayloadJson); }
                catch (JsonException) { cache = null; }
                if (cache is not null)
                {
                    _profiles = cache.Profiles ?? []; _visits = cache.Visits ?? []; _services = cache.Services ?? []; _works = cache.Works ?? []; _payments = cache.Payments ?? []; _appointments = cache.Appointments ?? [];
                    ApplyFilter();
                    UpdateOverviewMetrics();
                    SourceStatusTextBlock.Text = $"Открыт локальный снимок: {cached.RefreshedAt:dd.MM.yyyy HH:mm}. Для новых данных нажмите «Обновить из источников».";
                    return;
                }
            }
        }
        LoadingTextBlock.Text = _forceRefresh
            ? "Получаем актуальные данные из источников Firebird…"
            : "Сохранённого снимка нет. Загружаем данные из источников Firebird…";
        var options = FirebirdClinicOptionsLoader.Load().ToDictionary(x => x.ClinicDataSourceId);
        var profiles = new List<ProfileRow>(); var visits = new List<VisitRow>(); var works = new List<WorkRow>(); var services = new List<ServiceRow>(); var payments = new List<PaymentRow>(); var appointments = new List<AppointmentRow>();
        var successfulSources = 0;
        foreach (var card in _details.LinkedCards.Where(x => x.ClinicDataSourceId is not null && options.ContainsKey(x.ClinicDataSourceId.Value)))
        {
            try
            {
                var dossier = await new FirebirdPatientDossierReader(options[card.ClinicDataSourceId!.Value]).ReadAsync(card.SourcePatientId);
                successfulSources++;
                var source = $"{card.Branch?.Name ?? "Филиал"} · карта {card.SourceCardNumber ?? "—"}";
                if (dossier.Profile is { } profile)
                    profiles.Add(new(source, $"Телефон: {profile.GetValueOrDefault("TEL_MOB")} · E-mail: {profile.GetValueOrDefault("EMAIL")} · Пол: {profile.GetValueOrDefault("POL")} · Скидка: {profile.GetValueOrDefault("SKIDKA")}", $"Адрес: {profile.GetValueOrDefault("GOROD")}, {profile.GetValueOrDefault("STREET")}, {profile.GetValueOrDefault("DOM")}"));
                visits.AddRange(dossier.Visits.Select(row => new VisitRow(row.GetValueOrDefault("DATE_IND") ?? "", card.Branch?.Name ?? "", card.SourceCardNumber ?? "", row.GetValueOrDefault("SUM_IND") ?? "", row.GetValueOrDefault("INF_IND") ?? "")));
                services.AddRange(dossier.Services.Select(row => new ServiceRow(row.GetValueOrDefault("DATE_IND"), card.Branch?.Name ?? "", row.GetValueOrDefault("TEXT_IND1"), row.GetValueOrDefault("KOL_IND1"), row.GetValueOrDefault("SUMMA_IND1"))));
                works.AddRange(dossier.Works.Select(row => new WorkRow(row.GetValueOrDefault("DATEWORK") ?? "", card.Branch?.Name ?? "", row.GetValueOrDefault("COMMENT") ?? "", row.GetValueOrDefault("NZUB_WP") ?? "", row.GetValueOrDefault("PRICE_END") ?? "")));
                payments.AddRange(dossier.Payments.Select(row => new PaymentRow(row.GetValueOrDefault("DATE_IND2"), card.Branch?.Name ?? "", row.GetValueOrDefault("SUMMA_IND2"), row.GetValueOrDefault("TEXT_IND2"), row.GetValueOrDefault("NOMKASSA_IND2"))));
                appointments.AddRange(dossier.Appointments.Select(row => new AppointmentRow(row.GetValueOrDefault("DATEPR"), row.GetValueOrDefault("TIMEPR"), card.Branch?.Name ?? "", row.GetValueOrDefault("ID_DOC"), row.GetValueOrDefault("ID_KABINET"), row.GetValueOrDefault("DOP_INFO"))));
            }
            catch (Exception ex) { profiles.Add(new(card.Branch?.Name ?? "Филиал", $"Источник временно недоступен: {ex.Message}", "")); }
        }
        _profiles = profiles; _visits = visits; _services = services; _works = works; _payments = payments; _appointments = appointments; ApplyFilter();
        var phones = _details.LinkedCards.Select(x => x.NormalizedMobilePhone).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count();
        var names = _details.LinkedCards.Select(x => $"{x.LastName} {x.FirstName} {x.MiddleName}".Trim()).Distinct().Count();
        var differences = new List<string>();
        if (names > 1) differences.Add("ФИО");
        if (phones > 1) differences.Add("телефоны");
        if (_details.LinkedCards.Select(x => x.DateOfBirth).Distinct().Count() > 1) differences.Add("дата рождения");
        if (_details.LinkedCards.Select(x => x.Email).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count() > 1) differences.Add("e-mail");
        MetricsTextBlock.Text = $"Приёмов: {visits.Count} · работ: {works.Count} · оплат: {payments.Count} · записей: {appointments.Count}" + (differences.Count > 0 ? $" · Различаются: {string.Join(", ", differences)}" : " · Данные филиалов согласованы");
        SourceStatusTextBlock.Text = $"Источники: {string.Join(" · ", _details.LinkedCards.Select(x => x.Branch?.Name).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())}. Обновлено при открытии: {DateTime.Now:dd.MM.yyyy HH:mm}";
        var snapshots = new PatientDossierSnapshotService();
        UpdateOverviewMetrics();
        var cachePayload = JsonSerializer.Serialize(new DossierCache(profiles, visits, services, works, payments, appointments));
        if (successfulSources > 0)
            foreach (var card in _details.LinkedCards) await snapshots.SaveAsync(card.Id, cachePayload, true, null);
    }
    private async void RefreshButton_Click(object sender, RoutedEventArgs e) { _forceRefresh = true; try { await LoadFirebirdDetailsAsync(); } finally { _forceRefresh = false; } }
    private void SourceFilterComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => ApplyFilter();
    private void ApplyFilter()
    {
        var branch = (SourceFilterComboBox.SelectedItem as SourceFilter)?.Value ?? "";
        ProfileItems.ItemsSource = _profiles.Where(x => branch == "" || x.Source.StartsWith(branch + " ·", StringComparison.OrdinalIgnoreCase)).ToList();
        VisitsGrid.ItemsSource = _visits.Where(x => branch == "" || x.Branch == branch).ToList();
        ServicesGrid.ItemsSource = _services.Where(x => branch == "" || x.Branch == branch).ToList();
        WorksGrid.ItemsSource = _works.Where(x => branch == "" || x.Branch == branch).ToList();
        PaymentsGrid.ItemsSource = _payments.Where(x => branch == "" || x.Branch == branch).ToList();
        AppointmentsGrid.ItemsSource = _appointments.Where(x => branch == "" || x.Branch == branch).ToList();
    }
    private void VisitsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VisitsGrid.SelectedItem is not VisitRow visit) return;
        var services = _services.Where(x => x.Branch == visit.Branch && x.Date == visit.Date).Select(x => $"• {x.Name} — {x.Quantity} шт., {x.Amount}");
        var payments = _payments.Where(x => x.Branch == visit.Branch && x.Date == visit.Date).Select(x => $"• {x.Amount} · {x.Description} · касса {x.Cashbox}");
        MessageBox.Show($"Приём: {visit.Date}\nФилиал: {visit.Branch}\nСумма: {visit.Amount}\nКомментарий: {visit.Comment}\n\nУслуги:\n{string.Join("\n", services.DefaultIfEmpty("нет данных"))}\n\nОплаты:\n{string.Join("\n", payments.DefaultIfEmpty("нет данных"))}", "Детали приёма", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void OpenVisitDetails(object sender, MouseButtonEventArgs e)
    {
        if (VisitsGrid.SelectedItem is not VisitRow visit) return;
        new VisitDetailsWindow(visit, _services.Where(x => x.Branch == visit.Branch && x.Date == visit.Date), _payments.Where(x => x.Branch == visit.Branch && x.Date == visit.Date)) { Owner = this }.ShowDialog();
    }
    private async Task LoadCrmJournalAsync()
    {
        var rows = new List<JournalRow>();
        foreach (var activity in _details.Activity)
        {
            var type = activity.ActivityType == "MangoCall" ? "Звонок Mango" : activity.ActivityType;
            rows.Add(new JournalRow(activity.OccurredAt ?? activity.CreatedAt, type, activity.Title, activity.ContactValue ?? "Mango", activity.ActivityType == "MangoCall" && int.TryParse(activity.ExternalId, out var callId) ? callId : null));
        }
        foreach (var task in _details.Tasks)
        {
            var due = task.DueAt is null ? "без срока" : $"срок {task.DueAt.Value:dd.MM.yyyy HH:mm}";
            rows.Add(new JournalRow(task.CreatedAt, "Задача", $"{task.Title} · {task.Status} · {due}", "Органайзер", null));
        }
        if (_details.Card.CrmPersonId is int personId)
        {
            var notes = await new CrmPatientNoteService().GetAsync(personId);
            rows.AddRange(notes.Select(note => new JournalRow(note.CreatedAt, "Заметка", note.Text, note.Author?.FullName ?? $"Пользователь #{note.AuthorUserId}", null)));
        }
        JournalGrid.ItemsSource = rows.OrderByDescending(x => x.Date).ToList();
    }

    private async void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_details.Card.CrmPersonId is not int personId) { MessageBox.Show("Сначала создайте единую CRM-карточку пациента.", "CRM-журнал", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            await new CrmPatientNoteService().AddAsync(personId, _currentUser.UserId, NewNoteTextBox.Text);
            NewNoteTextBox.Clear();
            await LoadCrmJournalAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "CRM-журнал", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void JournalGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (JournalGrid.SelectedItem is not JournalRow { MangoCallId: int callId }) return;
        var api = new MangoApiClient(new HttpClient { Timeout = TimeSpan.FromMinutes(10) }, MangoApiOptionsLoader.Load());
        var window = new CallDetailsWindow(_db, api) { Owner = this };
        await window.LoadAsync(callId);
        if (window.IsVisible) window.ShowDialog();
    }

    private void UpdateOverviewMetrics()
    {
        var total = _payments.Sum(x => ParseAmount(x.Amount));
        var lastPayment = _payments.Select(x => new { Row = x, Date = ParseDate(x.Date) })
            .Where(x => x.Date is not null).OrderByDescending(x => x.Date).FirstOrDefault();
        var now = DateTime.Today;
        var appointments = _appointments.Select(x => new { Row = x, Date = ParseDate(x.Date) })
            .Where(x => x.Date is not null).ToList();
        var nextAppointment = appointments.Where(x => x.Date >= now).OrderBy(x => x.Date).FirstOrDefault();
        var lastAppointment = appointments.Where(x => x.Date < now).OrderByDescending(x => x.Date).FirstOrDefault();
        MetricsTextBlock.Text = $"Приёмов: {_visits.Count} · работ: {_works.Count} · оплат: {_payments.Count} (итог: {total:N2}) · записей: {_appointments.Count}" +
            (lastPayment is null ? "" : $" · последняя оплата: {lastPayment.Date:dd.MM.yyyy} ({lastPayment.Row.Amount})") +
            (nextAppointment is not null ? $" · ближайшая запись: {nextAppointment.Date:dd.MM.yyyy}" : lastAppointment is not null ? $" · последняя запись: {lastAppointment.Date:dd.MM.yyyy}" : "");

        var lines = new List<string>();
        AddDifference("ФИО", _details.LinkedCards.Select(x => (x.Branch?.Name ?? "Филиал", $"{x.LastName} {x.FirstName} {x.MiddleName}".Trim())));
        AddDifference("Телефон", _details.LinkedCards.Where(x => !string.IsNullOrWhiteSpace(x.NormalizedMobilePhone)).Select(x => (x.Branch?.Name ?? "Филиал", x.NormalizedMobilePhone!)));
        AddDifference("Дата рождения", _details.LinkedCards.Where(x => x.DateOfBirth is not null).Select(x => (x.Branch?.Name ?? "Филиал", x.DateOfBirth!.Value.ToString("dd.MM.yyyy"))));
        AddDifference("E-mail", _details.LinkedCards.Where(x => !string.IsNullOrWhiteSpace(x.Email)).Select(x => (x.Branch?.Name ?? "Филиал", x.Email!)));
        DifferencesTextBlock.Text = lines.Count == 0
            ? "Расхождений между картами филиалов не найдено."
            : "Расхождения: " + string.Join("; ", lines);

        void AddDifference(string label, IEnumerable<(string Branch, string Value)> values)
        {
            var groups = values.Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .GroupBy(x => x.Value.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
            if (groups.Count > 1)
                lines.Add($"{label}: {string.Join(" / ", groups.Select(g => $"{string.Join(", ", g.Select(x => x.Branch).Distinct())} — {g.Key}"))}");
        }
    }
    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.AllowWhiteSpaces, out var date) ? date : null;
    private static decimal ParseAmount(string? value) => decimal.TryParse((value ?? "").Replace(" ", "").Replace(" ", ""), NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out var amount) ? amount : 0m;

    public sealed record ProfileRow(string Source, string Contacts, string Address);
    public sealed record VisitRow(string Date, string Branch, string Card, string Amount, string Comment);
    public sealed record ServiceRow(string? Date, string Branch, string? Name, string? Quantity, string? Amount);
    public sealed record WorkRow(string Date, string Branch, string Description, string Tooth, string Total);
    public sealed record PaymentRow(string? Date, string Branch, string? Amount, string? Description, string? Cashbox);
    public sealed record AppointmentRow(string? Date, string? Time, string Branch, string? Doctor, string? Room, string? Info);
    private sealed record JournalRow(DateTime Date, string Type, string Title, string Source, int? MangoCallId);
    public sealed record DossierCache(List<ProfileRow> Profiles, List<VisitRow> Visits, List<ServiceRow> Services, List<WorkRow> Works, List<PaymentRow> Payments, List<AppointmentRow> Appointments);
    private sealed record SourceFilter(string Label, string Value);
}
