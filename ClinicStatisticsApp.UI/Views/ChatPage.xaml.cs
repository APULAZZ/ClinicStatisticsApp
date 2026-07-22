using ClinicStatisticsApp.Models;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ClinicStatisticsApp.UI.Views;

public partial class ChatPage : UserControl
{
    private readonly CurrentUserInfo _currentUser;
    private readonly HttpClient _http;
    private readonly List<PendingAttachment> _pendingAttachments = [];
    private List<ChatUser> _users = [];
    private int? _conversationId;
    private readonly System.Windows.Threading.DispatcherTimer _refreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public ChatPage(CurrentUserInfo currentUser)
    {
        InitializeComponent();
        _currentUser = currentUser;
        _http = new HttpClient { BaseAddress = ChatServerEndpoint.GetBaseUri() };
        CreateGroupButton.Visibility = string.Equals(_currentUser.RoleCode, "Admin", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        Loaded += async (_, _) => { await LoadUsersAsync(); _refreshTimer.Start(); };
        Unloaded += (_, _) => _refreshTimer.Stop();
        _refreshTimer.Tick += async (_, _) => { if (_conversationId.HasValue) await LoadMessagesAsync(); await LoadConversationsAsync(); };
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private async Task LoadUsersAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/api/chat/users");
            response.EnsureSuccessStatusCode();
            _users = await JsonSerializer.DeserializeAsync<List<ChatUser>>(await response.Content.ReadAsStreamAsync(), JsonOptions) ?? [];
            _users.RemoveAll(x => x.Id == _currentUser.UserId);
            SearchResultsListBox.ItemsSource = _users;
            ChatTitleTextBlock.Text = "Выберите пользователя слева, чтобы начать личный чат.";
            SearchHintTextBlock.Visibility = _users.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            SearchHintTextBlock.Text = _users.Count == 0 ? "Аккаунты для поиска не найдены." : string.Empty;
            await LoadConversationsAsync();
        }
        catch
        {
            ChatTitleTextBlock.Text = "Сервер чата пока не запущен.";
            SearchResultsBorder.Visibility = Visibility.Visible;
            SearchHintTextBlock.Visibility = Visibility.Visible;
            SearchHintTextBlock.Text = "Нет подключения к серверу чата. Запустите ClinicStatisticsApp.ChatServer на компьютере-сервере.";
        }
    }

    private async void UserSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_users.Count == 0) await LoadUsersAsync();
        var text = UserSearchTextBox.Text.Trim();
        var matches = string.IsNullOrWhiteSpace(text) ? _users : _users.Where(x => x.DisplayName.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Login.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
        SearchResultsListBox.ItemsSource = matches;
        SearchHintTextBlock.Visibility = matches.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (matches.Count == 0 && _users.Count > 0) SearchHintTextBlock.Text = "Совпадений не найдено.";
        SearchResultsBorder.Visibility = Visibility.Visible;
    }

    private void UserSearchTextBox_GotFocus(object sender, RoutedEventArgs e) => SearchResultsBorder.Visibility = _users.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    private async void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsListBox.SelectedItem is not ChatUser user) return;
        try
        {
            var body = JsonSerializer.Serialize(new { userId = _currentUser.UserId, otherUserId = user.Id });
            using var response = await _http.PostAsync("/api/chat/direct", new StringContent(body, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var result = await JsonSerializer.DeserializeAsync<ConversationResult>(await response.Content.ReadAsStreamAsync(), JsonOptions);
            _conversationId = result?.Id;
            ChatTitleTextBlock.Text = user.DisplayName;
            ManageGroupButton.Visibility = Visibility.Collapsed;
            UserSearchTextBox.Clear(); SearchResultsBorder.Visibility = Visibility.Collapsed;
            EnableComposer(); ClearPendingAttachments();
            await LoadMessagesAsync(); await LoadConversationsAsync();
        }
        catch { ChatTitleTextBlock.Text = "Не удалось открыть диалог: сервер чата недоступен."; }
    }

    private async Task LoadMessagesAsync()
    {
        if (!_conversationId.HasValue) return;
        try
        {
            using var response = await _http.GetAsync($"/api/chat/conversations/{_conversationId}/messages?userId={_currentUser.UserId}");
            response.EnsureSuccessStatusCode();
            var messages = await JsonSerializer.DeserializeAsync<List<ChatMessage>>(await response.Content.ReadAsStreamAsync(), JsonOptions) ?? [];
            foreach (var message in messages) message.IsOwn = message.SenderUserId == _currentUser.UserId;
            MessagesListBox.ItemsSource = messages;
            if (messages.Count > 0) MessagesListBox.ScrollIntoView(messages[^1]);
            await MarkAsReadAsync();
        }
        catch { }
    }

    private async Task MarkAsReadAsync()
    {
        if (!_conversationId.HasValue) return;
        try
        {
            var body = JsonSerializer.Serialize(new { userId = _currentUser.UserId });
            using var response = await _http.PostAsync($"/api/chat/conversations/{_conversationId}/read", new StringContent(body, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }
        catch { }
    }

    private async Task LoadConversationsAsync()
    {
        try
        {
            using var response = await _http.GetAsync($"/api/chat/conversations/{_currentUser.UserId}");
            response.EnsureSuccessStatusCode();
            var chats = await JsonSerializer.DeserializeAsync<List<ChatConversation>>(await response.Content.ReadAsStreamAsync(), JsonOptions) ?? [];
            ConversationsListBox.ItemsSource = chats;
        }
        catch { }
    }

    private async void ConversationsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConversationsListBox.SelectedItem is not ChatConversation chat) return;
        _conversationId = chat.Id; ChatTitleTextBlock.Text = chat.DisplayTitle;
        ManageGroupButton.Visibility = chat.IsGroup && string.Equals(_currentUser.RoleCode, "Admin", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        EnableComposer(); ClearPendingAttachments();
        await LoadMessagesAsync(); await LoadConversationsAsync();
    }

    private void EnableComposer() => MessageTextBox.IsEnabled = AddAttachmentButton.IsEnabled = SendButton.IsEnabled = _conversationId.HasValue;

    private async void ManageGroupButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_conversationId.HasValue || ConversationsListBox.SelectedItem is not ChatConversation chat || !chat.IsGroup) return;
        try
        {
            using var response = await _http.GetAsync($"/api/chat/groups/{chat.Id}?userId={_currentUser.UserId}");
            response.EnsureSuccessStatusCode();
            var group = await JsonSerializer.DeserializeAsync<GroupInfo>(await response.Content.ReadAsStreamAsync(), JsonOptions);
            if (group is null) return;
            var window = new ManageChatGroupWindow(group.Title, _users, group.ParticipantIds) { Owner = Window.GetWindow(this) };
            if (window.ShowDialog() != true) return;
            var body = JsonSerializer.Serialize(new { updatedByUserId = _currentUser.UserId, title = window.GroupTitle, participantIds = window.ParticipantIds });
            using var updateResponse = await _http.PutAsync($"/api/chat/groups/{chat.Id}", new StringContent(body, Encoding.UTF8, "application/json"));
            updateResponse.EnsureSuccessStatusCode();
            ChatTitleTextBlock.Text = window.GroupTitle;
            await LoadConversationsAsync();
        }
        catch { MessageBox.Show("Не удалось обновить группу.", "Чат", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void CreateGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new CreateChatGroupWindow(_currentUser, _users) { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() != true) return;
        try
        {
            var body = JsonSerializer.Serialize(new { createdByUserId = _currentUser.UserId, title = window.GroupTitle, participantIds = window.ParticipantIds });
            using var response = await _http.PostAsync("/api/chat/groups", new StringContent(body, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var created = await JsonSerializer.DeserializeAsync<ConversationResult>(await response.Content.ReadAsStreamAsync(), JsonOptions);
            _conversationId = created?.Id; ChatTitleTextBlock.Text = window.GroupTitle; EnableComposer();
            ManageGroupButton.Visibility = Visibility.Visible;
            await LoadConversationsAsync();
        }
        catch { MessageBox.Show("Не удалось создать групповой чат.", "Чат", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void AddAttachmentButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Title = "Выберите файлы для отправки" };
        if (dialog.ShowDialog() != true) return;
        foreach (var file in dialog.FileNames)
        {
            var info = new FileInfo(file);
            if (info.Length > 25 * 1024 * 1024) { MessageBox.Show($"Файл «{info.Name}» больше 25 МБ и не будет добавлен.", "Чат", MessageBoxButton.OK, MessageBoxImage.Information); continue; }
            _pendingAttachments.Add(new PendingAttachment(info.Name, File.ReadAllBytes(file), GetContentType(info.Extension)));
        }
        UpdatePendingAttachmentsCaption();
    }

    private void MessageTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control || !Clipboard.ContainsImage()) return;
        var image = Clipboard.GetImage();
        if (image is null) return;
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream(); encoder.Save(stream);
        _pendingAttachments.Add(new PendingAttachment($"Снимок экрана {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png", stream.ToArray(), "image/png"));
        UpdatePendingAttachmentsCaption();
        e.Handled = true;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendAsync();
    private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; await SendAsync(); } }

    private async Task SendAsync()
    {
        if (!_conversationId.HasValue || (string.IsNullOrWhiteSpace(MessageTextBox.Text) && _pendingAttachments.Count == 0)) return;
        try
        {
            using var body = new MultipartFormDataContent();
            body.Add(new StringContent(_conversationId.Value.ToString()), "conversationId");
            body.Add(new StringContent(_currentUser.UserId.ToString()), "senderUserId");
            body.Add(new StringContent(MessageTextBox.Text ?? string.Empty), "text");
            foreach (var attachment in _pendingAttachments)
            {
                var content = new ByteArrayContent(attachment.Bytes); content.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType);
                body.Add(content, "files", attachment.FileName);
            }
            using var response = await _http.PostAsync("/api/chat/messages", body);
            response.EnsureSuccessStatusCode();
            MessageTextBox.Clear(); ClearPendingAttachments(); await LoadMessagesAsync(); await LoadConversationsAsync();
        }
        catch { MessageBox.Show("Не удалось отправить сообщение или вложение.", "Чат", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void AttachmentButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: long id } button) return;
        var attachment = button.DataContext as ChatAttachment;
        if (attachment is null) return;
        try
        {
            using var response = await _http.GetAsync($"/api/chat/attachments/{id}?userId={_currentUser.UserId}"); response.EnsureSuccessStatusCode();
            if (attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var image = await response.Content.ReadAsByteArrayAsync();
                new ImagePreviewWindow(attachment.FileName, image) { Owner = Window.GetWindow(this) }.ShowDialog();
                return;
            }
            var dialog = new SaveFileDialog { FileName = attachment.FileName, Title = "Сохранить вложение" };
            if (dialog.ShowDialog() != true) return;
            await using var target = File.Create(dialog.FileName); await response.Content.CopyToAsync(target);
            Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
        }
        catch { MessageBox.Show("Не удалось загрузить вложение.", "Чат", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void DeleteMessageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: long id }) return;
        if (MessageBox.Show("Удалить это сообщение и все вложения к нему?", "Чат", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        try
        {
            using var response = await _http.DeleteAsync($"/api/chat/messages/{id}?userId={_currentUser.UserId}");
            response.EnsureSuccessStatusCode();
            await LoadMessagesAsync(); await LoadConversationsAsync();
        }
        catch { MessageBox.Show("Не удалось удалить сообщение.", "Чат", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ClearPendingAttachments() { _pendingAttachments.Clear(); UpdatePendingAttachmentsCaption(); }
    private void UpdatePendingAttachmentsCaption() => PendingAttachmentsTextBlock.Text = _pendingAttachments.Count == 0 ? string.Empty : $"Вложения: {string.Join(", ", _pendingAttachments.Select(x => x.FileName))}";
    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif", ".pdf" => "application/pdf", _ => "application/octet-stream" };

    internal sealed class ChatUser { public int Id { get; init; } public string FullName { get; init; } = string.Empty; public string Login { get; init; } = string.Empty; public string DisplayName => RussianText.Fix(FullName); }
    private sealed class ConversationResult { public int Id { get; init; } }
    private sealed class GroupInfo { public string Title { get; init; } = string.Empty; public List<int> ParticipantIds { get; init; } = []; }
    private sealed class ChatConversation { public int Id { get; init; } public string Title { get; init; } = string.Empty; public bool IsGroup { get; init; } public int UnreadCount { get; init; } public string DisplayTitle => RussianText.Fix(Title); public Visibility UnreadVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed; }
    private sealed class ChatMessage { public long Id { get; init; } public int SenderUserId { get; init; } public string SenderName { get; init; } = string.Empty; public string Text { get; init; } = string.Empty; public DateTime SentAt { get; init; } public List<ChatAttachment> Attachments { get; init; } = []; public bool IsOwn { get; set; } public HorizontalAlignment BubbleAlignment => IsOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left; public string BubbleBackground => IsOwn ? "#DCEBFF" : "#FFFFFF"; public string SenderColor => IsOwn ? "#1D4ED8" : "#475569"; public string SenderCaption => IsOwn ? "Вы" : RussianText.Fix(SenderName); public string TimestampText => SentAt.ToLocalTime().ToString("dd.MM HH:mm"); public Visibility DeleteVisibility => IsOwn ? Visibility.Visible : Visibility.Collapsed; }
    private sealed class ChatAttachment { public long Id { get; init; } public string FileName { get; init; } = string.Empty; public string ContentType { get; init; } = string.Empty; public long Length { get; init; } public string DisplayName => $"📎 {FileName}"; }
    private sealed record PendingAttachment(string FileName, byte[] Bytes, string ContentType);
}
