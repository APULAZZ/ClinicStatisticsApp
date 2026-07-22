using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public sealed class PatientDossierSnapshotService
{
    public async Task EnsureStorageAsync(CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.PatientDossierSnapshots', N'U') IS NULL
CREATE TABLE dbo.PatientDossierSnapshots (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ExternalPatientCardId int NOT NULL UNIQUE,
 PayloadJson nvarchar(max) NOT NULL, RefreshedAt datetime2 NOT NULL, IsSuccess bit NOT NULL,
 ErrorText nvarchar(2000) NULL,
 CONSTRAINT FK_PatientDossierSnapshots_ExternalPatientCards FOREIGN KEY (ExternalPatientCardId) REFERENCES dbo.ExternalPatientCards(Id) ON DELETE CASCADE)
""", token);
    }

    public async Task<PatientDossierSnapshot?> GetAsync(int cardId, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        return await db.PatientDossierSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.ExternalPatientCardId == cardId, token);
    }
    public async Task SaveAsync(int cardId, string payloadJson, bool success, string? error, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        var snapshot = await db.PatientDossierSnapshots.SingleOrDefaultAsync(x => x.ExternalPatientCardId == cardId, token);
        if (snapshot is null) { snapshot = new PatientDossierSnapshot { ExternalPatientCardId = cardId }; db.PatientDossierSnapshots.Add(snapshot); }
        snapshot.PayloadJson = payloadJson; snapshot.IsSuccess = success; snapshot.ErrorText = error; snapshot.RefreshedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(token);
    }
}
