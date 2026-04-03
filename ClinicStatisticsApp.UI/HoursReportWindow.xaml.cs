using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class HoursReportWindow : Window
    {
        private readonly CurrentUserInfo _currentUser;
        private readonly HoursReportService _hoursReportService = new HoursReportService();
        private readonly CopyEmployeesFromPreviousMonthService _copyService = new CopyEmployeesFromPreviousMonthService();

        private ObservableCollection<HoursEntryViewModel> _items = new();
        public ObservableCollection<Employee> Employees { get; set; } = new();

        public HoursReportWindow(CurrentUserInfo currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;

            if (_currentUser.BranchId == null)
            {
                MessageBox.Show("Для текущего пользователя не задан филиал.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            LoadPeriods();
            LoadEmployees();

            HoursDataGrid.ItemsSource = _items;
        }

        private void LoadPeriods()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 5; year <= currentYear + 2; year++)
            {
                YearComboBox.Items.Add(year);
            }

            YearComboBox.SelectedItem = currentYear;

            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Январь", Tag = 1 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Февраль", Tag = 2 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Март", Tag = 3 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Апрель", Tag = 4 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Май", Tag = 5 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Июнь", Tag = 6 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Июль", Tag = 7 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Август", Tag = 8 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Сентябрь", Tag = 9 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Октябрь", Tag = 10 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Ноябрь", Tag = 11 });
            MonthComboBox.Items.Add(new ComboBoxItem { Content = "Декабрь", Tag = 12 });

            MonthComboBox.SelectedIndex = DateTime.Now.Month - 1;
        }

        private void CopyFromPreviousButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentUser.BranchId == null)
                    return;

                var result = MessageBox.Show(
                    "Скопировать сотрудников из предыдущего месяца? Текущий список сотрудников в блоке ЧАСЫ будет заменен.",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                _copyService.CopyHoursEmployees(_currentUser.BranchId.Value, SelectedYear, SelectedMonth, _currentUser.UserId);
                LoadData();

                MessageBox.Show("Сотрудники скопированы.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка копирования", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadEmployees()
        {
            Employees = new ObservableCollection<Employee>(_hoursReportService.GetActiveEmployees());
            DataContext = this;
        }

        private int SelectedYear => (int)(YearComboBox.SelectedItem ?? DateTime.Now.Year);

        private int SelectedMonth
        {
            get
            {
                if (MonthComboBox.SelectedItem is ComboBoxItem item && item.Tag is int month)
                    return month;

                return DateTime.Now.Month;
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            if (_currentUser.BranchId == null)
                return;

            var data = _hoursReportService.GetHoursEntries(_currentUser.BranchId.Value, SelectedYear, SelectedMonth, _currentUser.UserId);

            _items = new ObservableCollection<HoursEntryViewModel>(data);
            HoursDataGrid.ItemsSource = _items;

            RecalculateTotals();
        }

        private void AddRowButton_Click(object sender, RoutedEventArgs e)
        {
            _items.Add(new HoursEntryViewModel());
            RecalculateTotals();
        }

        private void DeleteRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (HoursDataGrid.SelectedItem is HoursEntryViewModel selected)
            {
                _items.Remove(selected);
                RecalculateTotals();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentUser.BranchId == null)
                    return;

                var invalidRows = _items.Where(i => i.EmployeeId <= 0).ToList();
                if (invalidRows.Any())
                {
                    MessageBox.Show("Во всех строках должен быть выбран сотрудник.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var duplicateEmployees = _items
                    .GroupBy(i => i.EmployeeId)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (duplicateEmployees.Any())
                {
                    MessageBox.Show("Один и тот же сотрудник не может повторяться в блоке ЧАСЫ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _hoursReportService.SaveHoursEntries(_currentUser.BranchId.Value, SelectedYear, SelectedMonth, _currentUser.UserId, _items.ToList());

                MessageBox.Show("Данные сохранены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecalculateTotals()
        {
            HoursTotalTextBlock.Text = _items.Sum(x => x.WorkedHours).ToString("0.##");
        }
    }
}