using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class HoursReportWindow : Window
    {
        private readonly CurrentUserInfo _currentUser;
        private readonly Window? _previousWindow;
        private readonly HoursReportService _hoursReportService = new HoursReportService();
        private readonly CopyEmployeesFromPreviousMonthService _copyService = new CopyEmployeesFromPreviousMonthService();

        private ObservableCollection<HoursEntryViewModel> _items = new();
        public ObservableCollection<Employee> Employees { get; set; } = new();

        public HoursReportWindow(CurrentUserInfo currentUser, Window? previousWindow = null)
        {
            InitializeComponent();

            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _previousWindow = previousWindow;

            if (_currentUser.BranchId == null)
            {
                MessageBox.Show(
                    "Для текущего пользователя не задан филиал.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _previousWindow?.Show();
                Close();
                return;
            }

            LoadPeriods();
            LoadEmployees();

            SetItemsSource(new ObservableCollection<HoursEntryViewModel>());
        }

        private int SelectedYear => YearComboBox.SelectedItem is int year
            ? year
            : DateTime.Now.Year;

        private int SelectedMonth
        {
            get
            {
                if (MonthComboBox.SelectedItem is ComboBoxItem item && item.Tag is int month)
                    return month;

                return DateTime.Now.Month;
            }
        }

        private void LoadPeriods()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 5; year <= currentYear + 2; year++)
            {
                YearComboBox.Items.Add(year);
            }

            YearComboBox.SelectedItem = currentYear;

            MonthComboBox.Items.Add(CreateMonthItem("Январь", 1));
            MonthComboBox.Items.Add(CreateMonthItem("Февраль", 2));
            MonthComboBox.Items.Add(CreateMonthItem("Март", 3));
            MonthComboBox.Items.Add(CreateMonthItem("Апрель", 4));
            MonthComboBox.Items.Add(CreateMonthItem("Май", 5));
            MonthComboBox.Items.Add(CreateMonthItem("Июнь", 6));
            MonthComboBox.Items.Add(CreateMonthItem("Июль", 7));
            MonthComboBox.Items.Add(CreateMonthItem("Август", 8));
            MonthComboBox.Items.Add(CreateMonthItem("Сентябрь", 9));
            MonthComboBox.Items.Add(CreateMonthItem("Октябрь", 10));
            MonthComboBox.Items.Add(CreateMonthItem("Ноябрь", 11));
            MonthComboBox.Items.Add(CreateMonthItem("Декабрь", 12));

            MonthComboBox.SelectedIndex = DateTime.Now.Month - 1;
        }

        private ComboBoxItem CreateMonthItem(string text, int month)
        {
            return new ComboBoxItem
            {
                Content = new TextBlock
                {
                    Text = text,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                Tag = month,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
        }

        private void LoadEmployees()
        {
            Employees = new ObservableCollection<Employee>(_hoursReportService.GetActiveEmployees());
            DataContext = this;
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            if (_currentUser.BranchId == null)
                return;

            var data = _hoursReportService.GetHoursEntries(
                _currentUser.BranchId.Value,
                SelectedYear,
                SelectedMonth,
                _currentUser.UserId);

            SetItemsSource(new ObservableCollection<HoursEntryViewModel>(data));
        }

        private void SetItemsSource(ObservableCollection<HoursEntryViewModel> items)
        {
            UnsubscribeFromItems(_items);

            _items = items ?? new ObservableCollection<HoursEntryViewModel>();

            SubscribeToItems(_items);

            HoursDataGrid.ItemsSource = _items;
            RecalculateTotals();
        }

        private void SubscribeToItems(ObservableCollection<HoursEntryViewModel> items)
        {
            items.CollectionChanged += Items_CollectionChanged;

            foreach (var item in items)
            {
                SubscribeToItem(item);
            }
        }

        private void UnsubscribeFromItems(ObservableCollection<HoursEntryViewModel> items)
        {
            items.CollectionChanged -= Items_CollectionChanged;

            foreach (var item in items)
            {
                UnsubscribeFromItem(item);
            }
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<HoursEntryViewModel>())
                {
                    UnsubscribeFromItem(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<HoursEntryViewModel>())
                {
                    SubscribeToItem(item);
                }
            }

            RecalculateTotals();
        }

        private void SubscribeToItem(HoursEntryViewModel item)
        {
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += Item_PropertyChanged;
            }
        }

        private void UnsubscribeFromItem(HoursEntryViewModel item)
        {
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged -= Item_PropertyChanged;
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is HoursEntryViewModel item && e.PropertyName == nameof(HoursEntryViewModel.EmployeeId))
            {
                UpdateEmployeeName(item);
            }

            if (e.PropertyName == nameof(HoursEntryViewModel.WorkedHours))
            {
                RecalculateTotals();
            }
        }

        private void UpdateEmployeeName(HoursEntryViewModel item)
        {
            var employee = Employees.FirstOrDefault(x => x.Id == item.EmployeeId);
            item.EmployeeFullName = employee?.FullName ?? string.Empty;
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

                _copyService.CopyHoursEmployees(
                    _currentUser.BranchId.Value,
                    SelectedYear,
                    SelectedMonth,
                    _currentUser.UserId);

                LoadData();

                MessageBox.Show(
                    "Сотрудники скопированы.",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Ошибка копирования",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentUser.BranchId == null)
                    return;

                HoursDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                HoursDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                foreach (var item in _items)
                {
                    UpdateEmployeeName(item);
                }

                var invalidRows = _items.Where(i => i.EmployeeId <= 0).ToList();
                if (invalidRows.Any())
                {
                    MessageBox.Show(
                        "Во всех строках должен быть выбран сотрудник.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var duplicateEmployees = _items
                    .GroupBy(i => i.EmployeeId)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (duplicateEmployees.Any())
                {
                    MessageBox.Show(
                        "Один и тот же сотрудник не может повторяться в блоке ЧАСЫ.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _hoursReportService.SaveHoursEntries(
                    _currentUser.BranchId.Value,
                    SelectedYear,
                    SelectedMonth,
                    _currentUser.UserId,
                    _items.ToList());

                MessageBox.Show(
                    "Данные сохранены.",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Ошибка сохранения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void HoursDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Row.Item is HoursEntryViewModel item)
                {
                    UpdateEmployeeName(item);
                }

                RecalculateTotals();
            }));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _previousWindow?.Show();
            Close();
        }

        private void HoursDataGrid_CurrentCellChanged(object? sender, EventArgs e)
        {
            if (HoursDataGrid.SelectedItem is HoursEntryViewModel item)
            {
                UpdateEmployeeName(item);
            }

            RecalculateTotals();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_previousWindow != null && !_previousWindow.IsVisible)
            {
                _previousWindow.Show();
            }
        }

        private void RecalculateTotals()
        {
            HoursTotalTextBlock.Text = _items.Sum(x => x.WorkedHours).ToString("0.##");
        }
    }
}