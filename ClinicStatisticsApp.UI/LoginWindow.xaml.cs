using ClinicStatisticsApp.Services;
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

        private void LoginWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                PerformLogin();
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            PerformLogin();
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

        private void PerformLogin()
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

            var currentUser = _authService.Authenticate(login, password);

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
    }
}