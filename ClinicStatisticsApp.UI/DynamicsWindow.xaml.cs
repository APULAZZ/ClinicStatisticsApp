using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI
{
    public partial class DynamicsWindow : Window
    {
        private readonly DynamicsService _service = new DynamicsService();

        public DynamicsWindow()
        {
            InitializeComponent();
            LoadYears();
        }

        private void LoadYears()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 5; year <= currentYear + 2; year++)
            {
                YearComboBox.Items.Add(year);

                var checkBox = new CheckBox
                {
                    Content = year.ToString(),
                    Margin = new Thickness(0, 0, 10, 5),
                    IsChecked = year == currentYear || year == currentYear - 1
                };

                ComparisonYearsPanel.Children.Add(checkBox);
            }

            YearComboBox.SelectedItem = currentYear;
        }

        private int SelectedYear => (int)(YearComboBox.SelectedItem ?? DateTime.Now.Year);

        private List<int> SelectedComparisonYears =>
            ComparisonYearsPanel.Children
                .OfType<CheckBox>()
                .Where(x => x.IsChecked == true && int.TryParse(x.Content?.ToString(), out _))
                .Select(x => int.Parse(x.Content!.ToString()!))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _service.Build(SelectedYear, SelectedComparisonYears);
                RenderBlocks(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка построения динамики", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenderBlocks(DynamicsResult result)
        {
            BlocksPanel.Children.Clear();

            BlocksPanel.Children.Add(new TextBlock
            {
                Text = $"Динамика за {SelectedYear} год",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            });

            foreach (var block in result.BranchBlocks)
            {
                BlocksPanel.Children.Add(CreateBlock(block));
            }

            if (result.CallCenterBlock != null)
            {
                BlocksPanel.Children.Add(CreateBlock(result.CallCenterBlock));
            }

            BlocksPanel.Children.Add(CreateComparisonSection("Сравнение: только филиалы", result.BranchComparisonRows));
            BlocksPanel.Children.Add(CreateComparisonSection("Сравнение: только колл-центр", result.CallCenterComparisonRows));
            BlocksPanel.Children.Add(CreateComparisonSection("Сравнение: филиалы + колл-центр", result.SystemComparisonRows));
        }

        private UIElement CreateBlock(DynamicsBranchBlockViewModel block)
        {
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = block.BranchName,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 250,
                ItemsSource = block.Employees,
                Margin = new Thickness(0, 0, 0, 10),
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
            grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

            grid.Columns.Add(new DataGridTextColumn { Header = "Сотрудник", Binding = new System.Windows.Data.Binding("EmployeeFullName"), Width = 220 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Янв", Binding = new System.Windows.Data.Binding("January"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Фев", Binding = new System.Windows.Data.Binding("February"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Мар", Binding = new System.Windows.Data.Binding("March"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Апр", Binding = new System.Windows.Data.Binding("April"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Май", Binding = new System.Windows.Data.Binding("May"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Июн", Binding = new System.Windows.Data.Binding("June"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Июл", Binding = new System.Windows.Data.Binding("July"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Авг", Binding = new System.Windows.Data.Binding("August"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Сен", Binding = new System.Windows.Data.Binding("September"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Окт", Binding = new System.Windows.Data.Binding("October"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Ноя", Binding = new System.Windows.Data.Binding("November"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Дек", Binding = new System.Windows.Data.Binding("December"), Width = 60 });

            stack.Children.Add(grid);

            stack.Children.Add(new TextBlock
            {
                Text =
                    $"Итого: Янв={block.TotalJanuary}, Фев={block.TotalFebruary}, Мар={block.TotalMarch}, Апр={block.TotalApril}, " +
                    $"Май={block.TotalMay}, Июн={block.TotalJune}, Июл={block.TotalJuly}, Авг={block.TotalAugust}, " +
                    $"Сен={block.TotalSeptember}, Окт={block.TotalOctober}, Ноя={block.TotalNovember}, Дек={block.TotalDecember}",
                FontWeight = FontWeights.SemiBold
            });

            border.Child = stack;
            return border;
        }

        private UIElement CreateComparisonSection(string title, List<DynamicsComparisonRowViewModel> rows)
        {
            var border = new Border
            {
                Background = Brushes.WhiteSmoke,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 220,
                ItemsSource = rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
            grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

            grid.Columns.Add(new DataGridTextColumn { Header = "Год", Binding = new System.Windows.Data.Binding("Year"), Width = 80 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Янв", Binding = new System.Windows.Data.Binding("January"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Фев", Binding = new System.Windows.Data.Binding("February"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Мар", Binding = new System.Windows.Data.Binding("March"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Апр", Binding = new System.Windows.Data.Binding("April"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Май", Binding = new System.Windows.Data.Binding("May"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Июн", Binding = new System.Windows.Data.Binding("June"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Июл", Binding = new System.Windows.Data.Binding("July"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Авг", Binding = new System.Windows.Data.Binding("August"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Сен", Binding = new System.Windows.Data.Binding("September"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Окт", Binding = new System.Windows.Data.Binding("October"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Ноя", Binding = new System.Windows.Data.Binding("November"), Width = 60 });
            grid.Columns.Add(new DataGridTextColumn { Header = "Дек", Binding = new System.Windows.Data.Binding("December"), Width = 60 });

            stack.Children.Add(grid);

            border.Child = stack;
            return border;
        }

        private void InnerDataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            e.Handled = true;

            var scrollViewer = FindChildScrollViewer(this);
            if (scrollViewer != null)
            {
                scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta / 3.0);
            }
        }

        private ScrollViewer? FindChildScrollViewer(DependencyObject parent)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is ScrollViewer scrollViewer)
                    return scrollViewer;

                var result = FindChildScrollViewer(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}