using ClinicStatisticsApp.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClinicStatisticsApp.UI.Views;

public partial class VisitFunnelPage : UserControl
{
    private readonly VisitFunnelService _service = new();
    private VisitFunnelDashboard? _dashboard;
    private bool _specialistsLoaded;
    private bool _cancellationsLoaded;
    private bool _checksLoaded;

    public VisitFunnelPage()
    {
        InitializeComponent();
        FromDatePicker.SelectedDate = new DateTime(2024, 1, 1);
        ToDatePicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        RefreshButton.IsEnabled = false; StatusTextBlock.Text = "Обновляем посещения и записи рабочих филиалов…";
        try
        {
            var result = await _service.ImportWorkingSourcesAsync(from, to, new Progress<string>(text => StatusTextBlock.Text = text));
            StatusTextBlock.Text = string.Join(" · ", result.Sources.Select(x => x.Error is null ? $"{x.Source}: {x.Visits:N0} посещений" : $"{x.Source}: ошибка — {x.Error}"));
            await LoadAsync();
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Ошибка обновления: {ex.Message}"; }
        finally { RefreshButton.IsEnabled = true; }
    }

    private async void ShowButton_Click(object sender, RoutedEventArgs e) => await LoadAsync();

    private async Task LoadAsync()
    {
        if (!TryGetPeriod(out var from, out var to)) return;
        try
        {
            _dashboard = await _service.GetDashboardAsync(from, to);
            MonthlyGrid.ItemsSource = _dashboard.Monthly;
            var years = _dashboard.Monthly.Select(x => x.Month.Year).Distinct().OrderByDescending(x => x).ToList();
            YearComboBox.ItemsSource = years;
            YearComboBox.SelectedItem = years.FirstOrDefault();
            var branches = _dashboard.Monthly.Select(x => x.Branch).Distinct().OrderBy(x => x).Prepend("Все клиники").ToList();
            ChartBranchComboBox.ItemsSource = branches;
            ChartBranchComboBox.SelectedIndex = 0;
            ResetSecondaryTabs();
            RefreshMatrix();
            if (_dashboard.Monthly.Count == 0) StatusTextBlock.Text = "За выбранный период в рабочих филиалах ещё нет загруженных посещений. Нажмите «Обновить рабочие базы».";
        }
        catch (Exception ex) { StatusTextBlock.Text = $"Не удалось построить отчёт: {ex.Message}"; }
    }

    private void ResetSecondaryTabs()
    {
        _specialistsLoaded = _cancellationsLoaded = _checksLoaded = false;
        SpecialistsGrid.ItemsSource = null;
        CancellationsGrid.ItemsSource = null;
        ChecksGrid.ItemsSource = null;
    }

    private async void ReportTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source != ReportTabs || !TryGetPeriod(out var from, out var to)) return;
        try
        {
            switch (ReportTabs.SelectedIndex)
            {
                case 2 when !_specialistsLoaded:
                    StatusTextBlock.Text = "Загружаем итоги по специалистам…";
                    SpecialistsGrid.ItemsSource = await _service.GetSpecialistsAsync(from, to);
                    _specialistsLoaded = true;
                    StatusTextBlock.Text = "Итоги по специалистам загружены.";
                    break;
                case 3 when !_cancellationsLoaded:
                    StatusTextBlock.Text = "Проверяем снятые записи…";
                    CancellationsGrid.ItemsSource = await _service.GetCancellationsAsync(from, to);
                    _cancellationsLoaded = true;
                    StatusTextBlock.Text = "Снятые записи загружены.";
                    break;
                case 4 when !_checksLoaded:
                    StatusTextBlock.Text = "Загружаем записи для проверки карт…";
                    var checks = await _service.GetChecksAsync(from, to);
                    ChecksGrid.ItemsSource = checks.Rows;
                    _checksLoaded = true;
                    StatusTextBlock.Text = checks.IsTruncated
                        ? $"Для быстрого просмотра показаны последние {checks.DisplayLimit:N0} из {checks.Total:N0} записей. Сузьте период, чтобы увидеть все."
                        : $"Загружено записей: {checks.Total:N0}.";
                    break;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Не удалось загрузить вкладку: {ex.Message}";
        }
    }

    private void MatrixFilterChanged(object sender, SelectionChangedEventArgs e) => RefreshMatrix();
    private void ChartBranchChanged(object sender, SelectionChangedEventArgs e) => DrawChart();
    private void TrendCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawChart();

    private void RefreshMatrix()
    {
        if (_dashboard is null || YearComboBox.SelectedItem is not int year) return;
        ConfigureMatrixColumns();
        MatrixGrid.ItemsSource = VisitFunnelService.BuildTripleMatrix(_dashboard.Monthly, year);
        DrawChart();
    }

    private void ConfigureMatrixColumns()
    {
        MatrixGrid.Columns.Clear();
        MatrixGrid.Columns.Add(new DataGridTextColumn { Header = "Клиника", Binding = new Binding("Branch"), Width = 190 });
        var names = new[] { "Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек" };
        for (var month = 1; month <= 12; month++)
        {
            MatrixGrid.Columns.Add(CreateMetricColumn($"{names[month - 1]} · П", $"P{month:D2}", $"PatientBackgrounds[{month - 1}]", $"PatientTooltips[{month - 1}]", 70));
            MatrixGrid.Columns.Add(CreateMetricColumn($"{names[month - 1]} · Пос", $"V{month:D2}", $"VisitBackgrounds[{month - 1}]", $"VisitTooltips[{month - 1}]", 75));
            MatrixGrid.Columns.Add(CreateMetricColumn($"{names[month - 1]} · К", $"C{month:D2}", $"CoefficientBackgrounds[{month - 1}]", $"CoefficientTooltips[{month - 1}]", 60));
        }
        MatrixGrid.Columns.Add(new DataGridTextColumn { Header = "Итого · П", Binding = new Binding("TotalPatients"), Width = 80 });
        MatrixGrid.Columns.Add(new DataGridTextColumn { Header = "Итого · Пос", Binding = new Binding("TotalVisits"), Width = 85 });
        MatrixGrid.Columns.Add(new DataGridTextColumn { Header = "Итого · К", Binding = new Binding("TotalCoefficient"), Width = 75 });
    }

    private static DataGridTextColumn CreateMetricColumn(string header, string valuePath, string backgroundPath, string tooltipPath, double width)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.BackgroundProperty, new Binding(backgroundPath)));
        style.Setters.Add(new Setter(TextBlock.ToolTipProperty, new Binding(tooltipPath)));
        style.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2)));
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        return new DataGridTextColumn { Header = header, Binding = new Binding(valuePath), ElementStyle = style, Width = width };
    }

    private void DrawChart()
    {
        TrendCanvas.Children.Clear();
        if (_dashboard is null || YearComboBox.SelectedItem is not int year || ChartBranchComboBox.SelectedItem is not string branch || TrendCanvas.ActualWidth < 60 || TrendCanvas.ActualHeight < 80) return;
        ChartTitleText.Text = $"Динамика: {branch} · {year}";
        var patients = GetSeries(branch, year, VisitFunnelMetric.Patients);
        var visits = GetSeries(branch, year, VisitFunnelMetric.Visits);
        var coefficient = GetSeries(branch, year, VisitFunnelMetric.Coefficient);
        var max = Math.Max(1m, patients.Concat(visits).Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(0m).Max());
        var coefficientMax = Math.Max(1m, coefficient.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(0m).Max());
        var width = TrendCanvas.ActualWidth - 82; var height = TrendCanvas.ActualHeight - 36;
        for (var i = 0; i <= 4; i++)
        {
            var y = 8 + height * i / 4;
            TrendCanvas.Children.Add(new Line { X1 = 38, X2 = 38 + width, Y1 = y, Y2 = y, Stroke = new SolidColorBrush(Color.FromRgb(226, 232, 240)), StrokeThickness = 1 });
            var label = new TextBlock { Text = (max * (4 - i) / 4).ToString("N0"), FontSize = 10, Foreground = Brushes.Gray };
            Canvas.SetLeft(label, 0); Canvas.SetTop(label, y - 8); TrendCanvas.Children.Add(label);
            var right = new TextBlock { Text = (coefficientMax * (4 - i) / 4).ToString("N2"), FontSize = 10, Foreground = Brushes.DarkOrange };
            Canvas.SetLeft(right, 44 + width); Canvas.SetTop(right, y - 8); TrendCanvas.Children.Add(right);
        }
        DrawSeries(patients, width, height, max, Color.FromRgb(37, 99, 235), "Пациенты", "N0");
        DrawSeries(visits, width, height, max, Color.FromRgb(22, 163, 74), "Посещения", "N0");
        DrawSeries(coefficient, width, height, coefficientMax, Color.FromRgb(234, 88, 12), "Коэффициент", "N2");
        var names = new[] { "Янв", "Фев", "Мар", "Апр", "Май", "Июн", "Июл", "Авг", "Сен", "Окт", "Ноя", "Дек" };
        for (var i = 0; i < 12; i++)
        {
            var label = new TextBlock { Text = names[i], FontSize = 10, Foreground = Brushes.Gray };
            Canvas.SetLeft(label, 35 + width * i / 11 - 10); Canvas.SetTop(label, height + 14); TrendCanvas.Children.Add(label);
        }
    }

    private IReadOnlyList<decimal?> GetSeries(string branch, int year, VisitFunnelMetric metric)
    {
        var rows = _dashboard!.Monthly.Where(x => x.Month.Year == year && (branch == "Все клиники" || x.Branch == branch));
        return Enumerable.Range(1, 12).Select(month =>
        {
            var values = rows.Where(x => x.Month.Month == month).ToList();
            if (values.Count == 0) return null;
            return (decimal?)(metric == VisitFunnelMetric.Coefficient ? (values.Sum(x => x.Patients) == 0 ? 0m : (decimal)values.Sum(x => x.Visits) / values.Sum(x => x.Patients)) : values.Sum(x => VisitFunnelService.MetricValue(x, metric)));
        }).ToList();
    }

    private void DrawSeries(IReadOnlyList<decimal?> values, double width, double height, decimal max, Color color, string metric, string format)
    {
        Polyline? line = null;
        for (var i = 0; i < values.Count; i++)
        {
            if (values[i] is not decimal value) { line = null; continue; }
            line ??= new Polyline { Stroke = new SolidColorBrush(color), StrokeThickness = 2.4, StrokeLineJoin = PenLineJoin.Round };
            var point = new Point(38 + width * i / 11, 8 + height * (1 - (double)(value / max)));
            line.Points.Add(point);
            if (line.Points.Count == 1) TrendCanvas.Children.Add(line);
            var marker = new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(color), ToolTip = $"{metric}: {value.ToString(format)}" };
            Canvas.SetLeft(marker, point.X - 3); Canvas.SetTop(marker, point.Y - 3); TrendCanvas.Children.Add(marker);
        }
    }

    private bool TryGetPeriod(out DateTime from, out DateTime to)
    {
        from = FromDatePicker.SelectedDate ?? DateTime.Today; to = ToDatePicker.SelectedDate ?? DateTime.Today;
        if (from.Date <= to.Date) return true;
        StatusTextBlock.Text = "Дата начала не может быть позже даты окончания."; return false;
    }
}
