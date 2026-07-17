using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class NaradWindow : System.Windows.Controls.UserControl
    {
        private readonly CurrentUserInfo _currentUser;
        private readonly NaradService _naradService = new NaradService();
        private readonly NaradExcelExportService _excelExportService = new NaradExcelExportService();
        private readonly NaradPdfExportService _pdfExportService = new NaradPdfExportService();

        private ObservableCollection<NaradEntryViewModel> _items = new();

        public NaradWindow(CurrentUserInfo currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));

            if (_currentUser.BranchId == null)
            {
                MessageBox.Show(
                    "Для текущего пользователя не задан филиал.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                WorkspaceNavigator.Navigate(null);
                return;
            }

            LoadPeriods();
            UpdateHeader();

            SetItemsSource(new ObservableCollection<NaradEntryViewModel>());
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

            var data = _naradService.GetNaradEntries(
                _currentUser.BranchId.Value,
                SelectedYear,
                SelectedMonth,
                _currentUser.UserId);

            SetItemsSource(new ObservableCollection<NaradEntryViewModel>(data));
        }

        private void SetItemsSource(ObservableCollection<NaradEntryViewModel> items)
        {
            UnsubscribeFromItems(_items);

            _items = items ?? new ObservableCollection<NaradEntryViewModel>();

            SubscribeToItems(_items);

            NaradDataGrid.ItemsSource = _items;
            RecalculateTotals();
        }

        private void SubscribeToItems(ObservableCollection<NaradEntryViewModel> items)
        {
            items.CollectionChanged += Items_CollectionChanged;

            foreach (var item in items)
            {
                SubscribeToItem(item);
            }
        }

        private void UnsubscribeFromItems(ObservableCollection<NaradEntryViewModel> items)
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
                foreach (var item in e.OldItems.OfType<NaradEntryViewModel>())
                {
                    UnsubscribeFromItem(item);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<NaradEntryViewModel>())
                {
                    SubscribeToItem(item);
                }
            }

            RecalculateTotals();
        }

        private void SubscribeToItem(NaradEntryViewModel item)
        {
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += Item_PropertyChanged;
            }
        }

        private void UnsubscribeFromItem(NaradEntryViewModel item)
        {
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged -= Item_PropertyChanged;
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NaradEntryViewModel.IsIncluded) ||
                e.PropertyName == nameof(NaradEntryViewModel.PaymentPerReview) ||
                e.PropertyName == nameof(NaradEntryViewModel.TotalPayment))
            {
                RecalculateTotals();
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentUser.BranchId == null)
                    return;

                NaradDataGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                NaradDataGrid.CommitEdit(DataGridEditingUnit.Row, true);

                _naradService.SaveNaradEntries(
                    _currentUser.BranchId.Value,
                    SelectedYear,
                    SelectedMonth,
                    _items.ToList());

                MessageBox.Show(
                    "Наряд сохранен.",
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

                MessageBox.Show(
                    "Файл Excel успешно сохранен.",
                    "Экспорт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка экспорта",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Наряд_{_currentUser.BranchName}_{SelectedYear}_{SelectedMonth:00}.pdf"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                _pdfExportService.Export(
                    saveFileDialog.FileName,
                    _currentUser.BranchName ?? "Филиал",
                    SelectedYear,
                    SelectedMonth,
                    _items.ToList());

                MessageBox.Show(
                    "PDF-файл успешно сохранен.",
                    "Экспорт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка PDF-экспорта",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void NaradDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RecalculateTotals));
        }

        private void NaradDataGrid_CurrentCellChanged(object? sender, EventArgs e)
        {
            RecalculateTotals();
        }

        private void RecalculateTotals()
        {
            var included = _items.Where(x => x.IsIncluded).ToList();

            SmsTotalTextBlock.Text = included.Sum(x => x.SmsSentCount).ToString();
            ReviewsTotalTextBlock.Text = included.Sum(x => x.ReviewsLeftCount).ToString();
            PaymentTotalTextBlock.Text = included.Sum(x => x.TotalPayment).ToString("0.##");
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(null);
        }
    }
}
