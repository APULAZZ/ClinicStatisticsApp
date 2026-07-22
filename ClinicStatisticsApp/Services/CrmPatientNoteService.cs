using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public sealed class CrmPatientNoteService
{
    public async Task EnsureStorageAsync(CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.CrmPatientNotes', N'U') IS NULL
CREATE TABLE dbo.CrmPatientNotes (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
 CrmPersonId int NOT NULL,
 AuthorUserId int NOT NULL,
 [Text] nvarchar(4000) NOT NULL,
 CreatedAt datetime2 NOT NULL,
 CONSTRAINT FK_CrmPatientNotes_CrmPersons FOREIGN KEY (CrmPersonId) REFERENCES dbo.CrmPersons(Id) ON DELETE CASCADE,
 CONSTRAINT FK_CrmPatientNotes_Users FOREIGN KEY (AuthorUserId) REFERENCES dbo.Users(Id))
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmPatientNotes_CrmPersonId_CreatedAt')
CREATE INDEX IX_CrmPatientNotes_CrmPersonId_CreatedAt ON dbo.CrmPatientNotes(CrmPersonId, CreatedAt DESC)
""", token);
    }

    public async Task<IReadOnlyList<CrmPatientNote>> GetAsync(int crmPersonId, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        return await db.CrmPatientNotes.AsNoTracking().Include(x => x.Author)
            .Where(x => x.CrmPersonId == crmPersonId).OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(token);
    }

    public async Task AddAsync(int crmPersonId, int authorUserId, string text, CancellationToken token = default)
    {
        var value = text.Trim();
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Введите текст заметки.");
        await using var db = DbContextFactory.Create();
        db.CrmPatientNotes.Add(new CrmPatientNote { CrmPersonId = crmPersonId, AuthorUserId = authorUserId, Text = value, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(token);
    }
}
