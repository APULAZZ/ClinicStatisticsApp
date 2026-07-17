namespace ClinicStatisticsApp.CallCenter.Services;

/// <summary>
/// The same synchronization sequence used by the original CallCenterStatisticsApp.
/// The separate UI projects use different models, but the operation order is kept identical.
/// </summary>
public sealed class MangoSynchronizationService(
    MangoDirectorySyncService directory,
    MangoCallImportService calls)
{
    public async Task<string> SynchronizeAsync(
        DateTime from,
        DateTime to,
        bool syncEmployees,
        bool syncTopics,
        CancellationToken cancellationToken = default)
    {
        var completed = new List<string>();

        if (syncEmployees)
        {
            await directory.SyncEmployeesAsync(cancellationToken);
            completed.Add("сотрудники");
        }

        if (syncTopics)
        {
            await directory.SyncTopicsAsync(cancellationToken);
            completed.Add("тематики");
        }

        await calls.ImportCallsAsync(from, to, cancellationToken);
        completed.Add("звонки");

        return $"Синхронизация завершена: {string.Join(", ", completed)}.";
    }
}
