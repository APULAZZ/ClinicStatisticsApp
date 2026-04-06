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

            Employee = employee ?? throw new System.ArgumentNullException(nameof(employee));

            FullNameTextBox.Text = Employee.FullName;
            IsActiveCheckBox.IsChecked = Employee.IsActive;
            IsCallCenterCheckBox.IsChecked = Employee.IsCallCenter;
            RateTextBox.Text = Employee.DefaultReviewPaymentRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            CommentTextBox.Text = Employee.Comment ?? string.Empty;
        }

        private void ShowError(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorTextBlock.Text = string.Empty;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            var fullName = FullNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ShowError("Введите ФИО сотрудника.");
                FullNameTextBox.Focus();
                return;
            }

            decimal? rate = null;
            if (!string.IsNullOrWhiteSpace(RateTextBox.Text))
            {
                if (decimal.TryParse(
                    RateTextBox.Text.Replace(",", "."),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedRate))
                {
                    rate = parsedRate;
                }
                else
                {
                    ShowError("Неверный формат ставки.");
                    RateTextBox.Focus();
                    return;
                }
            }

            Employee.FullName = fullName;
            Employee.IsActive = IsActiveCheckBox.IsChecked == true;
            Employee.IsCallCenter = IsCallCenterCheckBox.IsChecked == true;
            Employee.DefaultReviewPaymentRate = rate;
            Employee.Comment = string.IsNullOrWhiteSpace(CommentTextBox.Text)
                ? null
                : CommentTextBox.Text.Trim();

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