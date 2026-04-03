using ClinicStatisticsApp.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class SummaryGeneralWindow : Window
    {
        private readonly SummaryGeneralService _summaryGeneralService = new SummaryGeneralService();

        public SummaryGeneralWindow()
        {
            InitializeComponent();
            LoadPeriods();
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

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _summaryGeneralService.Build(SelectedYear, SelectedMonth);

                BranchDataGrid.ItemsSource = result.BranchRows;
                CallCenterDataGrid.ItemsSource = result.CallCenterRows;

                BranchTotalsTextBlock.Text =
                    $"Итого по филиалам: Явка={result.BranchTotals.AttendanceTotal}, Неявка={result.BranchTotals.AbsenceTotal}, Всего={result.BranchTotals.GrandTotal}";

                CallCenterTotalsTextBlock.Text =
                    $"Итого по колл-центру: Явка={result.CallCenterTotals.AttendanceTotal}, Неявка={result.CallCenterTotals.AbsenceTotal}, Всего={result.CallCenterTotals.GrandTotal}";

                SystemTotalsTextBlock.Text =
                    $"Всего по системе клиник: Явка={result.SystemTotals.AttendanceTotal}, Неявка={result.SystemTotals.AbsenceTotal}, Всего={result.SystemTotals.GrandTotal}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка построения сводки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}