using ClinicStatisticsApp.Services;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class SummaryBookWindow : System.Windows.Controls.UserControl
    {
        private readonly SummaryBookExcelExportService _excelExportService = new SummaryBookExcelExportService();
        private readonly SummaryBookPdfExportService _pdfExportService = new SummaryBookPdfExportService();
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

            ExportMonthComboBox.Items.Add(CreateMonthItem("Январь", 1));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Февраль", 2));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Март", 3));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Апрель", 4));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Май", 5));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Июнь", 6));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Июль", 7));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Август", 8));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Сентябрь", 9));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Октябрь", 10));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Ноябрь", 11));
            ExportMonthComboBox.Items.Add(CreateMonthItem("Декабрь", 12));

            ExportMonthComboBox.SelectedIndex = DateTime.Now.Month - 1;
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

        private int ExportYear => ExportYearComboBox.SelectedItem is int year
            ? year
            : DateTime.Now.Year;

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

                MessageBox.Show(
                    "Сводная книга Excel успешно сохранена.",
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
                    FileName = $"Сводная_книга_{ExportYear}_{ExportMonth:00}.pdf"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                _pdfExportService.ExportMonthlySummaryBook(saveFileDialog.FileName, ExportYear, ExportMonth);

                MessageBox.Show(
                    "Сводная книга PDF успешно сохранена.",
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

        private void SummaryGeneralButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new SummaryGeneralWindow());
        }

        private void SummaryProfoButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new SummaryProfoWindow());
        }

        private void SummaryAdminButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new SummaryAdminWindow());
        }

        private void SummaryProDoctorButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new SummaryProDoctorWindow());
        }

        private void DynamicsButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new DynamicsWindow());
        }

        private void ComparativePerkButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new ComparativePerkWindow());
        }

        private void ComparativeProfiButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new ComparativeProfiWindow());
        }

        private void AbsolutePrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new AbsolutePrimaryWindow());
        }

        private void StubButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Этот сводный лист будет подключен следующим этапом.",
                "Информация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(null);
        }
    }
}
