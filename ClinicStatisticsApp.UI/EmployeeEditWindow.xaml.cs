using ClinicStatisticsApp.Models;
using System.Globalization;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class EmployeeEditWindow : Window
    {
        public Employee Employee { get; private set; }

        public EmployeeEditWindow(Employee employee)
        {
            InitializeComponent();

            Employee = employee;

            FullNameTextBox.Text = Employee.FullName;
            IsActiveCheckBox.IsChecked = Employee.IsActive;
            IsCallCenterCheckBox.IsChecked = Employee.IsCallCenter;
            RateTextBox.Text = Employee.DefaultReviewPaymentRate?.ToString(CultureInfo.InvariantCulture) ?? "";
            CommentTextBox.Text = Employee.Comment ?? "";
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var fullName = FullNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                MessageBox.Show("Введите ФИО сотрудника.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal? rate = null;
            if (!string.IsNullOrWhiteSpace(RateTextBox.Text))
            {
                if (decimal.TryParse(RateTextBox.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRate))
                {
                    rate = parsedRate;
                }
                else
                {
                    MessageBox.Show("Неверный формат ставки.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            Employee.FullName = fullName;
            Employee.IsActive = IsActiveCheckBox.IsChecked == true;
            Employee.IsCallCenter = IsCallCenterCheckBox.IsChecked == true;
            Employee.DefaultReviewPaymentRate = rate;
            Employee.Comment = string.IsNullOrWhiteSpace(CommentTextBox.Text) ? null : CommentTextBox.Text.Trim();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}