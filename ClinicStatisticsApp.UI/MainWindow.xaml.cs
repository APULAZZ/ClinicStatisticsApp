using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using ClinicStatisticsApp.UI.Views;
using System.Media;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class MainWindow : Window
    {
        private readonly CurrentUserInfo _currentUser;
        private readonly HttpClient _chatHttp = new();
        private readonly System.Windows.Threading.DispatcherTimer _chatBadgeTimer = new() { Interval = TimeSpan.FromSeconds(5) };
        private int _lastUnreadCount = -1;

        public MainWindow(CurrentUserInfo currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            WorkspaceNavigator.NavigateAction = ShowWorkspaceContent;
            App.Busy.Changed += Busy_Changed;

            LoadUserInfo();
            ConfigureByRole();
            _chatHttp.BaseAddress = ChatServerEndpoint.GetBaseUri();
            _chatBadgeTimer.Tick += async (_, _) => await RefreshChatBadgeAsync();
            Loaded += async (_, _) => { await RefreshChatBadgeAsync(); _chatBadgeTimer.Start(); };
            Closed += (_, _) => { _chatBadgeTimer.Stop(); _chatHttp.Dispose(); };
        }

        private async Task RefreshChatBadgeAsync()
        {
            try
            {
                using var response = await _chatHttp.GetAsync($"/api/chat/unread/{_currentUser.UserId}");
                response.EnsureSuccessStatusCode();
                var result = await JsonSerializer.DeserializeAsync<UnreadResult>(await response.Content.ReadAsStreamAsync(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var count = result?.Count ?? 0;
                ChatButton.Content = count > 0 ? $"Чат  ({count})" : "Чат";
                if (_lastUnreadCount >= 0 && count > _lastUnreadCount) SystemSounds.Asterisk.Play();
                _lastUnreadCount = count;
            }
            catch { ChatButton.Content = "Чат"; }
        }

        private sealed class UnreadResult { public int Count { get; init; } }

        private void LoadUserInfo()
        {
            var branchText = string.IsNullOrWhiteSpace(_currentUser.BranchName)
                ? "не привязан"
                : _currentUser.BranchName;

            UserInfoTextBlock.Text =
                $"Пользователь: {RussianText.Fix(_currentUser.FullName)} | Роль: {RussianText.Fix(_currentUser.RoleName)} | Филиал: {RussianText.Fix(branchText)}";
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
                FirebirdSyncButton.Visibility = _currentUser.RoleCode == ModuleAccessPolicy.AdminRole ? Visibility.Visible : Visibility.Collapsed;
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
                FirebirdSyncButton.Visibility = Visibility.Visible;
                CallCenterDashboardButton.Visibility = Visibility.Visible;
                CallCenterJournalButton.Visibility = Visibility.Visible;
                CallCenterEmployeeStatisticsButton.Visibility = Visibility.Visible;
                CallCenterGroupStatisticsButton.Visibility = Visibility.Visible;
                CallCenterImportButton.Visibility = Visibility.Visible;
                CallCenterGoogleTablesButton.Visibility = Visibility.Visible;
                CallCenterSettingsButton.Visibility = Visibility.Visible;
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

        private void MailButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Почта";
            ShowWorkspaceContent(new MailPage(_currentUser));
        }

        private void MailSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Настройки почты";
            ShowWorkspaceContent(new MailSettingsPage(_currentUser));
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Календарь";
            ShowWorkspaceContent(new CalendarPage(_currentUser));
        }

        private void TasksButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Задачи";
            ShowWorkspaceContent(new TasksPage(_currentUser));
        }

        private void FirebirdSyncButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Импорт пациентов";
            ShowWorkspaceContent(new FirebirdSyncPage());
        }

        private void PatientsButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Пациенты CRM";
            ShowWorkspaceContent(new PatientDirectoryPage(_currentUser));
        }

        private void DuplicatesButton_Click(object sender, RoutedEventArgs e)
        {
            CallCenterPageTitleTextBlock.Text = "Проверка дублей";
            ShowWorkspaceContent(new DuplicateReviewPage(_currentUser));
        }

        private void CrmFunnelButton_Click(object sender, RoutedEventArgs e) => OpenCrmAnalytics(0, "CRM · Воронка");
        private void CrmAppointmentsButton_Click(object sender, RoutedEventArgs e) => OpenCrmAnalytics(1, "CRM · Записи");
        private void CrmFinanceButton_Click(object sender, RoutedEventArgs e) => OpenCrmAnalytics(2, "CRM · Финансы");
        private void CrmRetentionButton_Click(object sender, RoutedEventArgs e) => OpenCrmAnalytics(3, "CRM · Удержание");
        private void OpenCrmAnalytics(int tabIndex, string title)
        {
            CallCenterPageTitleTextBlock.Text = title;
            ShowWorkspaceContent(new CrmAnalyticsPage(tabIndex));
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
