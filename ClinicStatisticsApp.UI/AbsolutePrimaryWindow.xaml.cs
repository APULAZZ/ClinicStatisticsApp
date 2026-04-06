using ClinicStatisticsApp.Services;
using System;
using System.Windows;

namespace ClinicStatisticsApp.UI
{
    public partial class AbsolutePrimaryWindow : Window
    {
        private readonly AbsolutePrimaryService _service = new AbsolutePrimaryService();
        private readonly Window? _previousWindow;

        public AbsolutePrimaryWindow(Window? previousWindow = null)
        {
            InitializeComponent();
            _previousWindow = previousWindow;
            LoadYears();
        }

        private void LoadYears()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 7; year <= currentYear + 2; year++)
            {
                YearComboBox.Items.Add(year);
            }

            YearComboBox.SelectedItem = currentYear;
        }

        private int SelectedYear => YearComboBox.SelectedItem is int year
            ? year
            : DateTime.Now.Year;

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _service.Build(SelectedYear);
                AbsolutePrimaryDataGrid.ItemsSource = result.Rows;

                TotalsTextBlock.Text =
                    $"Янв: {result.Totals.JanuaryTotal}   •   " +
                    $"Фев: {result.Totals.FebruaryTotal}   •   " +
                    $"Мар: {result.Totals.MarchTotal}   •   " +
                    $"Апр: {result.Totals.AprilTotal}   •   " +
                    $"Май: {result.Totals.MayTotal}   •   " +
                    $"Июн: {result.Totals.JuneTotal}   •   " +
                    $"Июл: {result.Totals.JulyTotal}   •   " +
                    $"Авг: {result.Totals.AugustTotal}   •   " +
                    $"Сен: {result.Totals.SeptemberTotal}   •   " +
                    $"Окт: {result.Totals.OctoberTotal}   •   " +
                    $"Ноя: {result.Totals.NovemberTotal}   •   " +
                    $"Дек: {result.Totals.DecemberTotal}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка построения",
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