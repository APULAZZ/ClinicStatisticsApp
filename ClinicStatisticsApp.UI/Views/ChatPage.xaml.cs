using ClinicStatisticsApp.Models;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ClinicStatisticsApp.UI.Views;

public partial class ChatPage : UserControl
{
    private readonly CurrentUserInfo _currentUser;
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5088") };
    private List<ChatUser> _users = [];
    private int? _conversationId;

    public ChatPage(CurrentUserInfo currentUser)
    {
        InitializeComponent();
        _currentUser = currentUser;
        Loaded += async (_, _) => await LoadUsersAsync();
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            using var response = await _http.GetAsync("/api/chat/users");
            response.EnsureSuccessStatusCode();
            _users = await JsonSerializer.DeserializeAsync<List<ChatUser>>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            _users.RemoveAll(x => x.Id == _currentUser.UserId);
            ChatsListBox.ItemsSource = _users;
            ChatsListBox.DisplayMemberPath = nameof(ChatUser.FullName);
            ChatTitleTextBlock.Text = "Выберите пользователя слева, чтобы начать личный чат.";
        }
        catch
        {
            ChatTitleTextBlock.Text = "Локальный сервер чата пока не запущен.";
        }
    }

    private void UserSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = UserSearchTextBox.Text.Trim();
        ChatsListBox.ItemsSource = string.IsNullOrWhiteSpace(text) ? _users : _users.Where(x => x.FullName.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Login.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async void ChatsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChatsListBox.SelectedItem is not ChatUser user) return;
        ChatTitleTextBlock.Text = user.FullName;
        try
        {
            var body = JsonSerializer.Serialize(new { userId = _currentUser.UserId, otherUserId = user.Id });
            using var response = await _http.PostAsync("/api/chat/direct", new StringContent(body, Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
            var result = await JsonSerializer.DeserializeAsync<ConversationResult>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _conversationId = result?.Id;
            MessageTextBox.IsEnabled = SendButton.IsEnabled = _conversationId.HasValue;
            await LoadMessagesAsync();
        }
        catch { ChatTitleTextBlock.Text = "Не удалось открыть диалог: сервер чата недоступен."; }
    }

    private async Task LoadMessagesAsync()
    {
        if (!_conversationId.HasValue) return;
        using var response = await _http.GetAsync($"/api/chat/conversations/{_conversationId}/messages");
        response.EnsureSuccessStatusCode();
        var messages = await JsonSerializer.DeserializeAsync<List<ChatMessage>>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        MessagesListBox.ItemsSource = messages;
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e) => await SendAsync();
    private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { e.Handled = true; await SendAsync(); } }
    private async Task SendAsync()
    {
        if (!_conversationId.HasValue || string.IsNullOrWhiteSpace(MessageTextBox.Text)) return;
        var body = JsonSerializer.Serialize(new { conversationId = _conversationId.Value, senderUserId = _currentUser.UserId, text = MessageTextBox.Text });
        using var response = await _http.PostAsync("/api/chat/messages", new StringContent(body, Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode(); MessageTextBox.Clear(); await LoadMessagesAsync();
    }

    private sealed class ChatUser { public int Id { get; init; } public string FullName { get; init; } = string.Empty; public string Login { get; init; } = string.Empty; }
    private sealed class ConversationResult { public int Id { get; init; } }
    private sealed class ChatMessage { public int SenderUserId { get; init; } public string Text { get; init; } = string.Empty; public DateTime SentAt { get; init; } public string Display => $"{SentAt:dd.MM HH:mm}  {Text}"; }
}
