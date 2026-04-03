using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ClinicStatisticsApp.UI
{
    public partial class NaradWindow : Window
    {
        private readonly CurrentUserInfo _currentUser;
        private readonly NaradService _naradService = new NaradService();
        private readonly NaradExcelExportService _excelExportService = new NaradExcelExportService();

        private ObservableCollection<NaradEntryViewModel> _items = new();

        public NaradWindow(CurrentUserInfo currentUser)
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
            UpdateHeader();

            NaradDataGrid.ItemsSource = _items;
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

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    FileName = $"Наряд_{_currentUser.BranchName}_{SelectedYear}_{SelectedMonth:00}.xlsx"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                _excelExportService.Export(
                    saveFileDialog.FileName,
                    _currentUser.BranchName ?? "Филиал",
                    SelectedYear,
                    SelectedMonth,
                    _items.ToList());

                MessageBox.Show("Файл Excel успешно сохранен.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void UpdateHeader()
        {
            HeaderTextBlock.Text = $"Филиал: {_currentUser.BranchName}";
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            if (_currentUser.BranchId == null)
                return;

            var data = _naradService.GetNaradEntries(_currentUser.BranchId.Value, SelectedYear, SelectedMonth, _currentUser.UserId);

            _items = new ObservableCollection<NaradEntryViewModel>(data);
            NaradDataGrid.ItemsSource = _items;

            RecalculateTotals();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentUser.BranchId == null)
                    return;

                _naradService.SaveNaradEntries(_currentUser.BranchId.Value, SelectedYear, SelectedMonth, _items.ToList());

                MessageBox.Show("Наряд сохранен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecalculateTotals()
        {
            var included = _items.Where(x => x.IsIncluded).ToList();

            SmsTotalTextBlock.Text = included.Sum(x => x.SmsSentCount).ToString();
            ReviewsTotalTextBlock.Text = included.Sum(x => x.ReviewsLeftCount).ToString();
            PaymentTotalTextBlock.Text = included.Sum(x => x.TotalPayment).ToString("0.##");
        }
    }
}