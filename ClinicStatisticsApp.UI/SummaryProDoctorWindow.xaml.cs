using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClinicStatisticsApp.UI
{
    public partial class SummaryProDoctorWindow : Window
    {
        private readonly SummaryProDoctorService _service = new SummaryProDoctorService();
        private SummaryProDoctorResult? _currentResult;

        private readonly List<(SummaryProDoctorBranchBlockViewModel Block, TextBox[] QrBoxes)> _qrEditors = new();

        public SummaryProDoctorWindow()
        {
            InitializeComponent();
            LoadYears();
        }

        private int SelectedYear => YearComboBox.SelectedItem is int year
            ? year
            : DateTime.Now.Year;

        private void LoadYears()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 5; year <= currentYear + 2; year++)
            {
                YearComboBox.Items.Add(year);
            }

            YearComboBox.SelectedItem = currentYear;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentResult = _service.Build(SelectedYear);
                RenderBlocks();
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

        private void RenderBlocks()
        {
            BlocksPanel.Children.Clear();
            _qrEditors.Clear();

            if (_currentResult == null)
                return;

            BlocksPanel.Children.Add(new TextBlock
            {
                Text = $"Отзывы ПроДокторов за {SelectedYear} год",
                FontSize = 24,
                FontWeight = FontWeights.SemiBold,
                Foreground = CreateBrush("#1F2937"),
                Margin = new Thickness(0, 0, 0, 18)
            });

            foreach (var block in _currentResult.BranchBlocks)
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
                    Height = 230,
                    ItemsSource = block.Employees,
                    Margin = new Thickness(0, 0, 0, 12),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FrozenColumnCount = 1
                };

                grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
                grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

                grid.Columns.Add(CreateBranchColumn("Администратор", "EmployeeFullName", 220));
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

                var qrPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 12)
                };

                qrPanel.Children.Add(new TextBlock
                {
                    Text = "QR-код:",
                    Width = 100,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CreateBrush("#1F2937")
                });

                var qrBoxes = new[]
                {
                    CreateQrTextBox(block.QrJanuary),
                    CreateQrTextBox(block.QrFebruary),
                    CreateQrTextBox(block.QrMarch),
                    CreateQrTextBox(block.QrApril),
                    CreateQrTextBox(block.QrMay),
                    CreateQrTextBox(block.QrJune),
                    CreateQrTextBox(block.QrJuly),
                    CreateQrTextBox(block.QrAugust),
                    CreateQrTextBox(block.QrSeptember),
                    CreateQrTextBox(block.QrOctober),
                    CreateQrTextBox(block.QrNovember),
                    CreateQrTextBox(block.QrDecember)
                };

                foreach (var box in qrBoxes)
                {
                    qrPanel.Children.Add(box);
                }

                stack.Children.Add(qrPanel);

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
                            $"Итого: Янв={block.TotalJanuary}, Фев={block.TotalFebruary}, Мар={block.TotalMarch}, Апр={block.TotalApril}, Май={block.TotalMay}, Июн={block.TotalJune}, " +
                            $"Июл={block.TotalJuly}, Авг={block.TotalAugust}, Сен={block.TotalSeptember}, Окт={block.TotalOctober}, Ноя={block.TotalNovember}, Дек={block.TotalDecember}",
                        FontWeight = FontWeights.SemiBold,
                        Foreground = CreateBrush("#1D4ED8"),
                        TextWrapping = TextWrapping.Wrap
                    }
                });

                border.Child = stack;
                BlocksPanel.Children.Add(border);

                _qrEditors.Add((block, qrBoxes));
            }

            BlocksPanel.Children.Add(new Border
            {
                Background = CreateBrush("#EFF6FF"),
                BorderBrush = CreateBrush("#BFDBFE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 8, 0, 0),
                Child = new TextBlock
                {
                    Text =
                        $"Итого по филиалам: Янв={_currentResult.GrandTotalJanuary}, Фев={_currentResult.GrandTotalFebruary}, Мар={_currentResult.GrandTotalMarch}, " +
                        $"Апр={_currentResult.GrandTotalApril}, Май={_currentResult.GrandTotalMay}, Июн={_currentResult.GrandTotalJune}, " +
                        $"Июл={_currentResult.GrandTotalJuly}, Авг={_currentResult.GrandTotalAugust}, Сен={_currentResult.GrandTotalSeptember}, " +
                        $"Окт={_currentResult.GrandTotalOctober}, Ноя={_currentResult.GrandTotalNovember}, Дек={_currentResult.GrandTotalDecember}",
                    FontSize = 17,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CreateBrush("#1D4ED8"),
                    TextWrapping = TextWrapping.Wrap
                }
            });
        }

        private TextBox CreateQrTextBox(int value)
        {
            return new TextBox
            {
                Width = 56,
                Height = 36,
                Margin = new Thickness(2, 0, 2, 0),
                Text = value.ToString(),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentResult == null)
                    return;

                foreach (var item in _qrEditors)
                {
                    item.Block.QrJanuary = ParseBox(item.QrBoxes[0]);
                    item.Block.QrFebruary = ParseBox(item.QrBoxes[1]);
                    item.Block.QrMarch = ParseBox(item.QrBoxes[2]);
                    item.Block.QrApril = ParseBox(item.QrBoxes[3]);
                    item.Block.QrMay = ParseBox(item.QrBoxes[4]);
                    item.Block.QrJune = ParseBox(item.QrBoxes[5]);
                    item.Block.QrJuly = ParseBox(item.QrBoxes[6]);
                    item.Block.QrAugust = ParseBox(item.QrBoxes[7]);
                    item.Block.QrSeptember = ParseBox(item.QrBoxes[8]);
                    item.Block.QrOctober = ParseBox(item.QrBoxes[9]);
                    item.Block.QrNovember = ParseBox(item.QrBoxes[10]);
                    item.Block.QrDecember = ParseBox(item.QrBoxes[11]);
                }

                _service.SaveQrValues(SelectedYear, _currentResult.BranchBlocks);
                _currentResult = _service.Build(SelectedYear);
                RenderBlocks();

                MessageBox.Show(
                    "QR-коды сохранены.",
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

        private int ParseBox(TextBox box)
        {
            return int.TryParse(box.Text, out var value) ? value : 0;
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

        private SolidColorBrush CreateBrush(string hex)
        {
            return (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        }
    }
}