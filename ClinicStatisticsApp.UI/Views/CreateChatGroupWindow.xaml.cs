using ClinicStatisticsApp.Models;
using System.Windows;

namespace ClinicStatisticsApp.UI.Views;

public partial class CreateChatGroupWindow : Window
{
    public string GroupTitle => TitleTextBox.Text.Trim();
    public IReadOnlyCollection<int> ParticipantIds => UsersListBox.SelectedItems.Cast<ChatPage.ChatUser>().Select(x => x.Id).ToList();
    internal CreateChatGroupWindow(CurrentUserInfo currentUser, IEnumerable<ChatPage.ChatUser> users)
    {
        InitializeComponent(); UsersListBox.ItemsSource = users.Where(x => x.Id != currentUser.UserId).ToList();
    }
    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupTitle) || ParticipantIds.Count == 0) { MessageBox.Show("Укажите название и хотя бы одного участника.", "Группа", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        DialogResult = true;
    }
}
