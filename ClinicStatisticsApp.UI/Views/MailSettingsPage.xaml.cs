using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.UI.Mail;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI.Views;

public partial class MailSettingsPage : UserControl
{
    private readonly CurrentUserInfo _currentUser;
    private readonly List<MailFilterRule> _filters;
    public MailSettingsPage(CurrentUserInfo currentUser)
    {
        InitializeComponent(); _currentUser = currentUser;
        var settings = MailSettingsStore.Load(currentUser.UserId);
        AddressTextBox.Text = settings.Address; PasswordBox.Password = settings.Password;
        _filters = MailFilterStore.Load(currentUser.UserId);
        RefreshFilters();
        Loaded += async (_, _) => await LoadFilterFoldersAsync(settings);
    }

    private MailSettings ReadSettings() => new() { Address = AddressTextBox.Text.Trim(), Password = PasswordBox.Password };
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AddressTextBox.Text) || string.IsNullOrWhiteSpace(PasswordBox.Password)) { StatusTextBlock.Text = "Заполните почтовый ящик и пароль."; return; }
        MailSettingsStore.Save(_currentUser.UserId, ReadSettings()); StatusTextBlock.Text = "Настройки сохранены.";
    }
    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        try { using var busy = App.Busy.Begin("Проверяем подключение к почте…"); var settings = ReadSettings(); if (string.IsNullOrWhiteSpace(settings.Address) || string.IsNullOrWhiteSpace(settings.Password)) throw new InvalidOperationException("Заполните почтовый ящик и пароль."); await new MailService(settings).TestAsync(); StatusTextBlock.Text = "Подключение работает. Почтовый ящик доступен."; }
        catch (Exception ex) { StatusTextBlock.Text = $"Не удалось подключиться: {ex.Message}"; }
    }

    private async Task LoadFilterFoldersAsync(MailSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Address) || string.IsNullOrWhiteSpace(settings.Password)) return;
        try { FilterDestinationComboBox.ItemsSource = await new MailService(settings).GetFoldersAsync(); }
        catch { }
    }

    private void AddFilterButton_Click(object sender, RoutedEventArgs e)
    {
        var senderText = FilterSenderTextBox.Text.Trim();
        var subjectText = FilterSubjectTextBox.Text.Trim();
        var destination = FilterDestinationComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(senderText) && string.IsNullOrWhiteSpace(subjectText)) { StatusTextBlock.Text = "Укажите отправителя или тему для фильтра."; return; }
        if (string.IsNullOrWhiteSpace(destination)) { StatusTextBlock.Text = "Выберите папку назначения."; return; }
        _filters.Add(new MailFilterRule { SenderContains = senderText, SubjectContains = subjectText, DestinationFolder = destination, MarkAsRead = FilterMarkReadCheckBox.IsChecked == true });
        MailFilterStore.Save(_currentUser.UserId, _filters);
        FilterSenderTextBox.Clear(); FilterSubjectTextBox.Clear(); FilterMarkReadCheckBox.IsChecked = false;
        RefreshFilters(); StatusTextBlock.Text = "Правило добавлено.";
    }

    private void DeleteFilterButton_Click(object sender, RoutedEventArgs e)
    {
        if (FiltersListBox.SelectedItem is not MailFilterRule rule) return;
        _filters.Remove(rule); MailFilterStore.Save(_currentUser.UserId, _filters); RefreshFilters(); StatusTextBlock.Text = "Правило удалено.";
    }

    private void RefreshFilters() { FiltersListBox.ItemsSource = null; FiltersListBox.ItemsSource = _filters; }
}
