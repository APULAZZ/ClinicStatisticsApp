using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using ClinicStatisticsApp.UI.Views;
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
            WorkspaceNavigator.NavigateAction = ShowWorkspaceContent;
            App.Busy.Changed += Busy_Changed;

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
            if (ModuleAccessPolicy.CanUseCallCenter(_currentUser.RoleCode) && !ModuleAccessPolicy.CanUseGeneralStatistics(_currentUser.RoleCode))
            {
                WelcomeTextBlock.Text =
                    "Вы вошли в раздел коллцентра.";

                NavigationTitleTextBlock.Text = "Коллцентр";
                MedicalHeaderPanel.Visibility = Visibility.Collapsed;
                CallCenterPageTitleTextBlock.Visibility = Visibility.Visible;
                ReportsButton.Visibility = Visibility.Collapsed;
                NaradButton.Visibility = Visibility.Collapsed;
                SummaryButton.Visibility = Visibility.Collapsed;
                EmployeesButton.Visibility = Visibility.Collapsed;
                UsersButton.Visibility = Visibility.Collapsed;
                CallCenterDashboardButton.Visibility = Visibility.Visible;
                CallCenterJournalButton.Visibility = Visibility.Visible;
                CallCenterEmployeeStatisticsButton.Visibility = Visibility.Visible;
                CallCenterGroupStatisticsButton.Visibility = Visibility.Visible;
                CallCenterImportButton.Visibility = Visibility.Visible;
                CallCenterGoogleTablesButton.Visibility = Visibility.Visible;
                CallCenterSettingsButton.Visibility = ModuleAccessPolicy.CanManageCallCenter(_currentUser.RoleCode)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                OpenCallCenterJournal();
                return;
            }

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
            else if (_currentUser.RoleCode is "Manager" or ModuleAccessPolicy.StatisticsRole)
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
                ShowWorkspaceContent(new BranchReportWindow(_currentUser));
                return;
            }

            if (_currentUser.RoleCode is "Admin" or "Manager" or ModuleAccessPolicy.StatisticsRole)
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

                    ShowWorkspaceContent(new BranchReportWindow(context));
                }

                return;
            }

            MessageBox.Show(
                "Для вашей роли работа с филиальными отчетами недоступна.",
                "Доступ запрещен",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void NaradButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.RoleCode == "BranchUser")
            {
                ShowWorkspaceContent(new NaradWindow(_currentUser));
                return;
            }

            if (_currentUser.RoleCode is "Admin" or "Manager" or ModuleAccessPolicy.StatisticsRole)
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

                    ShowWorkspaceContent(new NaradWindow(contextUser));
                }

                return;
            }

            MessageBox.Show(
                "Для вашей роли работа с нарядами недоступна.",
                "Доступ запрещен",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void SummaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser.RoleCode == "BranchUser")
            {
                MessageBox.Show(
                    "У вас нет доступа к сводной книге.",
                    "Доступ запрещен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            ShowWorkspaceContent(new SummaryBookWindow());
        }

        private void EmployeesButton_Click(object sender, RoutedEventArgs e)
        {
            ShowWorkspaceContent(new EmployeesWindow());
        }

        private void UsersButton_Click(object sender, RoutedEventArgs e)
        {
            ShowWorkspaceContent(new UsersWindow());
        }

        private void CallCenterJournalButton_Click(object sender, RoutedEventArgs e)
        {
            OpenCallCenterJournal();
        }

        private void ChatButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Чат";
            ShowWorkspaceContent(new ChatPage(_currentUser));
        }

        private void CallCenterDashboardButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Обзор";
            ShowWorkspaceContent(new CallCenterDashboardPage());
        }

        private void OpenCallCenterJournal()
        {
            CallCenterPageTitleTextBlock.Text = "Журнал звонков";
            ShowWorkspaceContent(new CallCenterJournalPage());
        }

        private void CallCenterEmployeeStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenCallCenterStatistics(false);
        }

        private void CallCenterGroupStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenCallCenterStatistics(true);
        }

        private async void CallCenterGoogleTablesButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Гугл-таблички";
            var page = new CallCenterGoogleTablesPage();
            ShowWorkspaceContent(page);
            await page.LoadAsync();
        }

        private void CallCenterImportButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Импорт из Mango";
            ShowWorkspaceContent(new CallCenterImportPage());
        }

        private void CallCenterSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Настройки API";
            ShowWorkspaceContent(new CallCenterSettingsPage());
        }

        private void OpenCallCenterStatistics(bool isGroupStatistics)
        {
            CallCenterPageTitleTextBlock.Text = isGroupStatistics ? "Статистика групп" : "Статистика сотрудников";
            ShowWorkspaceContent(new CallCenterStatisticsPage(isGroupStatistics));
        }

        private void ShowWorkspaceContent(object? content)
        {
            if (content == null)
            {
                CallCenterContentControl.Content = null;
                CallCenterContentControl.Visibility = Visibility.Collapsed;
                StartContentGrid.Visibility = Visibility.Visible;
                return;
            }

            StartContentGrid.Visibility = Visibility.Collapsed;
            CallCenterContentControl.Visibility = Visibility.Visible;
            CallCenterContentControl.Content = content;
        }

        private void Busy_Changed(object? sender, BusyChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                BusyOverlay.Visibility = e.IsBusy ? Visibility.Visible : Visibility.Collapsed;
                BusyMessageText.Text = e.Message;
            });
        }
    }
}
