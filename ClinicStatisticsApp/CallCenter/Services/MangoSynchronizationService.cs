namespace ClinicStatisticsApp.CallCenter.Services;

/// <summary>Synchronizes reference data and calls in the same order as the standalone application.</summary>
public sealed class MangoSynchronizationService(
    MangoDirectorySyncService directory,
    MangoCallImportService calls)
{
    public async Task<string> SynchronizeAsync(
        DateTime from,
        DateTime to,
        bool syncEmployees,
        bool syncTopics,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var completed = new List<string>();

        if (syncEmployees)
        {
            progress?.Report("Обновляем список сотрудников…");
            await directory.SyncEmployeesAsync(cancellationToken);
            completed.Add("сотрудники");
        }

        if (syncTopics)
        {
            progress?.Report("Обновляем справочник тематик…");
            await directory.SyncTopicsAsync(cancellationToken);
            completed.Add("тематики");
        }

        progress?.Report("Проверяем и обновляем журнал звонков…");
        await calls.EnsurePeriodImportedAsync(from, to, progress, cancellationToken);
        completed.Add("звонки");

        return $"Синхронизация завершена: {string.Join(", ", completed)}.";
    }
}
