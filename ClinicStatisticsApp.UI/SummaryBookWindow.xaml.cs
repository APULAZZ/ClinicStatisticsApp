using ClinicStatisticsApp.Services;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class SummaryBookWindow : Window
    {
        private readonly SummaryBookExcelExportService _excelExportService = new SummaryBookExcelExportService();

        public SummaryBookWindow()
        {
            InitializeComponent();
            LoadExportPeriods();
        }

        private void LoadExportPeriods()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 7; year <= currentYear + 2; year++)
            {
                ExportYearComboBox.Items.Add(year);
            }

            ExportYearComboBox.SelectedItem = currentYear;

            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Январь", Tag = 1 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Февраль", Tag = 2 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Март", Tag = 3 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Апрель", Tag = 4 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Май", Tag = 5 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Июнь", Tag = 6 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Июль", Tag = 7 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Август", Tag = 8 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Сентябрь", Tag = 9 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Октябрь", Tag = 10 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Ноябрь", Tag = 11 });
            ExportMonthComboBox.Items.Add(new ComboBoxItem { Content = "Декабрь", Tag = 12 });

            ExportMonthComboBox.SelectedIndex = DateTime.Now.Month - 1;
        }

        private int ExportYear => (int)(ExportYearComboBox.SelectedItem ?? DateTime.Now.Year);

        private int ExportMonth
        {
            get
            {
                if (ExportMonthComboBox.SelectedItem is ComboBoxItem item && item.Tag is int month)
                    return month;

                return DateTime.Now.Month;
            }
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    FileName = $"Сводная_книга_{ExportYear}_{ExportMonth:00}.xlsx"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                _excelExportService.ExportMonthlySummaryBook(saveFileDialog.FileName, ExportYear, ExportMonth);

                MessageBox.Show("Сводная книга Excel успешно сохранена.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SummaryGeneralButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryGeneralWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void SummaryProfoButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryProfoWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void SummaryAdminButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryAdminWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void SummaryProDoctorButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new SummaryProDoctorWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void DynamicsButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new DynamicsWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void ComparativePerkButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ComparativePerkWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void ComparativeProfiButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new ComparativeProfiWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void AbsolutePrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            var window = new AbsolutePrimaryWindow
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void StubButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Этот сводный лист будет подключен следующим этапом.",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}