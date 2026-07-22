using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.UI.Mail;
using Microsoft.Win32;
using System.Media;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI.Views;

public partial class MailPage : UserControl
{
    private readonly MailService _service;
    private readonly int _userId;
    private readonly System.Windows.Threading.DispatcherTimer _mailRefreshTimer = new() { Interval = TimeSpan.FromMinutes(2) };
    private readonly List<string> _attachedFiles = [];
    private IReadOnlyList<string> _allFolders = [];
    private IReadOnlyList<MailMessageItem> _folderMessages = [];
    private MailMessageItem? _openItem;
    private MailMessageDetails? _openMessage;
    private string _currentFolder = "INBOX";

    public MailPage(CurrentUserInfo currentUser)
    {
        InitializeComponent();
        _userId = currentUser.UserId;
        _service = new MailService(MailSettingsStore.Load(currentUser.UserId));
        Loaded += async (_, _) => { await LoadFoldersAsync(); _mailRefreshTimer.Start(); };
        Unloaded += (_, _) => _mailRefreshTimer.Stop();
        _mailRefreshTimer.Tick += async (_, _) => await RefreshInboxSilentlyAsync();
    }

    private async Task LoadFoldersAsync()
    {
        if (!_service.IsConfigured) { WelcomeTextBlock.Text = "Чтобы начать работу, откройте «Настройки → Настройки почты» и укажите данные ящика Mail.ru."; return; }
        try
        {
            using var busy = App.Busy.Begin("Загружаем почту…");
            _allFolders = await _service.GetFoldersAsync();
            var inbox = _allFolders.FirstOrDefault(x => x.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) ?? "INBOX";
            FoldersTreeView.ItemsSource = BuildFolderTree(_allFolders, inbox);
            _currentFolder = inbox;
            await LoadMessagesAsync();
            _ = PrefetchFoldersAsync(_allFolders, inbox);
        }
        catch (Exception ex) { WelcomeTextBlock.Text = $"Не удалось получить почту: {ex.Message}"; }
    }

    private async Task LoadMessagesAsync(bool forceServerRefresh = false)
    {
        try
        {
            if (!forceServerRefresh && MailFolderCache.TryGet(_service.AccountKey, _currentFolder, out var cachedMessages))
            {
                _folderMessages = cachedMessages;
                ShowFilteredMessages();
                CacheStatusTextBlock.Text = "из кэша";
                UpdateFolderTitle();
                return;
            }
            using var busy = App.Busy.Begin("Загружаем письма…");
            _folderMessages = await _service.GetMessagesAsync(_currentFolder);
            if (_currentFolder.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) _folderMessages = await ApplyInboxFiltersAsync(_currentFolder, _folderMessages);
            MailFolderCache.Store(_service.AccountKey, _currentFolder, _folderMessages);
            ShowFilteredMessages();
            CacheStatusTextBlock.Text = "только что обновлено";
            UpdateFolderTitle();
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось загрузить письма: {ex.Message}", "Почта", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void UpdateFolderTitle() => FolderTitleTextBlock.Text = _currentFolder.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ? "Входящие" : _currentFolder;

    private void ShowFilteredMessages()
    {
        var query = SearchTextBox.Text.Trim();
        MessagesListBox.ItemsSource = string.IsNullOrWhiteSpace(query) ? _folderMessages : _folderMessages.Where(x => x.Sender.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Subject.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async void FoldersTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not MailFolderNode folder || string.IsNullOrWhiteSpace(folder.Path)) return;
        _currentFolder = folder.Path;
        MessagePanel.Visibility = Visibility.Collapsed; WelcomePanel.Visibility = Visibility.Visible;
        await LoadMessagesAsync();
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ShowFilteredMessages();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        MailFolderCache.Invalidate(_service.AccountKey, _currentFolder);
        await LoadMessagesAsync(true);
    }

    private async void MessagesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MessagesListBox.SelectedItems.Count != 1 || MessagesListBox.SelectedItem is not MailMessageItem item) return;
        try
        {
            using var busy = App.Busy.Begin("Открываем письмо…");
            _openItem = item;
            _openMessage = await _service.GetMessageAsync(_currentFolder, item.Uid);
            ComposePanel.Visibility = Visibility.Collapsed; WelcomePanel.Visibility = Visibility.Collapsed; MessagePanel.Visibility = Visibility.Visible;
            MessageSubjectTextBlock.Text = _openMessage.Subject;
            MessageFromTextBlock.Text = $"От: {_openMessage.Sender}\nКому: {_openMessage.Recipients}";
            MessageDateTextBlock.Text = _openMessage.Date.LocalDateTime.ToString("dd.MM.yyyy HH:mm");
            MessageBodyTextBlock.Text = _openMessage.Body;
            ShowAttachments(_openMessage.Attachments);
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось открыть письмо: {ex.Message}", "Почта", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ShowAttachments(IReadOnlyList<MailAttachment> attachments)
    {
        AttachmentsListPanel.Children.Clear();
        AttachmentsPanel.Visibility = attachments.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var attachment in attachments)
        {
            var button = new Button { Content = "📎 " + attachment.FileName, Style = (Style)FindResource("TextButton"), Tag = attachment, Margin = new Thickness(0, 0, 8, 0) };
            button.Click += SaveAttachmentButton_Click;
            AttachmentsListPanel.Children.Add(button);
        }
    }

    private async void SaveAttachmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MailAttachment attachment } || _openItem is null) return;
        var dialog = new SaveFileDialog { FileName = attachment.FileName };
        if (dialog.ShowDialog() != true) return;
        try { using var busy = App.Busy.Begin("Сохраняем вложение…"); await _service.SaveAttachmentAsync(_currentFolder, _openItem.Uid, attachment.Index, dialog.FileName); }
        catch (Exception ex) { MessageBox.Show($"Не удалось сохранить вложение: {ex.Message}", "Почта", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private IReadOnlyList<MailMessageItem> SelectedMessages() => MessagesListBox.SelectedItems.OfType<MailMessageItem>().ToList();

    private async void MarkReadButton_Click(object sender, RoutedEventArgs e) => await ChangeReadStateAsync(true);
    private async void MarkUnreadButton_Click(object sender, RoutedEventArgs e) => await ChangeReadStateAsync(false);

    private async Task ChangeReadStateAsync(bool isRead)
    {
        var selected = SelectedMessages();
        if (selected.Count == 0) return;
        try
        {
            using var busy = App.Busy.Begin("Обновляем статус писем…");
            await _service.SetReadAsync(_currentFolder, selected.Select(x => x.Uid), isRead);
            MailFolderCache.Invalidate(_service.AccountKey, _currentFolder);
            await LoadMessagesAsync(true);
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось изменить статус: {ex.Message}", "Почта", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectedMessages();
        if (selected.Count == 0 && _openItem is null) return;
        var menu = new ContextMenu();
        foreach (var folder in _allFolders.Where(x => !x.Equals(_currentFolder, StringComparison.OrdinalIgnoreCase)))
        {
            var item = new MenuItem { Header = folder.Equals("INBOX", StringComparison.OrdinalIgnoreCase) ? "Входящие" : folder, Tag = folder };
            item.Click += MoveToFolderMenuItem_Click;
            menu.Items.Add(item);
        }
        menu.PlacementTarget = sender as Button;
        menu.IsOpen = true;
    }

    private async void MoveToFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string destination }) return;
        await MoveSelectedAsync(destination);
    }

    private async void SpamButton_Click(object sender, RoutedEventArgs e)
    {
        var spam = FindSpecialFolder("спам", "spam", "junk");
        if (spam is not null) await MoveSelectedAsync(spam);
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var trash = FindSpecialFolder("корзина", "trash", "deleted");
        if (trash is not null) await MoveSelectedAsync(trash);
    }

    private string? FindSpecialFolder(params string[] names) => _allFolders.FirstOrDefault(folder => names.Any(name => folder.Contains(name, StringComparison.OrdinalIgnoreCase)));

    private async Task MoveSelectedAsync(string destination)
    {
        var selected = SelectedMessages().ToList();
        if (selected.Count == 0 && _openItem is not null) selected.Add(_openItem);
        if (selected.Count == 0) return;
        try
        {
            using var busy = App.Busy.Begin("Перемещаем письма…");
            await _service.MoveAsync(_currentFolder, destination, selected.Select(x => x.Uid));
            MailFolderCache.Invalidate(_service.AccountKey, _currentFolder);
            MailFolderCache.Invalidate(_service.AccountKey, destination);
            MessagePanel.Visibility = Visibility.Collapsed; WelcomePanel.Visibility = Visibility.Visible;
            await LoadMessagesAsync(true);
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось переместить письма: {ex.Message}", "Почта", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ComposeButton_Click(object sender, RoutedEventArgs e) => ShowCompose("Новое письмо", string.Empty, string.Empty, string.Empty);
    private void ReplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_openMessage is null) return;
        ShowCompose("Ответ", _openMessage.Sender, PrefixSubject("Re: ", _openMessage.Subject), $"\n\n--- Исходное письмо ---\n{_openMessage.Body}");
    }
    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (_openMessage is null) return;
        ShowCompose("Переслать", string.Empty, PrefixSubject("Fwd: ", _openMessage.Subject), $"\n\n--- Пересланное письмо ---\nОт: {_openMessage.Sender}\n{_openMessage.Body}");
    }

    private static string PrefixSubject(string prefix, string subject) => subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? subject : prefix + subject;
    private void ShowCompose(string title, string recipients, string subject, string body)
    {
        _attachedFiles.Clear(); UpdateAttachmentLabel();
        ComposeTitleTextBlock.Text = title; RecipientsTextBox.Text = recipients; SubjectTextBox.Text = subject; BodyTextBox.Text = body;
        MessagePanel.Visibility = Visibility.Collapsed; WelcomePanel.Visibility = Visibility.Collapsed; ComposePanel.Visibility = Visibility.Visible; RecipientsTextBox.Focus();
    }
    private void CancelComposeButton_Click(object sender, RoutedEventArgs e) { ComposePanel.Visibility = Visibility.Collapsed; WelcomePanel.Visibility = Visibility.Visible; }
    private void AttachFileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        _attachedFiles.AddRange(dialog.FileNames.Where(path => !_attachedFiles.Contains(path, StringComparer.OrdinalIgnoreCase)));
        UpdateAttachmentLabel();
    }
    private void UpdateAttachmentLabel() => AttachedFilesTextBlock.Text = _attachedFiles.Count == 0 ? string.Empty : $"Файлов: {_attachedFiles.Count}";

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RecipientsTextBox.Text)) { MessageBox.Show("Укажите получателя.", "Почта", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            using var busy = App.Busy.Begin("Отправляем письмо…");
            await _service.SendAsync(RecipientsTextBox.Text, SubjectTextBox.Text, BodyTextBox.Text, _attachedFiles);
            MessageBox.Show("Письмо отправлено.", "Почта", MessageBoxButton.OK, MessageBoxImage.Information);
            CancelComposeButton_Click(sender, e);
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось отправить письмо: {ex.Message}", "Почта", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async Task PrefetchFoldersAsync(IReadOnlyList<string> folders, string inbox)
    {
        try
        {
            var pending = folders.Where(x => !x.Equals(inbox, StringComparison.OrdinalIgnoreCase)).Where(x => !MailFolderCache.TryGet(_service.AccountKey, x, out _)).ToList();
            if (pending.Count == 0) return;
            var prefetched = await _service.PrefetchAsync(pending);
            foreach (var pair in prefetched) MailFolderCache.Store(_service.AccountKey, pair.Key, pair.Value);
            await Dispatcher.InvokeAsync(() => CacheStatusTextBlock.Text = "папки загружены");
        }
        catch { }
    }

    private async Task<IReadOnlyList<MailMessageItem>> ApplyInboxFiltersAsync(string sourceFolder, IReadOnlyList<MailMessageItem> messages)
    {
        var rules = MailFilterStore.Load(_userId);
        if (rules.Count == 0) return messages;
        var actions = new List<(uint Uid, string DestinationFolder, bool MarkAsRead)>();
        foreach (var message in messages)
        {
            var rule = rules.FirstOrDefault(x =>
                (string.IsNullOrWhiteSpace(x.SenderContains) || message.Sender.Contains(x.SenderContains, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(x.SubjectContains) || message.Subject.Contains(x.SubjectContains, StringComparison.OrdinalIgnoreCase)));
            if (rule is not null) actions.Add((message.Uid, rule.DestinationFolder, rule.MarkAsRead));
        }
        if (actions.Count == 0) return messages;
        await _service.ApplyFiltersAsync(sourceFolder, actions);
        var moved = actions.Where(x => !x.DestinationFolder.Equals(sourceFolder, StringComparison.OrdinalIgnoreCase)).Select(x => x.Uid).ToHashSet();
        var markedRead = actions.Where(x => x.MarkAsRead).Select(x => x.Uid).ToHashSet();
        var filteredMessages = messages.Where(x => !moved.Contains(x.Uid)).Select(x => markedRead.Contains(x.Uid) ? x with { IsUnread = false } : x).ToList();
        foreach (var destination in actions.Select(x => x.DestinationFolder).Distinct(StringComparer.OrdinalIgnoreCase)) MailFolderCache.Invalidate(_service.AccountKey, destination);
        return filteredMessages;
    }

    private int? _lastInboxUnreadCount;
    private async Task RefreshInboxSilentlyAsync()
    {
        if (!_service.IsConfigured) return;
        try
        {
            var inbox = _allFolders.FirstOrDefault(x => x.Equals("INBOX", StringComparison.OrdinalIgnoreCase)) ?? "INBOX";
            var messages = await _service.GetMessagesAsync(inbox);
            messages = await ApplyInboxFiltersAsync(inbox, messages);
            var unread = messages.Count(x => x.IsUnread);
            if (_lastInboxUnreadCount is int previous && unread > previous) SystemSounds.Asterisk.Play();
            _lastInboxUnreadCount = unread;
            MailFolderCache.Store(_service.AccountKey, inbox, messages);
            if (_currentFolder.Equals(inbox, StringComparison.OrdinalIgnoreCase))
            {
                _folderMessages = messages;
                ShowFilteredMessages();
                CacheStatusTextBlock.Text = "обновлено в фоне";
            }
        }
        catch { }
    }

    private static IReadOnlyList<MailFolderNode> BuildFolderTree(IReadOnlyList<string> folders, string inboxPath)
    {
        var inbox = new MailFolderNode("⌄  Входящие", inboxPath); var otherFolders = new List<MailFolderNode>();
        foreach (var folder in folders)
        {
            if (folder.Equals(inboxPath, StringComparison.OrdinalIgnoreCase)) continue;
            if (folder.StartsWith(inboxPath + "/", StringComparison.OrdinalIgnoreCase)) AddInboxChild(inbox, folder, inboxPath);
            else otherFolders.Add(new MailFolderNode("▱  " + folder, folder));
        }
        return new[] { inbox }.Concat(otherFolders.OrderBy(x => x.Title)).ToList();
    }
    private static void AddInboxChild(MailFolderNode root, string folderPath, string inboxPath)
    {
        var current = root; var path = inboxPath;
        foreach (var name in folderPath[(inboxPath.Length + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            path += "/" + name; var child = current.Children.FirstOrDefault(x => x.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            if (child is null) { child = new MailFolderNode("▱  " + name, path); current.Children.Add(child); } current = child;
        }
    }
    private sealed class MailFolderNode { public MailFolderNode(string title, string path) { Title = title; Path = path; } public string Title { get; } public string Path { get; } public List<MailFolderNode> Children { get; } = []; }
}
