using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System.Linq;
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

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            if (BranchComboBox.SelectedItem is not Branch branch)
            {
                MessageBox.Show("Выберите филиал.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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