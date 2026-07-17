using ClinicStatisticsApp.Services;
using ClinicStatisticsApp.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ClinicStatisticsApp.UI
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService = new AuthService();

        public LoginWindow()
        {
            InitializeComponent();

            KeyDown += LoginWindow_KeyDown;
            Loaded += (_, _) => LoginTextBox.Focus();
        }

        private async void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await PerformLoginAsync();
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformLoginAsync();
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

        private async Task PerformLoginAsync()
        {
            HideError();

            var login = LoginTextBox.Text.Trim();
            var password = UserPasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login))
            {
                ShowError("Введите логин.");
                LoginTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите пароль.");
                UserPasswordBox.Focus();
                return;
            }

            SetLoginInProgress(true);

            CurrentUserInfo? currentUser;
            try
            {
                currentUser = await Task.Run(() => _authService.Authenticate(login, password));
            }
            catch (Exception)
            {
                ShowError("Не удалось подключиться к базе данных. Проверьте соединение с сервером и повторите попытку.");
                return;
            }
            finally
            {
                SetLoginInProgress(false);
            }

            if (currentUser == null)
            {
                ShowError("Неверный логин или пароль.");
                UserPasswordBox.Focus();
                return;
            }

            var mainWindow = new MainWindow(currentUser);
            mainWindow.Show();

            Close();
        }

        private void SetLoginInProgress(bool isInProgress)
        {
            LoginButton.IsEnabled = !isInProgress;
            LoginButton.Content = isInProgress ? "Проверяем..." : "Войти";
            LoginTextBox.IsEnabled = !isInProgress;
            UserPasswordBox.IsEnabled = !isInProgress;
        }
    }
}
