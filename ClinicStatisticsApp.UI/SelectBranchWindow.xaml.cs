using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class SelectBranchWindow : Window
    {
        private readonly UserService _userService = new UserService();

        public Branch? SelectedBranch { get; private set; }

        public SelectBranchWindow()
        {
            InitializeComponent();
            LoadBranches();
        }

        private void LoadBranches()
        {
            BranchComboBox.ItemsSource = _userService.GetBranches();
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorTextBlock.Text = string.Empty;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            if (BranchComboBox.SelectedItem is not Branch branch)
            {
                ShowError("Выберите филиал.");
                return;
            }

            SelectedBranch = branch;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}