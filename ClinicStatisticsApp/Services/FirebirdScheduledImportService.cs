using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

/// <summary>
/// Runs only while the CRM desktop application is open. It reads each configured
/// production Firebird source sequentially and records the result in CRM history.
/// Firebird access is performed exclusively through FirebirdPatientReader (SELECT).
/// </summary>
public sealed class FirebirdScheduledImportService : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(2);
    private readonly CancellationTokenSource _stop = new();
    private Task? _worker;

    public void Start()
    {
        if (_worker is null) _worker = RunAsync(_stop.Token);
    }

    private static async Task ImportAllAsync(CancellationToken token)
    {
        var options = FirebirdClinicOptionsLoader.Load();
        if (options.Count == 0) return;

        await using var lookupDb = DbContextFactory.Create();
        var enabledIds = await lookupDb.ClinicDataSources.AsNoTracking()
            .Where(x => x.IsActive && !x.IsTest).Select(x => x.Id).ToListAsync(token);

        foreach (var option in options.Where(x => enabledIds.Contains(x.ClinicDataSourceId)))
        {
            var started = DateTime.UtcNow;
            await using var db = DbContextFactory.Create();
            try
            {
                var snapshots = await new FirebirdPatientReader(option).ReadPatientsAsync(token);
                var result = await new ExternalPatientSynchronizationService(db).SynchronizeAsync(option.ClinicDataSourceId, snapshots, token);
                db.FirebirdImportRuns.Add(new FirebirdImportRun { ClinicDataSourceId = option.ClinicDataSourceId, StartedAt = started, FinishedAt = DateTime.UtcNow, IsSuccess = true, SourceCount = result.SourceCount, CreatedCount = result.CreatedCount, UpdatedCount = result.UpdatedCount });
                await db.SaveChangesAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                db.FirebirdImportRuns.Add(new FirebirdImportRun { ClinicDataSourceId = option.ClinicDataSourceId, StartedAt = started, FinishedAt = DateTime.UtcNow, IsSuccess = false, ErrorText = ex.Message });
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(Interval);
        try { while (await timer.WaitForNextTickAsync(token)) await ImportAllAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }
}
