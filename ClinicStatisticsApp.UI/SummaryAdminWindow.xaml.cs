using ClinicStatisticsApp.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class SummaryAdminWindow : Window
    {
        private readonly SummaryAdminService _summaryAdminService = new SummaryAdminService();

        public SummaryAdminWindow()
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

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _summaryAdminService.Build(SelectedYear, SelectedMonth);

                BranchAdminsDataGrid.ItemsSource = result.BranchRows;
                CallCenterDataGrid.ItemsSource = result.CallCenterRows;

                BranchTotalsTextBlock.Text =
                    $"Итого по филиалам: Явка = {result.BranchAttendanceTotal}, Неявка = {result.BranchAbsenceTotal}, Премия = {result.BranchPremiumTotal:0.##}";

                CallCenterTotalsTextBlock.Text =
                    $"Итого по колл-центру: Явка = {result.CallCenterAttendanceTotal}, Неявка = {result.CallCenterAbsenceTotal}";

                SystemTotalsTextBlock.Text =
                    $"Всего по системе клиник: Явка = {result.SystemAttendanceTotal}, Неявка = {result.SystemAbsenceTotal}, Премия = {result.SystemPremiumTotal:0.##}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка построения отчета",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}