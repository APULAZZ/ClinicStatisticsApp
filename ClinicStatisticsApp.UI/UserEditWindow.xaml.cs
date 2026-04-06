using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class UserEditWindow : Window
    {
        private readonly UserService _userService = new UserService();

        public User User { get; private set; }

        public UserEditWindow(User user)
        {
            InitializeComponent();

            User = user ?? throw new System.ArgumentNullException(nameof(user));

            LoadReferences();
            FillForm();
        }

        private void LoadReferences()
        {
            RoleComboBox.ItemsSource = _userService.GetRoles();
            BranchComboBox.ItemsSource = _userService.GetBranches();
        }

        private void FillForm()
        {
            LoginTextBox.Text = User.Login;
            PasswordTextBox.Text = User.PasswordHash;
            FullNameTextBox.Text = User.FullName;
            RoleComboBox.SelectedValue = User.RoleId;
            BranchComboBox.SelectedValue = User.BranchId;
            IsActiveCheckBox.IsChecked = User.IsActive;

            UpdateBranchAvailability();
        }

        private void RoleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBranchAvailability();
        }

        private void UpdateBranchAvailability()
        {
            if (RoleComboBox.SelectedItem is not Role selectedRole)
                return;

            var needsBranch = selectedRole.Code == "BranchUser";

            BranchComboBox.IsEnabled = needsBranch;

            if (!needsBranch)
            {
                BranchComboBox.SelectedIndex = -1;
            }
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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            var login = LoginTextBox.Text.Trim();
            var password = PasswordTextBox.Text.Trim();
            var fullName = FullNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(login))
            {
                ShowError("Введите логин.");
                LoginTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите пароль.");
                PasswordTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ShowError("Введите ФИО.");
                FullNameTextBox.Focus();
                return;
            }

            if (RoleComboBox.SelectedItem is not Role selectedRole)
            {
                ShowError("Выберите роль.");
                RoleComboBox.Focus();
                return;
            }

            int? branchId = null;

            if (selectedRole.Code == "BranchUser")
            {
                if (BranchComboBox.SelectedItem is not Branch selectedBranch)
                {
                    ShowError("Для пользователя филиала нужно выбрать филиал.");
                    BranchComboBox.Focus();
                    return;
                }

                branchId = selectedBranch.Id;
            }

            User.Login = login;
            User.PasswordHash = password;
            User.FullName = fullName;
            User.RoleId = selectedRole.Id;
            User.BranchId = branchId;
            User.IsActive = IsActiveCheckBox.IsChecked == true;

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