using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI
{
    public partial class BranchReportWindow : System.Windows.Controls.UserControl
    {
        private readonly CurrentUserInfo _currentUser;
        private readonly BranchReportStatusService _statusService = new BranchReportStatusService();
        private readonly BranchReportExcelExportService _excelExportService = new BranchReportExcelExportService();
        private readonly BranchReportPdfExportService _pdfExportService = new BranchReportPdfExportService();

        private readonly int _branchId;
        private readonly string _branchName;

        public BranchReportWindow(CurrentUserInfo currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
            _branchId = currentUser.BranchId ?? 0;
            _branchName = currentUser.BranchName ?? "не задан";

            HeaderTextBlock.Text = "Филиальный отчет";
            PeriodTextBlock.Text = $"Филиал: {_branchName}";

            LoadPeriods();
            UpdateStatus();
            ConfigureButtons();
        }

        public BranchReportWindow(SelectedBranchContext context)
        {
            InitializeComponent();

            if (context == null)
                throw new ArgumentNullException(nameof(context));

            _currentUser = context.CurrentUser ?? throw new ArgumentNullException(nameof(context.CurrentUser));
            _branchId = context.BranchId;
            _branchName = context.BranchName ?? "не задан";

            HeaderTextBlock.Text = "Филиальный отчет";
            PeriodTextBlock.Text = $"Филиал: {_branchName}";

            LoadPeriods();
            UpdateStatus();
            ConfigureButtons();
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

        private void PeriodChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            UpdateStatus();
            ConfigureButtons();
        }

        private void UpdateStatus()
        {
            if (_branchId <= 0)
            {
                SetStatusBadge("● Филиал не задан", "#6B7280", "#6B7280", "#FFFFFF");
                return;
            }

            var status = _statusService.GetStatus(_branchId, SelectedYear, SelectedMonth);

            if (status == "Closed")
            {
                SetStatusBadge("● Закрыт", "#FEE2E2", "#FECACA", "#991B1B");
            }
            else
            {
                SetStatusBadge("● Черновик", "#DCFCE7", "#BBF7D0", "#166534");
            }
        }

        private void SetStatusBadge(string text, string backgroundHex, string borderHex, string foregroundHex)
        {
            StatusTextBlock.Text = text;
            StatusBadgeBorder.Background = CreateBrush(backgroundHex);
            StatusBadgeBorder.BorderBrush = CreateBrush(borderHex);
            StatusTextBlock.Foreground = CreateBrush(foregroundHex);
        }

        private static SolidColorBrush CreateBrush(string hex)
        {
            var colorObject = ColorConverter.ConvertFromString(hex);
            if (colorObject is Color color)
                return new SolidColorBrush(color);

            return Brushes.Transparent;
        }

        private void ConfigureButtons()
        {
            var roleCode = _currentUser.RoleCode ?? string.Empty;

            var isAdmin = roleCode == "Admin";
            var isBranchUser = roleCode == "BranchUser";

            ClosePeriodButton.IsEnabled = isAdmin || isBranchUser;
            ReopenPeriodButton.IsEnabled = isAdmin;
        }

        private void ClosePeriodButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_branchId <= 0)
                    return;

                if (!_statusService.Exists(_branchId, SelectedYear, SelectedMonth))
                {
                    MessageBox.Show(
                        "Нельзя закрыть пустой период без созданного отчета.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                _statusService.ClosePeriod(_branchId, SelectedYear, SelectedMonth);
                UpdateStatus();

                MessageBox.Show(
                    "Период закрыт.",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Ошибка закрытия периода",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ReopenPeriodButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_branchId <= 0)
                    return;

                _statusService.ReopenPeriod(_branchId, SelectedYear, SelectedMonth);
                UpdateStatus();

                MessageBox.Show(
                    "Период открыт.",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Ошибка открытия периода",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_branchId <= 0)
                    return;

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel files (*.xlsx)|*.xlsx",
                    FileName = $"Отчет_{_branchName}_{SelectedYear}_{SelectedMonth:00}.xlsx"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                _excelExportService.Export(
                    saveFileDialog.FileName,
                    _branchId,
                    _branchName,
                    SelectedYear,
                    SelectedMonth);

                MessageBox.Show(
                    "Файл Excel успешно сохранен.",
                    "Экспорт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Ошибка экспорта",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportPdfButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_branchId <= 0)
                    return;

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Отчет_{_branchName}_{SelectedYear}_{SelectedMonth:00}.pdf"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                _pdfExportService.Export(
                    saveFileDialog.FileName,
                    _branchId,
                    _branchName,
                    SelectedYear,
                    SelectedMonth);

                MessageBox.Show(
                    "PDF-файл успешно сохранен.",
                    "Экспорт",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Ошибка PDF-экспорта",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void PerkButton_Click(object sender, RoutedEventArgs e)
        {
            var userContext = CloneUserWithSelectedBranch();
            WorkspaceNavigator.Navigate(new PerkReportWindow(userContext));
        }

        private void ProfiButton_Click(object sender, RoutedEventArgs e)
        {
            var userContext = CloneUserWithSelectedBranch();
            WorkspaceNavigator.Navigate(new ProfiReportWindow(userContext));
        }

        private void HoursButton_Click(object sender, RoutedEventArgs e)
        {
            var userContext = CloneUserWithSelectedBranch();
            WorkspaceNavigator.Navigate(new HoursReportWindow(userContext));
        }

        private void ReviewsButton_Click(object sender, RoutedEventArgs e)
        {
            var userContext = CloneUserWithSelectedBranch();
            WorkspaceNavigator.Navigate(new ReviewReportWindow(userContext));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(null);
        }

        private CurrentUserInfo CloneUserWithSelectedBranch()
        {
            return new CurrentUserInfo
            {
                UserId = _currentUser.UserId,
                Login = _currentUser.Login,
                FullName = _currentUser.FullName,
                RoleCode = _currentUser.RoleCode,
                RoleName = _currentUser.RoleName,
                BranchId = _branchId,
                BranchName = _branchName
            };
        }
    }
}
