using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class EmployeeEditWindow : Window
    {
        private readonly SummaryProfoService _summaryProfoService = new SummaryProfoService();

        public Employee Employee { get; private set; }

        private class ProfoRateOption
        {
            public string DisplayName { get; set; } = string.Empty;
            public decimal? Value { get; set; }
        }

        public EmployeeEditWindow(Employee employee)
        {
            InitializeComponent();

            Employee = employee ?? throw new ArgumentNullException(nameof(employee));

            LoadReferences();
            FillForm();
        }

        private void LoadReferences()
        {
            var rateOptions = new List<ProfoRateOption>
            {
                new ProfoRateOption { DisplayName = "— не задано —", Value = null },
                new ProfoRateOption { DisplayName = "0.5", Value = 0.5m },
                new ProfoRateOption { DisplayName = "1.0", Value = 1.0m }
            };

            DefaultProfoRateComboBox.ItemsSource = rateOptions;
            DefaultProfoRateComboBox.DisplayMemberPath = "DisplayName";
            DefaultProfoRateComboBox.SelectedValuePath = "Value";

            DefaultProfoCategoryComboBox.ItemsSource = _summaryProfoService.GetCategories();
        }

        private void FillForm()
        {
            FullNameTextBox.Text = Employee.FullName;
            IsActiveCheckBox.IsChecked = Employee.IsActive;
            IsCallCenterCheckBox.IsChecked = Employee.IsCallCenter;
            RateTextBox.Text = Employee.DefaultReviewPaymentRate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

            DefaultProfoRateComboBox.SelectedValue = Employee.DefaultProfoRate;
            DefaultProfoCategoryComboBox.SelectedValue = Employee.DefaultProfoCategoryId;

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

            decimal? reviewRate = null;
            if (!string.IsNullOrWhiteSpace(RateTextBox.Text))
            {
                if (decimal.TryParse(
                    RateTextBox.Text.Replace(",", "."),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var parsedRate))
                {
                    reviewRate = parsedRate;
                }
                else
                {
                    ShowError("Неверный формат базовой ставки за отзыв.");
                    RateTextBox.Focus();
                    return;
                }
            }

            decimal? defaultProfoRate = null;
            if (DefaultProfoRateComboBox.SelectedItem is ProfoRateOption selectedRateOption)
            {
                defaultProfoRate = selectedRateOption.Value;
            }

            int? defaultProfoCategoryId = null;
            if (DefaultProfoCategoryComboBox.SelectedItem is ProfoCategory selectedCategory)
            {
                defaultProfoCategoryId = selectedCategory.Id;
            }

            Employee.FullName = fullName;
            Employee.IsActive = IsActiveCheckBox.IsChecked == true;
            Employee.IsCallCenter = IsCallCenterCheckBox.IsChecked == true;
            Employee.DefaultReviewPaymentRate = reviewRate;
            Employee.DefaultProfoRate = defaultProfoRate;
            Employee.DefaultProfoCategoryId = defaultProfoCategoryId;
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