using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI.Views;

public partial class SchedulePage : UserControl
{
    private readonly CurrentUserInfo _currentUser;
    private readonly List<SourceOption> _sources = [];
    private IReadOnlyList<ScheduleAppointment> _appointments = [];
    private int _resourcePage;
    private bool IsWeekMode => ViewModeComboBox?.SelectedIndex == 1;
    private bool IsPeriodMode => ViewModeComboBox?.SelectedIndex == 2;
    private const double PixelsPerMinute = 1.25;
    private const int FirstHour = 7;

    public SchedulePage(CurrentUserInfo currentUser)
    {
        InitializeComponent();
        _currentUser = currentUser;
        DayPicker.SelectedDate = DateTime.Today;
        PeriodEndPicker.SelectedDate = DateTime.Today.AddDays(6);
        Loaded += async (_, _) => await LoadSourcesAsync();
    }

    private async Task LoadSourcesAsync()
    {
        var configured = FirebirdClinicOptionsLoader.Load().Select(x => x.ClinicDataSourceId).ToHashSet();
        await using var db = DbContextFactory.Create();
        var sourceQuery = db.ClinicDataSources.AsNoTracking().Where(x => x.IsActive && configured.Contains(x.Id));
        if (_currentUser.RoleCode == ModuleAccessPolicy.BranchUserRole)
            sourceQuery = sourceQuery.Where(x => x.BranchId == _currentUser.BranchId);
        var sources = await sourceQuery
            .OrderByDescending(x => x.IsTest).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.IsTest }).ToListAsync();
        foreach (var source in sources)
        {
            var option = new SourceOption(source.Id, source.Name, source.IsTest);
            _sources.Add(option);
            var checkbox = new CheckBox { Content = $"{source.Name}{(source.IsTest ? " · тест" : "")}", IsChecked = !source.IsTest, Tag = option, Style = (Style)FindResource("FilterChip") };
            checkbox.Checked += SourceChanged; checkbox.Unchecked += SourceChanged;
            SourcesPanel.Children.Add(checkbox);
        }
        await LoadScheduleAsync();
    }

    private IReadOnlyCollection<int> SelectedSourceIds => SourcesPanel.Children.OfType<CheckBox>().Where(x => x.IsChecked == true).Select(x => ((SourceOption)x.Tag).Id).ToArray();
    private void SelectAllSources_Click(object sender, RoutedEventArgs e) { foreach (var source in SourcesPanel.Children.OfType<CheckBox>()) source.IsChecked = true; }
    private void ClearSources_Click(object sender, RoutedEventArgs e) { foreach (var source in SourcesPanel.Children.OfType<CheckBox>()) source.IsChecked = false; }
    private async void SourceChanged(object sender, RoutedEventArgs e) { _resourcePage = 0; await LoadScheduleAsync(); }
    private async void DayPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => await LoadScheduleAsync();
    private async void PeriodEndPicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => await LoadScheduleAsync();
    private void ScheduleFilterChanged(object sender, RoutedEventArgs e) { if (ScheduleGrid is not null) RenderSchedule(); }
    private async void DoctorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _resourcePage = 0; await LoadScheduleAsync(); }
    private async void ToggleFavoriteDoctor_Click(object sender, RoutedEventArgs e)
    {
        if (DoctorComboBox.SelectedItem is not DoctorOption doctor || doctor.Name == "Все врачи") { MessageBox.Show("Сначала выберите врача.", "Избранные врачи"); return; }
        await new ScheduleFavoriteDoctorService().ToggleAsync(_currentUser.UserId, doctor.Name); await LoadScheduleAsync();
    }
    private async void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // SelectedIndex is initialized while XAML is still constructing controls.
        if (ScheduleGrid is null) return;
        _resourcePage = 0;
        await LoadScheduleAsync();
    }
    private void PreviousDay_Click(object sender, RoutedEventArgs e) => DayPicker.SelectedDate = (DayPicker.SelectedDate ?? DateTime.Today).AddDays(-1);
    private void NextDay_Click(object sender, RoutedEventArgs e) => DayPicker.SelectedDate = (DayPicker.SelectedDate ?? DateTime.Today).AddDays(1);
    private void Today_Click(object sender, RoutedEventArgs e) => DayPicker.SelectedDate = DateTime.Today;
    private void PreviousResources_Click(object sender, RoutedEventArgs e) { if (_resourcePage > 0) { _resourcePage--; RenderSchedule(); } }
    private void NextResources_Click(object sender, RoutedEventArgs e)
    {
        var resourceCount = _appointments.Select(x => x.ResourceKey).Distinct().Count();
        if ((_resourcePage + 1) * GetResourcesPerPage() < resourceCount) { _resourcePage++; RenderSchedule(); }
    }
    private void ScheduleScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) => RenderSchedule();
    private void ScheduleScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        foreach (var element in ScheduleGrid.Children.OfType<UIElement>().Where(x => Grid.GetRow(x) == 0)) element.RenderTransform = new TranslateTransform(0, e.VerticalOffset);
    }

    private async void AddServiceBlock_Click(object sender, RoutedEventArgs e)
    {
        var doctor = (DoctorComboBox.SelectedItem as DoctorOption)?.Name;
        var resource = _appointments.FirstOrDefault(x => x.DoctorName == doctor && x.DoctorId.HasValue);
        if (resource is null || DayPicker.SelectedDate is not DateTime day) { MessageBox.Show("Сначала выберите одного врача с доступным расписанием.", "Служебный блок", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var dialog = CreateBlockDialog(day);
        if (dialog.Window.ShowDialog() != true) return;
        try
        {
            var starts = (TimeSpan)dialog.Start.SelectedItem; var ends = (TimeSpan)dialog.End.SelectedItem;
            var service = new ScheduleBlockService();
            for (var offset = 0; offset < (dialog.Repeat.IsChecked == true ? 56 : 1); offset++)
            {
                var target = day.Date.AddDays(offset);
                if (dialog.Repeat.IsChecked == true && target.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
                await service.AddAsync(new CrmScheduleBlock { ClinicDataSourceId = resource.SourceId, SourceDoctorId = resource.DoctorId!.Value, StartsAt = target.Add(starts), EndsAt = target.Add(ends), Title = dialog.Title.Text.Trim(), Kind = ((ComboBoxItem)dialog.Kind.SelectedItem).Content.ToString()!, CreatedByUserId = _currentUser.UserId });
            }
            await LoadScheduleAsync();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Служебный блок", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void LinkDoctorBranches_Click(object sender, RoutedEventArgs e)
    {
        try
        {
        if (DoctorComboBox.SelectedItem is not DoctorOption doctor || doctor.Name == "Все врачи") { MessageBox.Show("Выберите одного врача.", "Связь филиалов"); return; }
        if (DayPicker.SelectedDate is not DateTime day) return;
        var candidates = await new ScheduleService().GetDoctorDirectoryAsync(SelectedSourceIds);
        var panel = new StackPanel { Margin = new Thickness(18), Width = 380 };
        panel.Children.Add(new TextBlock { Text = $"Единый профиль: {doctor.Name}", FontSize = 16, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Отметьте соответствующих врачей из выбранных филиалов.", Margin = new Thickness(0, 6, 0, 10), TextWrapping = TextWrapping.Wrap });
        var choices = candidates.Select(x => new CheckBox { Content = $"{x.BranchName}: {x.DoctorName}", Tag = x, IsChecked = x.DoctorName == doctor.Name, Margin = new Thickness(0, 3, 0, 3) }).ToList(); foreach (var item in choices) panel.Children.Add(item);
        var save = new Button { Content = "Сохранить", Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }; panel.Children.Add(save);
        var window = new Window { Title = "Связь филиалов", Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen }; save.Click += (_, _) => window.DialogResult = true;
        if (window.ShowDialog() != true) return;
        await new ScheduleDoctorProfileService().SaveAsync(doctor.Name, choices.Where(x => x.IsChecked == true).Select(x => (ScheduleDoctorDirectoryItem)x.Tag!).Select(x => (x.SourceId, x.DoctorId, x.DoctorName)));
        StatusTextBlock.Text = "Связи филиалов сохранены.";
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось открыть связи филиалов: {ex.Message}", "Связь филиалов", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void ManageDoctorProfiles_Click(object sender, RoutedEventArgs e)
    {
        var service = new ScheduleDoctorProfileService(); var links = await service.GetAllLinksAsync();
        var groups = links.GroupBy(x => x.ProfileName).Select(x => new { Name = x.Key, Text = $"{x.Key} — {string.Join(", ", x.Select(y => y.DoctorName).Distinct())}" }).ToList();
        var list = new ListBox { ItemsSource = groups, DisplayMemberPath = "Text", MinWidth = 500, MinHeight = 220 };
        var delete = new Button { Content = "Удалить профиль", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 10, 8, 0) }; var close = new Button { Content = "Закрыть", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 10, 0, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(delete); buttons.Children.Add(close); var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(new TextBlock { Text = "Профили врачей", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) }); panel.Children.Add(list); panel.Children.Add(buttons);
        var window = new Window { Title = "Профили врачей", Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen }; close.Click += (_, _) => window.Close(); delete.Click += async (_, _) => { if (list.SelectedItem is null) return; var name = (string)list.SelectedItem.GetType().GetProperty("Name")!.GetValue(list.SelectedItem)!; if (MessageBox.Show($"Удалить профиль «{name}»?", "Профили врачей", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { await service.DeleteAsync(name); window.Close(); await LoadScheduleAsync(); } }; window.ShowDialog();
    }

    private async void ScheduleQualityCheck_Click(object sender, RoutedEventArgs e)
    {
        var missingNames = _appointments.Count(x => x.NeedsPatientNameRefresh || (x.SourcePatientId != 10 && !x.IsServiceBlock && string.IsNullOrWhiteSpace(x.PatientName)));
        var missingDoctor = _appointments.Count(x => !x.IsServiceBlock && !x.DoctorId.HasValue);
        var overlaps = _appointments.Where(x => !x.IsOpenSlot).GroupBy(x => x.ResourceKey).Sum(group => group.OrderBy(x => x.StartsAt).Zip(group.OrderBy(x => x.StartsAt).Skip(1), (left, right) => left.StartsAt.AddMinutes(left.DurationMinutes) > right.StartsAt ? 1 : 0).Sum());
        var profileCount = (await new ScheduleDoctorProfileService().GetProfilesAsync()).Count;
        MessageBox.Show($"Период/дата: {DayPicker.SelectedDate:dd.MM.yyyy}\n\nЗаписей в снимке: {_appointments.Count}\nБез ФИО пациента: {missingNames}\nБез локального ID врача: {missingDoctor}\nПересечений по одному ресурсу: {overlaps}\nПрофилей врачей: {profileCount}\n\nПроверка не изменяет Firebird или расписание.", "Контроль данных расписания", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static (Window Window, TextBox Title, ComboBox Kind, ComboBox Start, ComboBox End, CheckBox Repeat) CreateBlockDialog(DateTime day, string? initialTitle = null, string? initialKind = null, TimeSpan? initialStart = null, TimeSpan? initialEnd = null)
    {
        var title = new TextBox { Text = initialTitle ?? "Обед", Margin = new Thickness(0, 4, 0, 10) }; var kind = new ComboBox { Margin = new Thickness(0, 4, 0, 10) };
        foreach (var value in new[] { "Обед", "Планёрка", "Отпуск", "Служебное" }) kind.Items.Add(new ComboBoxItem { Content = value }); kind.SelectedIndex = Math.Max(0, new[] { "Обед", "Планёрка", "Отпуск", "Служебное" }.ToList().IndexOf(initialKind ?? "Обед"));
        var start = new ComboBox { Margin = new Thickness(0, 4, 0, 10) }; var end = new ComboBox { Margin = new Thickness(0, 4, 0, 14) };
        for (var hour = 7; hour <= 20; hour++) foreach (var minute in new[] { 0, 30 }) { var value = new TimeSpan(hour, minute, 0); start.Items.Add(value); end.Items.Add(value); }
        start.SelectedItem = initialStart ?? new TimeSpan(13, 0, 0); end.SelectedItem = initialEnd ?? new TimeSpan(14, 0, 0);
        var repeat = new CheckBox { Content = "Повторять по будням 8 недель", Margin = new Thickness(0, 0, 0, 14) };
        var save = new Button { Content = "Сохранить", Padding = new Thickness(14, 6, 14, 6), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true };
        var panel = new StackPanel { Margin = new Thickness(18), Width = 280 }; panel.Children.Add(new TextBlock { Text = $"Служебный блок · {day:dd.MM.yyyy}", FontWeight = FontWeights.SemiBold, FontSize = 16 }); panel.Children.Add(new TextBlock { Text = "Название", Margin = new Thickness(0, 14, 0, 0) }); panel.Children.Add(title); panel.Children.Add(new TextBlock { Text = "Тип" }); panel.Children.Add(kind); panel.Children.Add(new TextBlock { Text = "Начало" }); panel.Children.Add(start); panel.Children.Add(new TextBlock { Text = "Окончание" }); panel.Children.Add(end); panel.Children.Add(save);
        panel.Children.Add(repeat); var window = new Window { Title = "Служебный блок", Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        save.Click += (_, _) => { if (string.IsNullOrWhiteSpace(title.Text) || start.SelectedItem is not TimeSpan s || end.SelectedItem is not TimeSpan e || e <= s) { MessageBox.Show("Укажите название и корректное время.", "Служебный блок", MessageBoxButton.OK, MessageBoxImage.Warning); return; } window.DialogResult = true; };
        return (window, title, kind, start, end, repeat);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (DayPicker.SelectedDate is not DateTime day || SelectedSourceIds.Count == 0) return;
        RefreshButton.IsEnabled = false; StatusTextBlock.Text = "Обновляем снимок. Firebird читается только на чтение…";
        try
        {
            var result = await new CrmAnalyticsWarehouseService().ImportAppointmentsAsync(day, day, SelectedSourceIds, new Progress<string>(x => StatusTextBlock.Text = x));
            StatusTextBlock.Text = $"Обновлено: {result.Sources.Sum(x => x.Appointments)} записей.";
            await LoadScheduleAsync();
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Ошибка: {ex.Message}"; }
        finally { RefreshButton.IsEnabled = true; }
    }

    private async Task LoadScheduleAsync()
    {
        if (!IsLoaded || DayPicker.SelectedDate is not DateTime day || SelectedSourceIds.Count == 0) { RenderEmpty("Выберите хотя бы один филиал."); return; }
        var selectedDoctor = DoctorComboBox.SelectedItem as DoctorOption;
        try
        {
            var service = new ScheduleService();
            var doctor = selectedDoctor?.Name == "Все врачи" ? null : selectedDoctor?.Name;
            if ((IsWeekMode || IsPeriodMode) && string.IsNullOrWhiteSpace(doctor)) { RenderEmpty("Для недельного режима и периода выберите одного врача."); return; }
            var from = IsWeekMode ? StartOfWeek(day) : day.Date;
            var to = IsWeekMode ? from.AddDays(6) : IsPeriodMode ? (PeriodEndPicker.SelectedDate ?? day).Date : from;
            if (to < from) { RenderEmpty("Конечная дата не может быть раньше начальной."); return; }
            if ((to - from).TotalDays > 30) { RenderEmpty("Для периода выберите не более 31 дня."); return; }
            _appointments = await service.GetRangeAsync(from, to, SelectedSourceIds, doctor);
            if (_appointments.Any(x => x.NeedsPatientNameRefresh))
            {
                StatusTextBlock.Text = "Загружаем ФИО пациентов из Firebird…";
                await new CrmAnalyticsWarehouseService().ImportAppointmentsAsync(from, to, SelectedSourceIds);
                _appointments = await service.GetRangeAsync(from, to, SelectedSourceIds, doctor);
            }
            // The selector must not be built from the already filtered grid: otherwise
            // selecting a unified profile would leave just that one doctor available.
            var favorites = await new ScheduleFavoriteDoctorService().GetAsync(_currentUser.UserId);
            var doctors = (await service.GetDoctorDirectoryAsync(SelectedSourceIds))
                .Select(x => x.DoctorName).Distinct().OrderBy(x => !favorites.Contains(x)).ThenBy(x => x).Select(x => new DoctorOption(x, favorites.Contains(x))).ToList();
            var current = selectedDoctor?.Name;
            DoctorComboBox.SelectionChanged -= DoctorComboBox_SelectionChanged;
            DoctorComboBox.ItemsSource = new[] { new DoctorOption("Все врачи") }.Concat(doctors).ToList();
            DoctorComboBox.SelectedItem = DoctorComboBox.Items.Cast<DoctorOption>().FirstOrDefault(x => x.Name == current) ?? DoctorComboBox.Items.Cast<DoctorOption>().First();
            DoctorComboBox.SelectionChanged += DoctorComboBox_SelectionChanged;
            RenderSchedule();
            StatusTextBlock.Text = _appointments.Count == 0 ? "Нет снимка на эту дату. Нажмите «Обновить из Firebird»." : $"Записей: {_appointments.Count}.";
        }
        catch { RenderEmpty("Снимок ещё не создан. Выберите тестовый источник и нажмите «Обновить из Firebird»."); }
    }

    private void RenderSchedule()
    {
        if (IsPeriodMode && ((PeriodEndPicker.SelectedDate ?? DayPicker.SelectedDate ?? DateTime.Today).Date - (DayPicker.SelectedDate ?? DateTime.Today).Date).TotalDays > 6)
        {
            RenderPeriodList();
            return;
        }
        if (IsWeekMode || IsPeriodMode) { RenderWeekSchedule(); return; }
        if (_appointments.Count == 0) { RenderEmpty("Нет записей на выбранную дату."); return; }
        ScheduleGrid.Children.Clear(); ScheduleGrid.ColumnDefinitions.Clear(); ScheduleGrid.RowDefinitions.Clear();
        var visible = GetVisibleAppointments();
        var allResources = visible.GroupBy(x => x.ResourceKey).Select(x => new { x.Key, Title = CompactResourceTitle(x.First()) }).ToList();
        var resourcesPerPage = GetResourcesPerPage();
        var lastPage = Math.Max(0, (allResources.Count - 1) / resourcesPerPage);
        _resourcePage = Math.Min(_resourcePage, lastPage);
        var resources = allResources.Skip(_resourcePage * resourcesPerPage).Take(resourcesPerPage).ToList();
        ResourcesPageTextBlock.Text = allResources.Count <= resourcesPerPage ? $"Врачей: {allResources.Count}" : $"{_resourcePage * resourcesPerPage + 1}–{Math.Min(allResources.Count, (_resourcePage + 1) * resourcesPerPage)} из {allResources.Count}";
        ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        foreach (var _ in resources) ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = resources.Count == 1 ? new GridLength(360) : new GridLength(1, GridUnitType.Star) });
        ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(900) });
        var corner = new TextBlock { Text = "Время", FontWeight = FontWeights.SemiBold, Foreground = Brushes.SlateGray, Margin = new Thickness(8) }; Grid.SetRow(corner, 0); ScheduleGrid.Children.Add(corner);
        var timeline = new Canvas { Height = 900, Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)) }; Grid.SetRow(timeline, 1); ScheduleGrid.Children.Add(timeline);
        for (var hour = FirstHour; hour <= 19; hour++)
        {
            var y = (hour - FirstHour) * 60 * PixelsPerMinute;
            var text = new TextBlock { Text = $"{hour:00}:00", Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontSize = 12 }; Canvas.SetTop(text, y - 8); Canvas.SetLeft(text, 7); timeline.Children.Add(text);
        }
        for (var index = 0; index < resources.Count; index++)
        {
            var header = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1, 1, 1, 0), Padding = new Thickness(5) };
            header.Child = new TextBlock { Text = resources[index].Title, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) }; Grid.SetColumn(header, index + 1); ScheduleGrid.Children.Add(header);
            var canvas = new Canvas { Height = 900, Background = new SolidColorBrush(Color.FromRgb(252, 253, 254)) }; Grid.SetColumn(canvas, index + 1); Grid.SetRow(canvas, 1); ScheduleGrid.Children.Add(canvas);
            AddGridLines(canvas);
            var columnWidth = resources.Count == 1 ? 360 : Math.Max(110, (ScheduleScrollViewer.ActualWidth - 90) / Math.Max(1, resources.Count));
            AddResourceAppointments(canvas, visible.Where(x => x.ResourceKey == resources[index].Key && !x.IsOpenSlot).OrderBy(x => x.StartsAt).ToList(), columnWidth - 12);
        }
    }

    private void RenderWeekSchedule()
    {
        if (_appointments.Count == 0) { RenderEmpty("На эту неделю нет записей. При необходимости обновите данные из Firebird."); return; }
        ScheduleGrid.Children.Clear(); ScheduleGrid.ColumnDefinitions.Clear(); ScheduleGrid.RowDefinitions.Clear();
        var monday = IsPeriodMode ? (DayPicker.SelectedDate ?? DateTime.Today).Date : StartOfWeek(DayPicker.SelectedDate ?? DateTime.Today);
        var dayCount = IsPeriodMode ? (int)Math.Max(1, Math.Min(31, ((PeriodEndPicker.SelectedDate ?? monday) - monday).TotalDays + 1)) : 7;
        ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        for (var i = 0; i < dayCount; i++) ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(900) });
        var corner = new TextBlock { Text = "Время", FontWeight = FontWeights.SemiBold, Foreground = Brushes.SlateGray, Margin = new Thickness(8) }; Grid.SetRow(corner, 0); ScheduleGrid.Children.Add(corner);
        var timeline = new Canvas { Height = 900, Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)) }; Grid.SetRow(timeline, 1); ScheduleGrid.Children.Add(timeline);
        for (var hour = FirstHour; hour <= 19; hour++) { var y = (hour - FirstHour) * 60 * PixelsPerMinute; var text = new TextBlock { Text = $"{hour:00}:00", Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), FontSize = 12 }; Canvas.SetTop(text, y - 8); Canvas.SetLeft(text, 7); timeline.Children.Add(text); }
        for (var index = 0; index < dayCount; index++)
        {
            var day = monday.AddDays(index); var header = new Border { Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1, 1, 1, 0), Padding = new Thickness(5) };
            header.Child = new TextBlock { Text = $"{day:ddd}\n{day:dd.MM}", FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)) }; Grid.SetColumn(header, index + 1); ScheduleGrid.Children.Add(header);
            var canvas = new Canvas { Height = 900, Background = new SolidColorBrush(Color.FromRgb(252, 253, 254)) }; Grid.SetColumn(canvas, index + 1); Grid.SetRow(canvas, 1); ScheduleGrid.Children.Add(canvas); AddGridLines(canvas);
            var columnWidth = Math.Max(80, (ScheduleScrollViewer.ActualWidth - 90) / dayCount);
            AddResourceAppointments(canvas, GetVisibleAppointments().Where(x => x.StartsAt.Date == day && !x.IsOpenSlot).OrderBy(x => x.StartsAt).ToList(), columnWidth - 12);
        }
        ResourcesPageTextBlock.Text = IsPeriodMode ? $"Период: {monday:dd.MM}–{monday.AddDays(dayCount - 1):dd.MM}" : "Неделя выбранного врача";
    }

    private int GetResourcesPerPage()
    {
        var width = ScheduleScrollViewer?.ActualWidth ?? 0;
        return width <= 0 ? 8 : Math.Clamp((int)((width - 76) / 150), 3, 8);
    }

    private IReadOnlyList<ScheduleAppointment> GetVisibleAppointments() => _appointments.Where(x =>
        x.IsServiceBlock ? ShowServiceBlocksCheckBox?.IsChecked == true :
        x.PatientName == "РЕЗЕРВ" ? ShowReservesCheckBox?.IsChecked == true :
        x.IsNoShow ? ShowNoShowsCheckBox?.IsChecked == true : ShowVisitsCheckBox?.IsChecked == true).ToList();

    private static DateTime StartOfWeek(DateTime value)
    {
        var offset = value.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)value.DayOfWeek - 1;
        return value.Date.AddDays(-offset);
    }

    private static string CompactResourceTitle(ScheduleAppointment appointment)
    {
        var names = appointment.DoctorName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var compactName = names.Length switch { 0 => appointment.DoctorName, 1 => names[0], _ => $"{names[0]} {string.Join(" ", names.Skip(1).Select(x => $"{x[0]}."))}" };
        return $"{compactName}\n{appointment.BranchName}";
    }

    private static void AddGridLines(Canvas canvas)
    {
        for (var minute = 0; minute <= 12 * 60; minute += 30)
        {
            var y = minute * PixelsPerMinute;
            canvas.Children.Add(new System.Windows.Shapes.Line { X1 = 0, X2 = 5000, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(minute % 60 == 0 ? Color.FromRgb(203, 213, 225) : Color.FromRgb(241, 245, 249)), StrokeThickness = minute % 60 == 0 ? 1 : 0.7 });
        }
        canvas.Children.Add(new Border { Width = 5000, Height = 900, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(0, 0, 1, 1), IsHitTestVisible = false });
    }

    private void AddResourceAppointments(Canvas canvas, IReadOnlyList<ScheduleAppointment> appointments, double width)
    {
        for (var index = 0; index < appointments.Count; index++)
        {
            var nextGap = index + 1 < appointments.Count ? Math.Max(1, (appointments[index + 1].StartsAt - appointments[index].StartsAt).TotalMinutes) : appointments[index].DurationMinutes;
            AddAppointment(canvas, appointments[index], width, Math.Min(appointments[index].DurationMinutes, nextGap));
        }
    }

    private void AddAppointment(Canvas canvas, ScheduleAppointment appointment, double width, double availableMinutes)
    {
        var top = Math.Max(0, (appointment.StartsAt.TimeOfDay.TotalMinutes - FirstHour * 60) * PixelsPerMinute);
        var color = appointment.IsServiceBlock ? Color.FromRgb(226, 232, 240)
            : appointment.PatientName == "РЕЗЕРВ" ? Color.FromRgb(254, 243, 199)
            : appointment.IsNoShow ? Color.FromRgb(254, 226, 226)
            : appointment.AppointmentType is "80" or "60" ? Color.FromRgb(219, 234, 254)
            : Color.FromRgb(236, 253, 245);
        var height = Math.Max(10, availableMinutes * PixelsPerMinute - 2);
        var border = new Border { Background = new SolidColorBrush(color), BorderBrush = new SolidColorBrush(appointment.IsNoShow ? Color.FromRgb(248, 113, 113) : Color.FromRgb(96, 165, 250)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(5, 4, 5, 3), Width = width, Height = height, ToolTip = string.Join("\n", new[] { appointment.PatientName, appointment.Room, appointment.Info }.Where(x => !string.IsNullOrWhiteSpace(x))) };
        var content = new StackPanel();
        content.Children.Add(new TextBlock { Text = height < 20 ? appointment.StartsAt.ToString("HH:mm") : $"{appointment.StartsAt:HH:mm}  {appointment.PatientName}", FontWeight = FontWeights.SemiBold, FontSize = height < 20 ? 8 : 11, TextTrimming = TextTrimming.CharacterEllipsis });
        if (height >= 20) content.Children.Add(new TextBlock { Text = appointment.BranchName, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), TextTrimming = TextTrimming.CharacterEllipsis });
        if (height >= 64 && !string.IsNullOrWhiteSpace(appointment.Room)) content.Children.Add(new TextBlock { Text = appointment.Room, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), TextTrimming = TextTrimming.CharacterEllipsis });
        border.Child = content;
        if (!appointment.IsServiceBlock) { border.Cursor = System.Windows.Input.Cursors.Hand; border.MouseLeftButtonDown += (_, _) => ShowAppointmentDetails(appointment); }
        if (appointment.IsServiceBlock && appointment.ScheduleBlockId is int blockId)
        {
            border.Cursor = System.Windows.Input.Cursors.Hand;
            border.MouseLeftButtonDown += async (_, _) =>
            {
                var edit = CreateBlockDialog(appointment.StartsAt.Date, appointment.PatientName, appointment.AppointmentType, appointment.StartsAt.TimeOfDay, appointment.StartsAt.AddMinutes(appointment.DurationMinutes).TimeOfDay);
                if (edit.Window.ShowDialog() != true) return;
                await new ScheduleBlockService().UpdateAsync(new CrmScheduleBlock { Id = blockId, StartsAt = appointment.StartsAt.Date.Add((TimeSpan)edit.Start.SelectedItem), EndsAt = appointment.StartsAt.Date.Add((TimeSpan)edit.End.SelectedItem), Title = edit.Title.Text.Trim(), Kind = ((ComboBoxItem)edit.Kind.SelectedItem).Content.ToString()! });
                await LoadScheduleAsync();
            };
            border.MouseRightButtonDown += async (_, _) => { if (MessageBox.Show($"Удалить служебный блок «{appointment.PatientName}»?", "Служебный блок", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) { await new ScheduleBlockService().DeleteAsync(blockId); await LoadScheduleAsync(); } };
        }
        Canvas.SetTop(border, top); Canvas.SetLeft(border, 6); canvas.Children.Add(border);
    }

    private void RenderEmpty(string message)
    {
        ScheduleGrid.Children.Clear(); ScheduleGrid.ColumnDefinitions.Clear(); ScheduleGrid.RowDefinitions.Clear();
        ScheduleGrid.Children.Add(new TextBlock { Text = message, Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)), Margin = new Thickness(18), TextWrapping = TextWrapping.Wrap });
    }

    private void ShowAppointmentDetails(ScheduleAppointment appointment)
    {
        var status = appointment.PatientName == "РЕЗЕРВ" ? "Резерв" : appointment.IsNoShow ? "Неявка" : "Приём";
        var panel = new StackPanel { Margin = new Thickness(18), Width = 360 };
        panel.Children.Add(new TextBlock { Text = appointment.PatientName, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        foreach (var line in new[] { $"Время: {appointment.StartsAt:dd.MM.yyyy HH:mm}", $"Филиал: {appointment.BranchName}", $"Врач: {appointment.DoctorName}", $"Статус: {status}", string.IsNullOrWhiteSpace(appointment.Room) ? null : $"Кабинет: {appointment.Room}", string.IsNullOrWhiteSpace(appointment.Info) ? null : $"Комментарий: {appointment.Info}" }.Where(x => x is not null)) panel.Children.Add(new TextBlock { Text = line, Margin = new Thickness(0, 7, 0, 0), TextWrapping = TextWrapping.Wrap });
        var openPatient = new Button { Content = "Открыть пациента в CRM", Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, IsEnabled = appointment.SourcePatientId is not 10 and not 350000 && !appointment.IsServiceBlock };
        panel.Children.Add(openPatient);
        var window = new Window { Title = "Запись в расписании", Content = panel, SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        openPatient.Click += (_, _) => { window.Close(); WorkspaceNavigator.Navigate(new PatientDirectoryPage(_currentUser, appointment.PatientName)); };
        window.ShowDialog();
    }

    private void RenderPeriodList()
    {
        ScheduleGrid.Children.Clear(); ScheduleGrid.ColumnDefinitions.Clear(); ScheduleGrid.RowDefinitions.Clear();
        var entries = GetVisibleAppointments().Where(x => !x.IsOpenSlot).OrderBy(x => x.StartsAt).ThenBy(x => x.BranchName).ToList();
        if (entries.Count == 0) { RenderEmpty("Нет записей с выбранными фильтрами за этот период."); return; }
        var panel = new StackPanel { Margin = new Thickness(8) };
        foreach (var group in entries.GroupBy(x => x.StartsAt.Date))
        {
            panel.Children.Add(new TextBlock { Text = group.Key.ToString("dddd, dd.MM.yyyy"), FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), Margin = new Thickness(4, 12, 4, 6) });
            foreach (var appointment in group)
            {
                var status = appointment.IsServiceBlock ? "Служебный блок" : appointment.PatientName == "РЕЗЕРВ" ? "Резерв" : appointment.IsNoShow ? "Неявка" : "Приём";
                var border = new Border { Background = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 2, 0, 2) };
                var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                row.Children.Add(new TextBlock { Text = appointment.StartsAt.ToString("HH:mm"), FontWeight = FontWeights.SemiBold });
                var branch = new TextBlock { Text = appointment.BranchName, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) }; Grid.SetColumn(branch, 1); row.Children.Add(branch);
                var patient = new TextBlock { Text = appointment.PatientName, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetColumn(patient, 2); row.Children.Add(patient);
                var label = new TextBlock { Text = status, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), TextAlignment = TextAlignment.Right }; Grid.SetColumn(label, 3); row.Children.Add(label); border.Child = row; panel.Children.Add(border);
            }
        }
        ScheduleGrid.Children.Add(panel); ResourcesPageTextBlock.Text = $"Компактный список: {entries.Count} записей";
    }

    private sealed record SourceOption(int Id, string Name, bool IsTest);
    private sealed record DoctorOption(string Name, bool IsFavorite = false) { public string Display => IsFavorite ? $"★ {Name}" : Name; }
}
