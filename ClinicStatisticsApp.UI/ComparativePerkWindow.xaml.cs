using ClinicStatisticsApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace ClinicStatisticsApp.UI
{
    public partial class ComparativePerkWindow : System.Windows.Controls.UserControl
    {
        private readonly ComparativePerkService _service = new ComparativePerkService();
        public ComparativePerkWindow()
        {
            InitializeComponent();
            LoadYears();
        }

        private int MainYear => MainYearComboBox.SelectedItem is int year
            ? year
            : DateTime.Now.Year;

        private List<int> SelectedOtherYears =>
            YearsPanel.Children
                .OfType<CheckBox>()
                .Where(x => x.IsChecked == true && int.TryParse(x.Content?.ToString(), out _))
                .Select(x => int.Parse(x.Content!.ToString()!))
                .Where(y => y != MainYear)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

        private void LoadYears()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 7; year <= currentYear + 2; year++)
            {
                MainYearComboBox.Items.Add(year);

                var cb = new CheckBox
                {
                    Content = year.ToString(),
                    Margin = new Thickness(0, 0, 12, 6),
                    IsChecked = year == currentYear - 1 || year == currentYear - 2,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold
                };

                YearsPanel.Children.Add(cb);
            }

            MainYearComboBox.SelectedItem = currentYear;
        }

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
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка построения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BuildGridColumns(List<int> otherYears, int mainYear)
        {
            ComparativeDataGrid.Columns.Clear();
            ComparativeDataGrid.Resources.Clear();

            ComparativeDataGrid.RowHeight = 26;
            ComparativeDataGrid.ColumnHeaderHeight = 32;
            ComparativeDataGrid.FontSize = 12;
            ComparativeDataGrid.GridLinesVisibility = DataGridGridLinesVisibility.All;
            ComparativeDataGrid.HorizontalGridLinesBrush = CreateBrush("#E5E7EB");
            ComparativeDataGrid.VerticalGridLinesBrush = CreateBrush("#E5E7EB");

            ComparativeDataGrid.Resources.Add(typeof(DataGridCell), CreateCompactCellStyle());
            ComparativeDataGrid.Resources.Add(typeof(DataGridColumnHeader), CreateCompactHeaderStyle());

            ComparativeDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Филиал",
                Binding = new Binding("Name"),
                Width = 210,
                ElementStyle = CreateBranchCellStyle()
            });

            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Янв", "January"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Фев", "February"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Мар", "March"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Апр", "April"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Май", "May"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Июн", "June"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Июл", "July"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Авг", "August"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Сен", "September"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Окт", "October"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Ноя", "November"));
            ComparativeDataGrid.Columns.Add(CreateMonthColumn("Дек", "December"));

            ComparativeDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = $"Итог {mainYear}",
                Binding = new Binding("MainYearTotal"),
                Width = 90,
                ElementStyle = CreateTotalCellStyle()
            });

            foreach (var year in otherYears)
            {
                ComparativeDataGrid.Columns.Add(new DataGridTextColumn
                {
                    Header = $"Итог {year}",
                    Binding = new Binding($"OtherYearTotals[{year}]"),
                    Width = 90,
                    ElementStyle = CreateTotalCellStyle()
                });
            }
        }

        private DataGridTextColumn CreateMonthColumn(string header, string bindingPath)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new Binding(bindingPath),
                Width = 64,
                ElementStyle = CreateCenteredCellStyle()
            };
        }

        private Style CreateCenteredCellStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            return style;
        }

        private Style CreateBranchCellStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, CreateBrush("#1F2937")));
            return style;
        }

        private Style CreateTotalCellStyle()
        {
            var style = new Style(typeof(TextBlock));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, CreateBrush("#1D4ED8")));
            return style;
        }

        private Style CreateCompactCellStyle()
        {
            var style = new Style(typeof(DataGridCell));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4, 2, 4, 2)));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, CreateBrush("#E5E7EB")));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            return style;
        }

        private Style CreateCompactHeaderStyle()
        {
            var style = new Style(typeof(DataGridColumnHeader));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 4, 6, 4)));
            style.Setters.Add(new Setter(Control.FontSizeProperty, 11.0));
            style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 32.0));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, CreateBrush("#D1D5DB")));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
            return style;
        }

        private System.Windows.Media.SolidColorBrush CreateBrush(string hex)
        {
            return (System.Windows.Media.SolidColorBrush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            WorkspaceNavigator.Navigate(new SummaryBookWindow());
        }
    }
}
