using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public sealed class ScheduleBlockService
{
    public async Task EnsureStorageAsync(CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.CrmScheduleBlocks', N'U') IS NULL
CREATE TABLE dbo.CrmScheduleBlocks (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ClinicDataSourceId int NOT NULL, SourceDoctorId bigint NOT NULL,
 StartsAt datetime2 NOT NULL, EndsAt datetime2 NOT NULL, Title nvarchar(300) NOT NULL, Kind nvarchar(50) NOT NULL,
 CreatedByUserId int NOT NULL, CreatedAt datetime2 NOT NULL)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmScheduleBlocks_Resource_StartsAt')
CREATE INDEX IX_CrmScheduleBlocks_Resource_StartsAt ON dbo.CrmScheduleBlocks(ClinicDataSourceId, SourceDoctorId, StartsAt)
""", token);
    }

    public async Task AddAsync(CrmScheduleBlock block, CancellationToken token = default)
    {
        if (block.EndsAt <= block.StartsAt) throw new InvalidOperationException("Время окончания должно быть позже начала.");
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create(); db.CrmScheduleBlocks.Add(block); await db.SaveChangesAsync(token);
    }

    public async Task DeleteAsync(int id, CancellationToken token = default)
    {
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create();
        var block = await db.CrmScheduleBlocks.FindAsync([id], token);
        if (block is null) return;
        db.CrmScheduleBlocks.Remove(block); await db.SaveChangesAsync(token);
    }

    public async Task UpdateAsync(CrmScheduleBlock value, CancellationToken token = default)
    {
        if (value.EndsAt <= value.StartsAt) throw new InvalidOperationException("Время окончания должно быть позже начала.");
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create();
        var block = await db.CrmScheduleBlocks.FindAsync([value.Id], token);
        if (block is null) return;
        block.StartsAt = value.StartsAt; block.EndsAt = value.EndsAt; block.Title = value.Title; block.Kind = value.Kind;
        await db.SaveChangesAsync(token);
    }
}
