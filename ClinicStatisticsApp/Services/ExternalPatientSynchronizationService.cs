using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace ClinicStatisticsApp.Services;

/// <summary>
/// Copies only permitted contact data from a branch source into SQL Server.
/// It never writes to Firebird and never automatically joins different people.
/// </summary>
public sealed class ExternalPatientSynchronizationService
{
    private readonly AppDbContext _db;

    public ExternalPatientSynchronizationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ExternalPatientSynchronizationResult> SynchronizeAsync(
        int clinicDataSourceId,
        IReadOnlyList<FirebirdPatientSnapshot> snapshots,
        CancellationToken cancellationToken = default)
    {
        var dataSource = await _db.ClinicDataSources
            .SingleOrDefaultAsync(x => x.Id == clinicDataSourceId && x.IsActive, cancellationToken);
        if (dataSource is null)
            throw new InvalidOperationException($"Активный источник данных с ID {clinicDataSourceId} не найден в CRM.");

        // Do not materialize the full entity here. In some deployed SQL Server
        // instances EF generated a projection containing a stale BranchId mapping,
        // although the import needs only the local identifiers at this stage.
        var existingSourceIds = await ReadExistingSourceIdsAsync(clinicDataSourceId, cancellationToken);

        var now = DateTime.UtcNow;
        var created = 0;
        var updated = 0;

        var cards = new List<ExternalPatientCard>(snapshots.Count);
        foreach (var snapshot in snapshots)
        {
            var card = new ExternalPatientCard
            {
                BranchId = dataSource.BranchId,
                ClinicDataSourceId = clinicDataSourceId,
                SourcePatientId = snapshot.SourcePatientId
            };

            if (existingSourceIds.Contains(snapshot.SourcePatientId))
            {
                updated++;
            }
            else
            {
                created++;
            }

            ApplySnapshot(card, snapshot, now);
            cards.Add(card);
        }

        await WriteCardsAsync(cards, cancellationToken);
        await SynchronizePhoneIndexAsync(clinicDataSourceId, snapshots, cancellationToken);

        return new ExternalPatientSynchronizationResult(created, updated, snapshots.Count);
    }

    private async Task<HashSet<long>> ReadExistingSourceIdsAsync(
        int clinicDataSourceId,
        CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT [Id], [SourcePatientId] FROM [dbo].[ExternalPatientCards] WHERE [ClinicDataSourceId] = @clinicDataSourceId";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@clinicDataSourceId";
            parameter.Value = clinicDataSourceId;
            command.Parameters.Add(parameter);

            var sourceIds = new HashSet<long>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                sourceIds.Add(reader.GetInt64(1));

            return sourceIds;
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private async Task WriteCardsAsync(IReadOnlyCollection<ExternalPatientCard> cards, CancellationToken cancellationToken)
    {
        if (cards.Count == 0)
            return;

        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            if (connection is not SqlConnection sqlConnection)
                throw new InvalidOperationException("CRM-импорт поддерживает только Microsoft SQL Server.");

            await using var transaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync(cancellationToken);
            await using (var setupCommand = sqlConnection.CreateCommand())
            {
                setupCommand.Transaction = transaction;
                setupCommand.CommandText = "SET XACT_ABORT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; CREATE TABLE #ImportCards ([BranchId] int NOT NULL, [ClinicDataSourceId] int NOT NULL, [SourcePatientId] bigint NOT NULL, [SourceCardNumber] nvarchar(100) NULL, [LastName] nvarchar(100) NOT NULL, [FirstName] nvarchar(100) NOT NULL, [MiddleName] nvarchar(100) NULL, [DateOfBirth] datetime2 NULL, [MobilePhone] nvarchar(50) NULL, [NormalizedMobilePhone] nvarchar(32) NULL, [Email] nvarchar(320) NULL, [NormalizedEmail] nvarchar(320) NULL, [IsActive] bit NOT NULL, [LastSyncedAt] datetime2 NOT NULL, [SourceFingerprint] nvarchar(128) NULL);";
                await setupCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            using (var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, transaction))
            {
                bulkCopy.DestinationTableName = "#ImportCards";
                await bulkCopy.WriteToServerAsync(CreateImportTable(cards), cancellationToken);
            }

            await using var mergeCommand = sqlConnection.CreateCommand();
            mergeCommand.Transaction = transaction;
            mergeCommand.CommandText = "UPDATE [target] SET [SourceCardNumber] = [source].[SourceCardNumber], [LastName] = [source].[LastName], [FirstName] = [source].[FirstName], [MiddleName] = [source].[MiddleName], [DateOfBirth] = [source].[DateOfBirth], [MobilePhone] = [source].[MobilePhone], [NormalizedMobilePhone] = [source].[NormalizedMobilePhone], [Email] = [source].[Email], [NormalizedEmail] = [source].[NormalizedEmail], [IsActive] = [source].[IsActive], [LastSyncedAt] = [source].[LastSyncedAt], [SourceFingerprint] = [source].[SourceFingerprint] FROM [dbo].[ExternalPatientCards] AS [target] INNER JOIN #ImportCards AS [source] ON [source].[ClinicDataSourceId] = [target].[ClinicDataSourceId] AND [source].[SourcePatientId] = [target].[SourcePatientId]; INSERT INTO [dbo].[ExternalPatientCards] ([BranchId], [ClinicDataSourceId], [SourcePatientId], [SourceCardNumber], [LastName], [FirstName], [MiddleName], [DateOfBirth], [MobilePhone], [NormalizedMobilePhone], [Email], [NormalizedEmail], [IsActive], [LastSyncedAt], [SourceFingerprint]) SELECT [source].[BranchId], [source].[ClinicDataSourceId], [source].[SourcePatientId], [source].[SourceCardNumber], [source].[LastName], [source].[FirstName], [source].[MiddleName], [source].[DateOfBirth], [source].[MobilePhone], [source].[NormalizedMobilePhone], [source].[Email], [source].[NormalizedEmail], [source].[IsActive], [source].[LastSyncedAt], [source].[SourceFingerprint] FROM #ImportCards AS [source] WHERE NOT EXISTS (SELECT 1 FROM [dbo].[ExternalPatientCards] AS [target] WHERE [target].[ClinicDataSourceId] = [source].[ClinicDataSourceId] AND [target].[SourcePatientId] = [source].[SourcePatientId]);";
            await mergeCommand.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection)
                await connection.CloseAsync();
        }
    }

    private async Task SynchronizePhoneIndexAsync(int clinicDataSourceId, IReadOnlyList<FirebirdPatientSnapshot> snapshots, CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();
        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection) await connection.OpenAsync(cancellationToken);
        try
        {
            if (connection is not SqlConnection sqlConnection)
                throw new InvalidOperationException("CRM-импорт поддерживает только Microsoft SQL Server.");
            await using var transaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync(cancellationToken);
            await using (var setup = sqlConnection.CreateCommand())
            {
                setup.Transaction = transaction;
                setup.CommandText = """
IF OBJECT_ID(N'dbo.CrmPatientContactPhones', N'U') IS NULL
CREATE TABLE dbo.CrmPatientContactPhones (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ClinicDataSourceId int NOT NULL, SourcePatientId bigint NOT NULL,
 PhoneKind nvarchar(20) NOT NULL, OriginalPhone nvarchar(100) NOT NULL, NormalizedPhone nvarchar(32) NOT NULL, SyncedAt datetime2 NOT NULL,
 CONSTRAINT UQ_CrmPatientContactPhones UNIQUE(ClinicDataSourceId, SourcePatientId, PhoneKind, NormalizedPhone));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmPatientContactPhones_NormalizedPhone') CREATE INDEX IX_CrmPatientContactPhones_NormalizedPhone ON dbo.CrmPatientContactPhones(NormalizedPhone);
DELETE FROM dbo.CrmPatientContactPhones WHERE ClinicDataSourceId = @sourceId;
""";
                setup.Parameters.AddWithValue("@sourceId", clinicDataSourceId);
                await setup.ExecuteNonQueryAsync(cancellationToken);
            }
            var phones = CreatePhoneIndexTable(clinicDataSourceId, snapshots);
            if (phones.Rows.Count > 0)
            {
                using var bulkCopy = new SqlBulkCopy(sqlConnection, SqlBulkCopyOptions.Default, transaction) { DestinationTableName = "dbo.CrmPatientContactPhones" };
                // The destination begins with the identity column Id, whereas
                // the in-memory table deliberately does not. Map by name so a
                // phone kind can never be shifted into SourcePatientId.
                foreach (DataColumn column in phones.Columns)
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                await bulkCopy.WriteToServerAsync(phones, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        finally
        {
            if (closeConnection) await connection.CloseAsync();
        }
    }

    private static DataTable CreatePhoneIndexTable(int clinicDataSourceId, IEnumerable<FirebirdPatientSnapshot> snapshots)
    {
        var table = new DataTable();
        table.Columns.Add("ClinicDataSourceId", typeof(int)); table.Columns.Add("SourcePatientId", typeof(long)); table.Columns.Add("PhoneKind", typeof(string));
        table.Columns.Add("OriginalPhone", typeof(string)); table.Columns.Add("NormalizedPhone", typeof(string)); table.Columns.Add("SyncedAt", typeof(DateTime));
        var unique = new HashSet<(long PatientId, string Kind, string Phone)>();
        var syncedAt = DateTime.UtcNow;
        foreach (var snapshot in snapshots)
        {
            foreach (var (kind, original) in new[] { ("Mobile", snapshot.MobilePhone), ("Work", snapshot.WorkPhone), ("Home", snapshot.HomePhone) })
            {
                var normalized = NormalizePhone(original);
                if (normalized is null || string.IsNullOrWhiteSpace(original) || !unique.Add((snapshot.SourcePatientId, kind, normalized))) continue;
                table.Rows.Add(clinicDataSourceId, snapshot.SourcePatientId, kind, original.Trim(), normalized, syncedAt);
            }
        }
        return table;
    }

    private static DataTable CreateImportTable(IEnumerable<ExternalPatientCard> cards)
    {
        var table = new DataTable();
        table.Columns.Add("BranchId", typeof(int)); table.Columns.Add("ClinicDataSourceId", typeof(int)); table.Columns.Add("SourcePatientId", typeof(long));
        table.Columns.Add("SourceCardNumber", typeof(string)); table.Columns.Add("LastName", typeof(string)); table.Columns.Add("FirstName", typeof(string));
        table.Columns.Add("MiddleName", typeof(string)); table.Columns.Add("DateOfBirth", typeof(DateTime)); table.Columns.Add("MobilePhone", typeof(string));
        table.Columns.Add("NormalizedMobilePhone", typeof(string)); table.Columns.Add("Email", typeof(string)); table.Columns.Add("NormalizedEmail", typeof(string));
        table.Columns.Add("IsActive", typeof(bool)); table.Columns.Add("LastSyncedAt", typeof(DateTime)); table.Columns.Add("SourceFingerprint", typeof(string));
        foreach (var card in cards)
            table.Rows.Add(card.BranchId, card.ClinicDataSourceId!.Value, card.SourcePatientId,
                (object?)card.SourceCardNumber ?? DBNull.Value, card.LastName, card.FirstName,
                (object?)card.MiddleName ?? DBNull.Value, (object?)card.DateOfBirth ?? DBNull.Value,
                (object?)card.MobilePhone ?? DBNull.Value, (object?)card.NormalizedMobilePhone ?? DBNull.Value,
                (object?)card.Email ?? DBNull.Value, (object?)card.NormalizedEmail ?? DBNull.Value,
                card.IsActive, card.LastSyncedAt, (object?)card.SourceFingerprint ?? DBNull.Value);
        return table;
    }

    private static void ApplySnapshot(ExternalPatientCard card, FirebirdPatientSnapshot snapshot, DateTime syncedAt)
    {
        // In several clinic databases NUM_CARD is empty; the stable Firebird ID
        // is the actual card identifier used by staff and is preserved on copy.
        card.SourceCardNumber = EmptyToNull(snapshot.SourceCardNumber) ?? snapshot.SourcePatientId.ToString();
        card.LastName = snapshot.LastName.Trim();
        card.FirstName = snapshot.FirstName.Trim();
        card.MiddleName = EmptyToNull(snapshot.MiddleName);
        card.DateOfBirth = ToSqlServerDateOrNull(snapshot.DateOfBirth);
        card.MobilePhone = EmptyToNull(snapshot.MobilePhone);
        card.NormalizedMobilePhone = NormalizePhone(snapshot.MobilePhone) ?? NormalizePhone(snapshot.WorkPhone) ?? NormalizePhone(snapshot.HomePhone);
        card.Email = EmptyToNull(snapshot.Email);
        card.NormalizedEmail = NormalizeEmail(snapshot.Email);
        card.LastSyncedAt = syncedAt;
        card.SourceFingerprint = CreateFingerprint(snapshot);
    }

    internal static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 10) digits = "7" + digits;
        if (digits.Length == 11 && digits[0] == '8') digits = "7" + digits[1..];
        return digits.Length is >= 10 and <= 15 ? "+" + digits : null;
    }

    internal static string? NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var email = value.Trim().ToLowerInvariant();
        return email.Contains('@') ? email : null;
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime? ToSqlServerDateOrNull(DateTime? value)
    {
        if (value is null || value.Value.Date < System.Data.SqlTypes.SqlDateTime.MinValue.Value.Date)
            return null;

        return value.Value.Date;
    }

    private static string CreateFingerprint(FirebirdPatientSnapshot snapshot)
    {
        var source = string.Join("|", snapshot.SourcePatientId, snapshot.SourceCardNumber, snapshot.LastName, snapshot.FirstName,
            snapshot.MiddleName, snapshot.DateOfBirth?.ToString("yyyy-MM-dd"), snapshot.MobilePhone, snapshot.WorkPhone,
            snapshot.HomePhone, snapshot.Email);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
}

public sealed record ExternalPatientSynchronizationResult(int CreatedCount, int UpdatedCount, int SourceCount);
