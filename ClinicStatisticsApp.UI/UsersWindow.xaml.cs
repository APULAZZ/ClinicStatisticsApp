using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class UsersWindow : Window
    {
        private readonly UserService _userService = new UserService();

        public UsersWindow()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            try
            {
                UsersDataGrid.ItemsSource = _userService.GetAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка загрузки пользователей", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var user = new User
            {
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var editWindow = new UserEditWindow(user)
            {
                Owner = this
            };

            if (editWindow.ShowDialog() == true)
            {
                try
                {
                    _userService.Add(user);
                    LoadUsers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка добавления пользователя", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is not User selectedUser)
            {
                MessageBox.Show("Выберите пользователя.", "Редактирование", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userFromDb = _userService.GetById(selectedUser.Id);
            if (userFromDb == null)
            {
                MessageBox.Show("Пользователь не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var editWindow = new UserEditWindow(userFromDb)
            {
                Owner = this
            };

            if (editWindow.ShowDialog() == true)
            {
                try
                {
                    _userService.Update(userFromDb);
                    LoadUsers();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Ошибка редактирования пользователя", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EnableButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeActive(true);
        }

        private void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            ChangeActive(false);
        }

        private void ChangeActive(bool isActive)
        {
            if (UsersDataGrid.SelectedItem is not User selectedUser)
            {
                MessageBox.Show("Выберите пользователя.", "Изменение статуса", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _userService.SetActive(selectedUser.Id, isActive);
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка изменения статуса", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is not User selectedUser)
            {
                MessageBox.Show("Выберите пользователя.", "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Удалить пользователя '{selectedUser.Login}'?",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                _userService.Delete(selectedUser.Id);
                LoadUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось удалить пользователя. Возможно, он уже используется в отчетах или журнале.\n\n" + ex.Message,
                    "Ошибка удаления",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }
    }
}