using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Models;
using ClinicStatisticsApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace ClinicStatisticsApp.UI.Views;

public partial class FirebirdSyncPage : UserControl
{
    private readonly AppDbContext _db = DbContextFactory.Create();
    private IReadOnlyList<FirebirdClinicConnectionOptions> _options = Array.Empty<FirebirdClinicConnectionOptions>();

    public FirebirdSyncPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadSourcesAsync();
        Unloaded += (_, _) => _db.Dispose();
    }

    private async Task LoadSourcesAsync()
    {
        try
        {
            _options = FirebirdClinicOptionsLoader.Load();
            var sources = await ReadSourcesAsync();
            foreach (var source in sources) source.Name = $"{source.Name} ({source.Branch?.Name})";
            SourceComboBox.ItemsSource = sources;
            SourceComboBox.SelectedItem = sources.FirstOrDefault(x => _options.Any(o => o.ClinicDataSourceId == x.Id)) ?? sources.FirstOrDefault();
            RefreshSelection();
        }
        catch (Exception ex)
        {
            RunButton.IsEnabled = false;
            StatusTextBlock.Text = $"Не удалось загрузить источники CRM: {ex.Message}";
        }
    }

    private async Task<List<ClinicDataSource>> ReadSourcesAsync()
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT s.[Id], s.[BranchId], s.[Code], s.[Name], s.[IsTest], s.[IsActive], b.[Name] FROM [dbo].[ClinicDataSources] s INNER JOIN [dbo].[Branches] b ON b.[Id] = s.[BranchId] WHERE s.[IsActive] = 1 ORDER BY s.[IsTest], s.[Name]";
            var result = new List<ClinicDataSource>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(new ClinicDataSource
                {
                    Id = reader.GetInt32(0), BranchId = reader.GetInt32(1), Code = reader.GetString(2), Name = reader.GetString(3),
                    IsTest = reader.GetBoolean(4), IsActive = reader.GetBoolean(5), Branch = new Branch { Id = reader.GetInt32(1), Name = reader.GetString(6) }
                });
            return result;
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }

    private void SourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshSelection();

    private void RefreshSelection()
    {
        if (SourceComboBox.SelectedItem is not ClinicDataSource source) return;
        var configured = _options.Any(x => x.ClinicDataSourceId == source.Id);
        SourceTextBlock.Text = $"Источник: {source.Name}";
        RunButton.Content = $"Импортировать: {source.Name}";
        RunButton.IsEnabled = configured;
        StatusTextBlock.Text = configured
            ? "Источник настроен локально и доступен только для чтения."
            : "Для этого источника пока нет локальной настройки подключения. Добавьте его в firebird.Local.json; рабочая Firebird-база не изменяется.";
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceComboBox.SelectedItem is not ClinicDataSource source) return;
        var options = _options.SingleOrDefault(x => x.ClinicDataSourceId == source.Id);
        if (options is null) return;

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        var startedAt = DateTime.UtcNow;
        try
        {
            RunButton.IsEnabled = false;
            using var busy = App.Busy.Begin($"Читаем источник {source.Name}…");
            StatusTextBlock.Text = "Читаем карточки пациентов из Firebird…";
            var snapshots = await new FirebirdPatientReader(options).ReadPatientsAsync(deadline.Token);
            StatusTextBlock.Text = "Сохраняем разрешённые данные в CRM…";
            var result = await new ExternalPatientSynchronizationService(_db).SynchronizeAsync(source.Id, snapshots, deadline.Token);
            await RecordRunAsync(source.Id, startedAt, true, result, null);
            StatusTextBlock.Text = $"Импорт завершён: новых карт — {result.CreatedCount}, обновлено — {result.UpdatedCount}, всего в источнике — {result.SourceCount}.";
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            StatusTextBlock.Text = "Импорт остановлен по ограничению времени 15 минут.";
        }
        catch (Exception ex)
        {
            await RecordRunAsync(source.Id, startedAt, false, null, ex.Message);
            StatusTextBlock.Text = "Импорт не завершён. Подробности показаны в окне ошибки.";
            MessageBox.Show(ex.ToString(), "Ошибка импорта Firebird", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            RefreshSelection();
        }
    }

    private async void ImportAllButton_Click(object sender, RoutedEventArgs e)
    {
        var sources = (SourceComboBox.ItemsSource as IEnumerable<ClinicDataSource>)?
            .Where(x => !x.IsTest && _options.Any(o => o.ClinicDataSourceId == x.Id)).ToList() ?? [];
        if (sources.Count == 0)
        {
            MessageBox.Show("Нет настроенных рабочих источников для импорта.", "Импорт пациентов", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Будут последовательно импортированы {sources.Count} рабочих баз. Firebird-базы не изменяются. Продолжить?", "Импорт всех баз", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        using var deadline = new CancellationTokenSource(TimeSpan.FromHours(2));
        var completed = new List<string>();
        var failed = new List<string>();
        RunButton.IsEnabled = false;
        ImportAllButton.IsEnabled = false;
        SourceComboBox.IsEnabled = false;
        try
        {
            foreach (var source in sources)
            {
                try
                {
                    var startedAt = DateTime.UtcNow;
                    var options = _options.Single(x => x.ClinicDataSourceId == source.Id);
                    using var busy = App.Busy.Begin($"Импортируем {source.Name}…");
                    StatusTextBlock.Text = $"Читаем {source.Name}…";
                    var snapshots = await new FirebirdPatientReader(options).ReadPatientsAsync(deadline.Token);
                    StatusTextBlock.Text = $"Сохраняем {source.Name} в CRM…";
                    var result = await new ExternalPatientSynchronizationService(_db).SynchronizeAsync(source.Id, snapshots, deadline.Token);
                    await RecordRunAsync(source.Id, startedAt, true, result, null);
                    completed.Add($"{source.Name}: +{result.CreatedCount}, обновлено {result.UpdatedCount}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await RecordRunAsync(source.Id, DateTime.UtcNow, false, null, ex.Message);
                    failed.Add($"{source.Name}: {ex.Message}");
                }
            }

            StatusTextBlock.Text = $"Массовый импорт завершён. Успешно: {completed.Count}; с ошибками: {failed.Count}.";
            MessageBox.Show($"Успешно:\n{string.Join("\n", completed)}\n\nПроблемы:\n{(failed.Count == 0 ? "нет" : string.Join("\n", failed))}", "Импорт всех баз", MessageBoxButton.OK, failed.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = "Массовый импорт остановлен по ограничению времени 2 часа.";
        }
        finally
        {
            SourceComboBox.IsEnabled = true;
            ImportAllButton.IsEnabled = true;
            RefreshSelection();
        }
    }

    private async Task RecordRunAsync(int sourceId, DateTime startedAt, bool isSuccess, ExternalPatientSynchronizationResult? result, string? error)
    {
        _db.FirebirdImportRuns.Add(new FirebirdImportRun { ClinicDataSourceId = sourceId, StartedAt = startedAt, FinishedAt = DateTime.UtcNow, IsSuccess = isSuccess, SourceCount = result?.SourceCount, CreatedCount = result?.CreatedCount, UpdatedCount = result?.UpdatedCount, ErrorText = error });
        await _db.SaveChangesAsync();
    }

    private async void ShowHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP (30) s.[Name], r.[FinishedAt], r.[IsSuccess], r.[SourceCount], r.[CreatedCount], r.[UpdatedCount], r.[ErrorText] FROM [dbo].[FirebirdImportRuns] r INNER JOIN [dbo].[ClinicDataSources] s ON s.[Id] = r.[ClinicDataSourceId] ORDER BY r.[FinishedAt] DESC";
            var lines = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var source = reader.GetString(0); var time = reader.GetDateTime(1).ToLocalTime().ToString("dd.MM HH:mm");
                var state = reader.GetBoolean(2) ? $"успешно: {reader.GetInt32(3)} карт" : $"ошибка: {(reader.IsDBNull(6) ? "нет текста" : reader.GetString(6))}";
                lines.Add($"{time} · {source} · {state}");
            }
            MessageBox.Show(lines.Count == 0 ? "История пока пуста." : string.Join("\n", lines), "История импортов", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }
}
