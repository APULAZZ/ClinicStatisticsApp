using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
namespace ClinicStatisticsApp.Services;
public sealed class ScheduleFavoriteDoctorService
{
    public async Task EnsureStorageAsync() { await using var db = DbContextFactory.Create(); await db.Database.ExecuteSqlRawAsync("IF OBJECT_ID(N'dbo.CrmScheduleFavoriteDoctors',N'U') IS NULL CREATE TABLE dbo.CrmScheduleFavoriteDoctors (Id int IDENTITY(1,1) PRIMARY KEY, UserId int NOT NULL, DoctorName nvarchar(200) NOT NULL, CONSTRAINT UX_CrmScheduleFavoriteDoctors UNIQUE(UserId,DoctorName))"); }
    public async Task<HashSet<string>> GetAsync(int userId) { await EnsureStorageAsync(); await using var db = DbContextFactory.Create(); return (await db.CrmScheduleFavoriteDoctors.Where(x => x.UserId == userId).Select(x => x.DoctorName).ToListAsync()).ToHashSet(); }
    public async Task ToggleAsync(int userId, string name) { await EnsureStorageAsync(); await using var db = DbContextFactory.Create(); var item = await db.CrmScheduleFavoriteDoctors.FirstOrDefaultAsync(x => x.UserId == userId && x.DoctorName == name); if (item is null) db.CrmScheduleFavoriteDoctors.Add(new CrmScheduleFavoriteDoctor { UserId = userId, DoctorName = name }); else db.CrmScheduleFavoriteDoctors.Remove(item); await db.SaveChangesAsync(); }
}
