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
    public partial class ProfiReportWindow : Window
    {
        private readonly CurrentUserInfo _currentUser;
        private readonly ProfiReportService _profiReportService = new ProfiReportService();
        private readonly CopyEmployeesFromPreviousMonthService _copyService = new CopyEmployeesFromPreviousMonthService();

        private ObservableCollection<ProfiEntryViewModel> _items = new();
        public ObservableCollection<Employee> Employees { get; set; } = new();

        public ProfiReportWindow(CurrentUserInfo currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

            if (_currentUser.BranchId == null)
            {
                MessageBox.Show("Для текущего пользователя не задан филиал.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            LoadPeriods();
            LoadEmployees();

            SetItemsSource(new ObservableCollection<ProfiEntryViewModel>());
        }

        private int SelectedYear => YearComboBox.SelectedItem is int year ? year : DateTime.Now.Year;

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
            Employees = new ObservableCollection<Employee>(_profiReportService.GetActiveEmployees());
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

            var data = _profiReportService.GetProfiEntries(
                _currentUser.BranchId.Value,
                SelectedYear,
                SelectedMonth,
                _currentUser.UserId);

            SetItemsSource(new ObservableCollection<ProfiEntryViewModel>(data));
        }

        private void SetItemsSource(ObservableCollection<ProfiEntryViewModel> items)
        {
            UnsubscribeFromItems(_items);

            _items = items ?? new ObservableCollection<ProfiEntryViewModel>();

            SubscribeToItems(_items);

            ProfiDataGrid.ItemsSource = _items;
            RecalculateTotals();
        }

        private void SubscribeToItems(ObservableCollection<ProfiEntryViewModel> items)
        {
            items.CollectionChanged += Items_CollectionChanged;

            foreach (var item in items)
            {
                SubscribeToItem(item);
            }
        }

        private void UnsubscribeFromItems(ObservableCollection<ProfiEntryViewModel> items)
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
                foreach (var item in e.OldItems.OfType<ProfiEntryViewModel>())
                {
                    UnsubscribeFromItem(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<ProfiEntryViewModel>())
                {
                    SubscribeToItem(item);
                }
            }

            RecalculateTotals();
        }

        private void SubscribeToItem(ProfiEntryViewModel item)
        {
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += Item_PropertyChanged;
            }
        }

        private void UnsubscribeFromItem(ProfiEntryViewModel item)
        {
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged -= Item_PropertyChanged;
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is ProfiEntryViewModel item && e.PropertyName == nameof(ProfiEntryViewModel.EmployeeId))
            {
                UpdateEmployeeName(item);
            }

            if (e.PropertyName == nameof(ProfiEntryViewModel.InvitedCount) ||
                e.PropertyName == nameof(ProfiEntryViewModel.BookedCount) ||
                e.PropertyName == nameof(ProfiEntryViewModel.ArrivedCount))
            {
                RecalculateTotals();
            }
        }

        private void UpdateEmployeeName(ProfiEntryViewModel item)
        {
            var employee = Employees.FirstOrDefault(x => x.Id == item.EmployeeId);
            item.EmployeeFullName = employee?.FullName ?? string.Empty;
        }

        private void AddRowButton_Click(object sender, RoutedEventArgs e)
        {
            _items.Add(new ProfiEntryViewModel());
            RecalculateTotals();
        }

        private void DeleteRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfiDataGrid.SelectedItem is ProfiEntryViewModel selected)
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
                    "Скопировать сотрудников из предыдущего месяца? Текущий список сотрудников в блоке ПРОФЫ будет заменен.",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                _copyService.CopyProfiEmployees(
                    _currentUser.BranchId.Value,
                    SelectedYear,
                    SelectedMonth,
                    _currentUser.UserId);

                LoadData();

                MessageBox.Show("Сотрудники скопированы.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка копирования", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentUser.BranchId == null)
                    return;

                ProfiDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                ProfiDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                foreach (var item in _items)
                {
                    UpdateEmployeeName(item);
                }

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
                    MessageBox.Show("Один и тот же сотрудник не может повторяться в блоке ПРОФЫ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _profiReportService.SaveProfiEntries(
                    _currentUser.BranchId.Value,
                    SelectedYear,
                    SelectedMonth,
                    _currentUser.UserId,
                    _items.ToList());

                MessageBox.Show("Данные сохранены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ProfiDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (e.Row.Item is ProfiEntryViewModel item)
                {
                    UpdateEmployeeName(item);
                }

                RecalculateTotals();
            }));
        }

        private void ProfiDataGrid_CurrentCellChanged(object? sender, EventArgs e)
        {
            if (ProfiDataGrid.SelectedItem is ProfiEntryViewModel item)
            {
                UpdateEmployeeName(item);
            }

            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            InvitedTotalTextBlock.Text = _items.Sum(x => x.InvitedCount).ToString();
            BookedTotalTextBlock.Text = _items.Sum(x => x.BookedCount).ToString();
            ArrivedTotalTextBlock.Text = _items.Sum(x => x.ArrivedCount).ToString();
        }
    }
}