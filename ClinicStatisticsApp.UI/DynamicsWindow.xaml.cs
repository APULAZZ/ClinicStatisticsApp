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

        private int SelectedYear => YearComboBox.SelectedItem is int year
            ? year
            : DateTime.Now.Year;

        private List<int> SelectedComparisonYears =>
            ComparisonYearsPanel.Children
                .OfType<CheckBox>()
                .Where(x => x.IsChecked == true && int.TryParse(x.Content?.ToString(), out _))
                .Select(x => int.Parse(x.Content!.ToString()!))
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

        private void LoadYears()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 5; year <= currentYear + 2; year++)
            {
                YearComboBox.Items.Add(year);

                var checkBox = new CheckBox
                {
                    Content = year.ToString(),
                    Margin = new Thickness(0, 0, 14, 8),
                    IsChecked = year == currentYear || year == currentYear - 1,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold
                };

                ComparisonYearsPanel.Children.Add(checkBox);
            }

            YearComboBox.SelectedItem = currentYear;
        }

        private void BuildButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = _service.Build(SelectedYear, SelectedComparisonYears);
                RenderBlocks(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "Ошибка построения динамики",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RenderBlocks(DynamicsResult result)
        {
            BlocksPanel.Children.Clear();

            BlocksPanel.Children.Add(new TextBlock
            {
                Text = $"Динамика за {SelectedYear} год",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#1F2937"),
                Margin = new Thickness(0, 0, 0, 18)
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
                CornerRadius = new CornerRadius(16),
                BorderBrush = CreateBrush("#E5E7EB"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = block.BranchName,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#1F2937"),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 260,
                ItemsSource = block.Employees,
                Margin = new Thickness(0, 0, 0, 12),
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FrozenColumnCount = 1
            };

            grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
            grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

            grid.Columns.Add(CreateBranchColumn("Сотрудник", "EmployeeFullName", 220));
            grid.Columns.Add(CreateCenteredColumn("Янв", "January", 60));
            grid.Columns.Add(CreateCenteredColumn("Фев", "February", 60));
            grid.Columns.Add(CreateCenteredColumn("Мар", "March", 60));
            grid.Columns.Add(CreateCenteredColumn("Апр", "April", 60));
            grid.Columns.Add(CreateCenteredColumn("Май", "May", 60));
            grid.Columns.Add(CreateCenteredColumn("Июн", "June", 60));
            grid.Columns.Add(CreateCenteredColumn("Июл", "July", 60));
            grid.Columns.Add(CreateCenteredColumn("Авг", "August", 60));
            grid.Columns.Add(CreateCenteredColumn("Сен", "September", 60));
            grid.Columns.Add(CreateCenteredColumn("Окт", "October", 60));
            grid.Columns.Add(CreateCenteredColumn("Ноя", "November", 60));
            grid.Columns.Add(CreateCenteredColumn("Дек", "December", 60));

            stack.Children.Add(grid);

            stack.Children.Add(new Border
            {
                Background = CreateBrush("#F8FAFC"),
                BorderBrush = CreateBrush("#E5E7EB"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                Child = new TextBlock
                {
                    Text =
                        $"Итого: Янв={block.TotalJanuary}, Фев={block.TotalFebruary}, Мар={block.TotalMarch}, Апр={block.TotalApril}, " +
                        $"Май={block.TotalMay}, Июн={block.TotalJune}, Июл={block.TotalJuly}, Авг={block.TotalAugust}, " +
                        $"Сен={block.TotalSeptember}, Окт={block.TotalOctober}, Ноя={block.TotalNovember}, Дек={block.TotalDecember}",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CreateBrush("#1D4ED8"),
                    TextWrapping = TextWrapping.Wrap
                }
            });

            border.Child = stack;
            return border;
        }

        private UIElement CreateComparisonSection(string title, List<DynamicsComparisonRowViewModel> rows)
        {
            var border = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(16),
                BorderBrush = CreateBrush("#E5E7EB"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#1F2937"),
                Margin = new Thickness(0, 0, 0, 12)
            });

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 230,
                ItemsSource = rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
            grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

            grid.Columns.Add(CreateBranchColumn("Год", "Year", 90));
            grid.Columns.Add(CreateCenteredColumn("Янв", "January", 60));
            grid.Columns.Add(CreateCenteredColumn("Фев", "February", 60));
            grid.Columns.Add(CreateCenteredColumn("Мар", "March", 60));
            grid.Columns.Add(CreateCenteredColumn("Апр", "April", 60));
            grid.Columns.Add(CreateCenteredColumn("Май", "May", 60));
            grid.Columns.Add(CreateCenteredColumn("Июн", "June", 60));
            grid.Columns.Add(CreateCenteredColumn("Июл", "July", 60));
            grid.Columns.Add(CreateCenteredColumn("Авг", "August", 60));
            grid.Columns.Add(CreateCenteredColumn("Сен", "September", 60));
            grid.Columns.Add(CreateCenteredColumn("Окт", "October", 60));
            grid.Columns.Add(CreateCenteredColumn("Ноя", "November", 60));
            grid.Columns.Add(CreateCenteredColumn("Дек", "December", 60));

            stack.Children.Add(grid);

            border.Child = stack;
            return border;
        }

        private DataGridTextColumn CreateCenteredColumn(string header, string bindingPath, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(bindingPath),
                Width = width,
                ElementStyle = CreateCenteredCellStyle()
            };
        }

        private DataGridTextColumn CreateBranchColumn(string header, string bindingPath, double width)
        {
            return new DataGridTextColumn
            {
                Header = header,
                Binding = new System.Windows.Data.Binding(bindingPath),
                Width = width,
                ElementStyle = CreateBranchCellStyle()
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

        private SolidColorBrush CreateBrush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        }
    }
}