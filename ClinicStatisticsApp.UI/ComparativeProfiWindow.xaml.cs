using ClinicStatisticsApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ClinicStatisticsApp.UI
{
    public partial class ComparativeProfiWindow : Window
    {
        private readonly ComparativeProfiService _service = new ComparativeProfiService();

        public ComparativeProfiWindow()
        {
            InitializeComponent();
            LoadYears();
        }

        private void LoadYears()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 7; year <= currentYear + 2; year++)
            {
                MainYearComboBox.Items.Add(year);

                var cb = new CheckBox
                {
                    Content = year.ToString(),
                    Margin = new Thickness(0, 0, 10, 5),
                    IsChecked = year == currentYear - 1 || year == currentYear - 2
                };

                YearsPanel.Children.Add(cb);
            }

            MainYearComboBox.SelectedItem = currentYear;
        }

        private int MainYear => (int)(MainYearComboBox.SelectedItem ?? DateTime.Now.Year);

        private List<int> SelectedOtherYears =>
            YearsPanel.Children
                .OfType<CheckBox>()
                .Where(x => x.IsChecked == true && int.TryParse(x.Content?.ToString(), out _))
                .Select(x => int.Parse(x.Content!.ToString()!))
                .Where(y => y != MainYear)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _service.Build(MainYear, SelectedOtherYears);
                BuildGridColumns(result.OtherYears, result.MainYear);
                ComparativeDataGrid.ItemsSource = result.Rows;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка построения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildGridColumns(List<int> otherYears, int mainYear)
        {
            ComparativeDataGrid.Columns.Clear();

            ComparativeDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Филиал",
                Binding = new Binding("Name"),
                Width = 220
            });

            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Январь", "January"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Февраль", "February"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Март", "March"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Апрель", "April"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Май", "May"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Июнь", "June"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Июль", "July"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Август", "August"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Сентябрь", "September"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Октябрь", "October"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Ноябрь", "November"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Декабрь", "December"));

            ComparativeDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = $"Итог {mainYear}",
                Binding = new Binding("MainYearTotal"),
                Width = 100
            });

            foreach (var year in otherYears)
            {
                ComparativeDataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = $"Итог {year}",
                    Binding = new Binding($"OtherYearTotals[{year}]"),
                    Width = 100
                });
            }
        }

        private DataGridTextColumn CreateMonthColumn(string header, string bindingPath)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(bindingPath),
                Width = 80
            };
        }
    }
}