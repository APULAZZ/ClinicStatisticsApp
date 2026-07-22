using System.Windows;

namespace ClinicStatisticsApp.UI.Views;

public partial class ManageChatGroupWindow : Window
{
    public string GroupTitle => TitleTextBox.Text.Trim();
    public IReadOnlyCollection<int> ParticipantIds => UsersListBox.SelectedItems.Cast<ChatPage.ChatUser>().Select(x => x.Id).ToList();

    internal ManageChatGroupWindow(string title, IEnumerable<ChatPage.ChatUser> users, IEnumerable<int> participantIds)
    {
        InitializeComponent();
        TitleTextBox.Text = title;
        var selected = participantIds.ToHashSet();
        UsersListBox.ItemsSource = users.ToList();
        foreach (var user in UsersListBox.Items.Cast<ChatPage.ChatUser>().Where(x => selected.Contains(x.Id))) UsersListBox.SelectedItems.Add(user);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GroupTitle) || ParticipantIds.Count == 0)
        {
            MessageBox.Show("Укажите название и хотя бы одного участника.", "Группа", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
