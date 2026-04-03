using ClinicStatisticsApp.Models;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class MainWindow : Window
    {
        private readonly CurrentUserInfo _currentUser;

        public MainWindow(CurrentUserInfo currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;

            LoadUserInfo();
            ConfigureByRole();
        }

        private void LoadUserInfo()
        {
            var branchText = string.IsNullOrWhiteSpace(_currentUser.BranchName)
                ? "не привязан"
                : _currentUser.BranchName;

            UserInfoTextBlock.Text =
                $"Пользователь: {_currentUser.FullName} | Роль: {_currentUser.RoleName} | Филиал: {branchText}";
        }

        private void ConfigureByRole()
        {
            if (_currentUser.RoleCode == "Admin")
            {
                WelcomeTextBlock.Text =
                    "Вы вошли как администратор.\n\n" +
                    "Доступны справочники, пользователи, все филиалы и дальнейшая настройка системы.";

                ReportsButton.IsEnabled = true;
                NaradButton.IsEnabled = true;
                SummaryButton.IsEnabled = true;
                EmployeesButton.IsEnabled = true;
                UsersButton.IsEnabled = true;
            }
            else if (_currentUser.RoleCode == "Manager")
            {
                WelcomeTextBlock.Text =
                    "Вы вошли как руководитель.\n\n" +
                    "Доступны все филиалы, сводная книга, просмотр и экспорт отчетов.";

                ReportsButton.IsEnabled = true;
                NaradButton.IsEnabled = true;
                SummaryButton.IsEnabled = true;
                EmployeesButton.IsEnabled = false;
                UsersButton.IsEnabled = false;
            }
            else if (_currentUser.RoleCode == "BranchUser")
            {
                WelcomeTextBlock.Text =
                    $"Вы вошли как пользователь филиала.\n\n" +
                    $"Ваш филиал: {_currentUser.BranchName}.\n" +
                    "Доступны только отчеты и наряды своего филиала.";

                ReportsButton.IsEnabled = true;
                NaradButton.IsEnabled = true;
                SummaryButton.IsEnabled = false;
                EmployeesButton.IsEnabled = false;
                UsersButton.IsEnabled = false;
            }
            else
            {
                WelcomeTextBlock.Text = "Роль пользователя не определена.";

                ReportsButton.IsEnabled = false;
                NaradButton.IsEnabled = false;
                SummaryButton.IsEnabled = false;
                EmployeesButton.IsEnabled = false;
                UsersButton.IsEnabled = false;
            }
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.RoleCode == "BranchUser")
            {
                var window = new BranchReportWindow(_currentUser)
                {
                    Owner = this
                };

                window.ShowDialog();
                return;
            }

            if (_currentUser.RoleCode == "Admin" || _currentUser.RoleCode == "Manager")
            {
                var selectBranchWindow = new SelectBranchWindow
                {
                    Owner = this
                };

                if (selectBranchWindow.ShowDialog() == true && selectBranchWindow.SelectedBranch != null)
                {
                    var context = new SelectedBranchContext
                    {
                        CurrentUser = _currentUser,
                        BranchId = selectBranchWindow.SelectedBranch.Id,
                        BranchName = selectBranchWindow.SelectedBranch.Name
                    };

                    var window = new BranchReportWindow(context)
                    {
                        Owner = this
                    };

                    window.ShowDialog();
                }

                return;
            }

            MessageBox.Show("Для вашей роли работа с филиальными отчетами недоступна.",
                "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void NaradButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.RoleCode == "BranchUser")
            {
                var window = new NaradWindow(_currentUser)
                {
                    Owner = this
                };

                window.ShowDialog();
                return;
            }

            if (_currentUser.RoleCode == "Admin" || _currentUser.RoleCode == "Manager")
            {
                var selectBranchWindow = new SelectBranchWindow
                {
                    Owner = this
                };

                if (selectBranchWindow.ShowDialog() == true && selectBranchWindow.SelectedBranch != null)
                {
                    var contextUser = new CurrentUserInfo
                    {
                        UserId = _currentUser.UserId,
                        Login = _currentUser.Login,
                        FullName = _currentUser.FullName,
                        RoleCode = _currentUser.RoleCode,
                        RoleName = _currentUser.RoleName,
                        BranchId = selectBranchWindow.SelectedBranch.Id,
                        BranchName = selectBranchWindow.SelectedBranch.Name
                    };

                    var window = new NaradWindow(contextUser)
                    {
                        Owner = this
                    };

                    window.ShowDialog();
                }

                return;
            }

            MessageBox.Show("Для вашей роли работа с нарядами недоступна.",
                "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SummaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.RoleCode == "BranchUser")
            {
                MessageBox.Show("У вас нет доступа к сводной книге.",
                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new SummaryBookWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void EmployeesButton_Click(object sender, RoutedEventArgs e)
        {
            var employeesWindow = new EmployeesWindow
            {
                Owner = this
            };

            employeesWindow.ShowDialog();
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            var usersWindow = new UsersWindow
            {
                Owner = this
            };

            usersWindow.ShowDialog();
        }
    }
}