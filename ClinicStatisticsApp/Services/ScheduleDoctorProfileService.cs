using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

/// <summary>Stores explicit cross-branch doctor matches in SQL CRM only.</summary>
public sealed class ScheduleDoctorProfileService
{
    public async Task EnsureStorageAsync(CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.CrmScheduleDoctorProfiles', N'U') IS NULL
CREATE TABLE dbo.CrmScheduleDoctorProfiles (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, Name nvarchar(200) NOT NULL, CreatedAt datetime2 NOT NULL);
IF OBJECT_ID(N'dbo.CrmScheduleDoctorProfileLinks', N'U') IS NULL
CREATE TABLE dbo.CrmScheduleDoctorProfileLinks (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ProfileId int NOT NULL, ClinicDataSourceId int NOT NULL, SourceDoctorId bigint NOT NULL, DoctorName nvarchar(200) NOT NULL);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'UX_CrmScheduleDoctorProfileLinks_Source')
CREATE UNIQUE INDEX UX_CrmScheduleDoctorProfileLinks_Source ON dbo.CrmScheduleDoctorProfileLinks(ClinicDataSourceId, SourceDoctorId);
""", token);
    }

    public async Task<IReadOnlyList<CrmScheduleDoctorProfile>> GetProfilesAsync(CancellationToken token = default)
    {
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create();
        return await db.CrmScheduleDoctorProfiles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(token);
    }

    public async Task SaveAsync(string name, IEnumerable<(int SourceId, long DoctorId, string DoctorName)> links, CancellationToken token = default)
    {
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create();
        var profile = await db.CrmScheduleDoctorProfiles.FirstOrDefaultAsync(x => x.Name == name, token);
        if (profile is null) { profile = new CrmScheduleDoctorProfile { Name = name }; db.CrmScheduleDoctorProfiles.Add(profile); await db.SaveChangesAsync(token); }
        db.CrmScheduleDoctorProfileLinks.RemoveRange(db.CrmScheduleDoctorProfileLinks.Where(x => x.ProfileId == profile.Id));
        foreach (var item in links.Distinct()) db.CrmScheduleDoctorProfileLinks.Add(new CrmScheduleDoctorProfileLink { ProfileId = profile.Id, ClinicDataSourceId = item.SourceId, SourceDoctorId = item.DoctorId, DoctorName = item.DoctorName });
        await db.SaveChangesAsync(token);
    }

    public async Task<IReadOnlyList<(int SourceId, long DoctorId)>> GetLinksAsync(string profileName, CancellationToken token = default)
    {
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create();
        var rows = await (from profile in db.CrmScheduleDoctorProfiles.AsNoTracking()
                      join link in db.CrmScheduleDoctorProfileLinks.AsNoTracking() on profile.Id equals link.ProfileId
                      where profile.Name == profileName select new { link.ClinicDataSourceId, link.SourceDoctorId })
            .ToListAsync(token);
        return rows.Select(x => (x.ClinicDataSourceId, x.SourceDoctorId)).ToList();
    }

    public async Task<IReadOnlyList<(string ProfileName, string DoctorName, int SourceId)>> GetAllLinksAsync(CancellationToken token = default)
    {
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create();
        var rows = await (from profile in db.CrmScheduleDoctorProfiles.AsNoTracking() join link in db.CrmScheduleDoctorProfileLinks.AsNoTracking() on profile.Id equals link.ProfileId select new { profile.Name, link.DoctorName, link.ClinicDataSourceId }).OrderBy(x => x.Name).ToListAsync(token);
        return rows.Select(x => (x.Name, x.DoctorName, x.ClinicDataSourceId)).ToList();
    }

    public async Task DeleteAsync(string profileName, CancellationToken token = default)
    {
        await EnsureStorageAsync(token); await using var db = DbContextFactory.Create(); var profile = await db.CrmScheduleDoctorProfiles.FirstOrDefaultAsync(x => x.Name == profileName, token); if (profile is null) return;
        db.CrmScheduleDoctorProfileLinks.RemoveRange(db.CrmScheduleDoctorProfileLinks.Where(x => x.ProfileId == profile.Id)); db.CrmScheduleDoctorProfiles.Remove(profile); await db.SaveChangesAsync(token);
    }
}
