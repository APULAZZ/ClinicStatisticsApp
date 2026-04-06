using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI
{
    public partial class SummaryProfoWindow : Window
    {
        private readonly SummaryProfoService _summaryProfoService = new SummaryProfoService();
        private readonly Window? _previousWindow;

        private ObservableCollection<SummaryProfoRowViewModel> _rows = new();
        private ObservableCollection<ProfoCategory> _categories = new();

        public SummaryProfoWindow(Window? previousWindow = null)
        {
            InitializeComponent();

            _previousWindow = previousWindow;

            LoadPeriods();
            LoadReferenceData();

            ProfoDataGrid.ItemsSource = _rows;
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

        private void LoadReferenceData()
        {
            _categories = new ObservableCollection<ProfoCategory>(_summaryProfoService.GetCategories());
            CategoryColumn.ItemsSource = _categories;

            RateColumn.ItemsSource = new object[] { 0.5m, 1.0m };
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

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var result = _summaryProfoService.Build(SelectedYear, SelectedMonth);

                _rows = new ObservableCollection<SummaryProfoRowViewModel>(result.Rows);
                ProfoDataGrid.ItemsSource = _rows;

                TotalsTextBlock.Text =
                    $"Итого: Пригласили = {result.InvitedTotal}, Записались = {result.BookedTotal}, Пришли = {result.ArrivedTotal}, Премия = {result.PremiumTotal:0.##}";

                ConversionTextBlock.Text =
                    $"Конверсия: Пригласили → Записались = {result.ConversionInvitedToBooked:0.#}%   |   " +
                    $"Записались → Пришли = {result.ConversionBookedToArrived:0.#}%   |   " +
                    $"Пригласили → Пришли = {result.ConversionInvitedToArrived:0.#}%";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка загрузки",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _summaryProfoService.SaveManualValues(SelectedYear, SelectedMonth, _rows.ToList());

                LoadData();

                MessageBox.Show(
                    "Сохранено.",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка сохранения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            _previousWindow?.Show();
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (_previousWindow != null && !_previousWindow.IsVisible)
            {
                _previousWindow.Show();
            }
        }
    }
}