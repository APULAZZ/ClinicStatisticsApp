using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI
{
    public partial class DynamicsWindow : Window
    {
        private readonly DynamicsService _service = new DynamicsService();
        private readonly Window? _previousWindow;

        public DynamicsWindow(Window? previousWindow = null)
        {
            InitializeComponent();
            _previousWindow = previousWindow;
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
                    Margin = new Thickness(0, 0, 12, 6),
                    IsChecked = year == currentYear || year == currentYear - 1,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 13,
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
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#1F2937"),
                Margin = new Thickness(0, 0, 0, 10)
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
                CornerRadius = new CornerRadius(12),
                BorderBrush = CreateBrush("#E5E7EB"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = block.BranchName,
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#1F2937"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 210,
                ItemsSource = block.Employees,
                Margin = new Thickness(0, 0, 0, 8),
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FrozenColumnCount = 1,
                RowHeight = 24,
                ColumnHeaderHeight = 28,
                FontSize = 11,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HorizontalGridLinesBrush = CreateBrush("#E5E7EB"),
                VerticalGridLinesBrush = CreateBrush("#E5E7EB")
            };

            grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
            grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

            grid.Resources.Add(typeof(DataGridCell), CreateCompactCellStyle());
            grid.Resources.Add(typeof(DataGridColumnHeader), CreateCompactHeaderStyle());

            grid.Columns.Add(CreateBranchColumn("Сотрудник", "EmployeeFullName", 200));
            grid.Columns.Add(CreateCenteredColumn("Янв", "January", 54));
            grid.Columns.Add(CreateCenteredColumn("Фев", "February", 54));
            grid.Columns.Add(CreateCenteredColumn("Мар", "March", 54));
            grid.Columns.Add(CreateCenteredColumn("Апр", "April", 54));
            grid.Columns.Add(CreateCenteredColumn("Май", "May", 54));
            grid.Columns.Add(CreateCenteredColumn("Июн", "June", 54));
            grid.Columns.Add(CreateCenteredColumn("Июл", "July", 54));
            grid.Columns.Add(CreateCenteredColumn("Авг", "August", 54));
            grid.Columns.Add(CreateCenteredColumn("Сен", "September", 54));
            grid.Columns.Add(CreateCenteredColumn("Окт", "October", 54));
            grid.Columns.Add(CreateCenteredColumn("Ноя", "November", 54));
            grid.Columns.Add(CreateCenteredColumn("Дек", "December", 54));

            stack.Children.Add(grid);

            stack.Children.Add(new Border
            {
                Background = CreateBrush("#F8FAFC"),
                BorderBrush = CreateBrush("#E5E7EB"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Child = new TextBlock
                {
                    Text =
                        $"Итого: Янв={block.TotalJanuary}, Фев={block.TotalFebruary}, Мар={block.TotalMarch}, Апр={block.TotalApril}, " +
                        $"Май={block.TotalMay}, Июн={block.TotalJune}, Июл={block.TotalJuly}, Авг={block.TotalAugust}, " +
                        $"Сен={block.TotalSeptember}, Окт={block.TotalOctober}, Ноя={block.TotalNovember}, Дек={block.TotalDecember}",
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
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
                CornerRadius = new CornerRadius(12),
                BorderBrush = CreateBrush("#E5E7EB"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#1F2937"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var grid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Height = 190,
                ItemsSource = rows,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                RowHeight = 24,
                ColumnHeaderHeight = 28,
                FontSize = 11,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                HorizontalGridLinesBrush = CreateBrush("#E5E7EB"),
                VerticalGridLinesBrush = CreateBrush("#E5E7EB")
            };

            grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
            grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

            grid.Resources.Add(typeof(DataGridCell), CreateCompactCellStyle());
            grid.Resources.Add(typeof(DataGridColumnHeader), CreateCompactHeaderStyle());

            grid.Columns.Add(CreateBranchColumn("Год", "Year", 80));
            grid.Columns.Add(CreateCenteredColumn("Янв", "January", 54));
            grid.Columns.Add(CreateCenteredColumn("Фев", "February", 54));
            grid.Columns.Add(CreateCenteredColumn("Мар", "March", 54));
            grid.Columns.Add(CreateCenteredColumn("Апр", "April", 54));
            grid.Columns.Add(CreateCenteredColumn("Май", "May", 54));
            grid.Columns.Add(CreateCenteredColumn("Июн", "June", 54));
            grid.Columns.Add(CreateCenteredColumn("Июл", "July", 54));
            grid.Columns.Add(CreateCenteredColumn("Авг", "August", 54));
            grid.Columns.Add(CreateCenteredColumn("Сен", "September", 54));
            grid.Columns.Add(CreateCenteredColumn("Окт", "October", 54));
            grid.Columns.Add(CreateCenteredColumn("Ноя", "November", 54));
            grid.Columns.Add(CreateCenteredColumn("Дек", "December", 54));

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
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 28.0));
            style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            style.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
            style.Setters.Add(new Setter(Control.BorderBrushProperty, CreateBrush("#D1D5DB")));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
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