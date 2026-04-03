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

        private void LoadYears()
        {
            var currentYear = DateTime.Now.Year;

            for (int year = currentYear - 5; year <= currentYear + 2; year++)
            {
                YearComboBox.Items.Add(year);
            }

            YearComboBox.SelectedItem = currentYear;
        }

        private int SelectedYear => (int)(YearComboBox.SelectedItem ?? DateTime.Now.Year);

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _currentResult = _service.Build(SelectedYear);
                RenderBlocks();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenderBlocks()
        {
            BlocksPanel.Children.Clear();
            _qrEditors.Clear();

            if (_currentResult == null)
                return;

            foreach (var block in _currentResult.BranchBlocks)
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
                    Height = 220,
                    ItemsSource = block.Employees,
                    Margin = new Thickness(0, 0, 0, 10),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                grid.SetValue(ScrollViewer.CanContentScrollProperty, false);
                grid.PreviewMouseWheel += InnerDataGrid_PreviewMouseWheel;

                grid.Columns.Add(new DataGridTextColumn
                {
                    Header = "Администратор",
                    Binding = new System.Windows.Data.Binding("EmployeeFullName"),
                    Width = 220
                });
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

                var qrPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                qrPanel.Children.Add(new TextBlock
                {
                    Text = "QR-код:",
                    Width = 100,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.SemiBold
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

                stack.Children.Add(new TextBlock
                {
                    Text =
                        $"Итого: Янв={block.TotalJanuary}, Фев={block.TotalFebruary}, Мар={block.TotalMarch}, Апр={block.TotalApril}, Май={block.TotalMay}, Июн={block.TotalJune}, " +
                        $"Июл={block.TotalJuly}, Авг={block.TotalAugust}, Сен={block.TotalSeptember}, Окт={block.TotalOctober}, Ноя={block.TotalNovember}, Дек={block.TotalDecember}",
                    FontWeight = FontWeights.SemiBold
                });

                border.Child = stack;
                BlocksPanel.Children.Add(border);

                _qrEditors.Add((block, qrBoxes));
            }

            BlocksPanel.Children.Add(new TextBlock
            {
                Text =
                    $"Итого по филиалам: Янв={_currentResult.GrandTotalJanuary}, Фев={_currentResult.GrandTotalFebruary}, Мар={_currentResult.GrandTotalMarch}, " +
                    $"Апр={_currentResult.GrandTotalApril}, Май={_currentResult.GrandTotalMay}, Июн={_currentResult.GrandTotalJune}, " +
                    $"Июл={_currentResult.GrandTotalJuly}, Авг={_currentResult.GrandTotalAugust}, Сен={_currentResult.GrandTotalSeptember}, " +
                    $"Окт={_currentResult.GrandTotalOctober}, Ноя={_currentResult.GrandTotalNovember}, Дек={_currentResult.GrandTotalDecember}",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.DarkBlue,
                Margin = new Thickness(0, 10, 0, 10)
            });
        }

        private TextBox CreateQrTextBox(int value)
        {
            return new TextBox
            {
                Width = 55,
                Margin = new Thickness(2, 0, 2, 0),
                Text = value.ToString()
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

                MessageBox.Show("QR-коды сохранены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
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