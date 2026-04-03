using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class EmployeesWindow : Window
    {
        private readonly EmployeeService _employeeService = new EmployeeService();

        public EmployeesWindow()
        {
            InitializeComponent();
            LoadEmployees();
        }

        private void LoadEmployees()
        {
            try
            {
                EmployeesDataGrid.ItemsSource = _employeeService.GetAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка загрузки сотрудников", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var newEmployee = new Employee
            {
                IsActive = true,
                IsCallCenter = false,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            var editWindow = new EmployeeEditWindow(newEmployee)
            {
                Owner = this
            };

            if (editWindow.ShowDialog() == true)
            {
                try
                {
                    _employeeService.Add(newEmployee);
                    LoadEmployees();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Ошибка добавления сотрудника", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeesDataGrid.SelectedItem is not Employee selectedEmployee)
            {
                MessageBox.Show("Выберите сотрудника.", "Редактирование", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var employeeFromDb = _employeeService.GetById(selectedEmployee.Id);
            if (employeeFromDb == null)
            {
                MessageBox.Show("Сотрудник не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var editWindow = new EmployeeEditWindow(employeeFromDb)
            {
                Owner = this
            };

            if (editWindow.ShowDialog() == true)
            {
                try
                {
                    _employeeService.Update(employeeFromDb);
                    LoadEmployees();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Ошибка редактирования сотрудника", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadEmployees();
        }
    }
}