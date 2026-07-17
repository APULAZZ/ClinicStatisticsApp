using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.CallCenter.Services;

/// <summary>Synchronizes the three reference lists used by the call-center reports.</summary>
public sealed class MangoDirectorySyncService(AppDbContext db, IMangoApiClient api)
{
    public async Task SyncAllAsync(CancellationToken cancellationToken = default)
    {
        await SyncEmployeesAsync(cancellationToken);
        await SyncTopicsAsync(cancellationToken);
    }

    public async Task SyncEmployeesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in await api.GetUsersAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(item.Id)) continue;
            var entity = await db.CallCenterEmployees.FirstOrDefaultAsync(x => x.MangoUserId == item.Id, cancellationToken);
            if (entity is null)
            {
                db.CallCenterEmployees.Add(new CallCenterEmployee
                {
                    MangoUserId = item.Id, FullName = item.Name ?? item.Extension ?? item.Id,
                    Extension = item.Extension, IsActive = true
                });
            }
            else
            {
                entity.FullName = item.Name ?? entity.FullName;
                entity.Extension = item.Extension ?? entity.Extension;
                entity.IsActive = true;
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncGroupsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in await api.GetGroupsAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(item.Id)) continue;
            var entity = await db.CallCenterGroups.FirstOrDefaultAsync(x => x.MangoGroupId == item.Id, cancellationToken);
            if (entity is null)
                db.CallCenterGroups.Add(new CallCenterGroup { MangoGroupId = item.Id, Name = item.Name ?? item.Id, IsActive = true });
            else { entity.Name = item.Name ?? entity.Name; entity.IsActive = true; }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncTopicsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var item in await api.GetTopicsAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(item.Id)) continue;
            var entity = await db.CallCenterTopics.FirstOrDefaultAsync(x => x.MangoTopicId == item.Id, cancellationToken);
            if (entity is null)
                db.CallCenterTopics.Add(new CallCenterTopic { MangoTopicId = item.Id, Name = item.Name ?? item.Id, IsActive = true });
            else { entity.Name = item.Name ?? entity.Name; entity.IsActive = true; }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
