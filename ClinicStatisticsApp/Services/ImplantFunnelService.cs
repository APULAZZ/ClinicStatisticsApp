using System.Data;
using System.Data.Common;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.CallCenter.Services;
using ClinicStatisticsApp.Integrations.Firebird;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClinicStatisticsApp.Services;

/// <summary>
/// CRM-owned import and reporting store for the implants landing funnel.
/// No data is written to Firebird; appointments and payments are read from the
/// existing CRM analytics warehouse.
/// </summary>
public sealed class ImplantFunnelService
{
    public async Task<int> ImportAsync(string fileName, IReadOnlyCollection<ImplantLeadInput> leads, CancellationToken token = default)
    {
        await EnsureStorageAsync(token);
        await using var db = DbContextFactory.Create();
        await db.Database.OpenConnectionAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var connection = db.Database.GetDbConnection();
        var inserted = 0;
        foreach (var lead in leads.Where(x => x.OccurredAt != default && !string.IsNullOrWhiteSpace(x.Phone)))
        {
            var phone = NormalizePhone(lead.Phone);
            if (phone is null) continue;
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{lead.Type}|{lead.OccurredAt:O}|{phone}|{lead.OperatorName}|{lead.SourceKey}")));
            await using var command = connection.CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = """
IF NOT EXISTS (SELECT 1 FROM dbo.ImplantFunnelLeads WHERE Fingerprint = @fingerprint)
BEGIN
 INSERT INTO dbo.ImplantFunnelLeads (Fingerprint, SourceFileName, SourceKey, LeadType, OccurredAt, Phone, NormalizedPhone, OperatorName, Details, ImportedAt)
 VALUES (@fingerprint, @fileName, @sourceKey, @leadType, @occurredAt, @phone, @normalizedPhone, @operatorName, @details, SYSUTCDATETIME());
 SELECT CAST(1 AS int);
END
ELSE SELECT CAST(0 AS int);
""";
            Add(command, "@fingerprint", fingerprint); Add(command, "@fileName", fileName); Add(command, "@sourceKey", lead.SourceKey);
            Add(command, "@leadType", lead.Type); Add(command, "@occurredAt", lead.OccurredAt); Add(command, "@phone", lead.Phone);
            Add(command, "@normalizedPhone", phone); Add(command, "@operatorName", lead.OperatorName); Add(command, "@details", lead.Details);
            inserted += Convert.ToInt32(await command.ExecuteScalarAsync(token));
        }
        await transaction.CommitAsync(token);
        return inserted;
    }

    /// <summary>
    /// Loads MANGO calls and tags once, then stores the tag-based classification next to
    /// the leads in the CRM-owned funnel table. The original Firebird databases are not involved.
    /// </summary>
    public async Task<ImplantFunnelMangoImportResult> ImportMangoAsync(DateTime from, DateTime to, IProgress<string>? progress = null, CancellationToken token = default)
    {
        if (from.Date > to.Date) throw new ArgumentException("Дата начала периода не может быть позже даты окончания.");
        await EnsureStorageAsync(token);
        await using var db = DbContextFactory.Create();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        var api = new MangoApiClient(http, MangoApiOptionsLoader.Load());

        progress?.Report("MANGO: обновляем справочник тематик…");
        await new MangoDirectorySyncService(db, api).SyncTopicsAsync(token);

        var sourceLeads = await db.Database.SqlQuery<ImplantFunnelMangoLead>($"""
SELECT Id, LeadType, OccurredAt, NormalizedPhone
FROM dbo.ImplantFunnelLeads
WHERE OccurredAt >= {from.Date} AND OccurredAt < {to.Date.AddDays(1)}
""").ToListAsync(token);
        var sourceLeadsByPhone = sourceLeads.GroupBy(x => x.NormalizedPhone).ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);
        bool IsCallFromSourceFile(MangoCallDto call)
        {
            var phone = NormalizePhone(call.PhoneNumber);
            if (phone is null || !sourceLeadsByPhone.TryGetValue(phone, out var candidates)) return false;
            return candidates.Any(lead =>
            {
                var expectedDirection = string.Equals(lead.LeadType, "Звонок", StringComparison.OrdinalIgnoreCase) ? "incoming" : "outgoing";
                return string.Equals(call.Direction, expectedDirection, StringComparison.OrdinalIgnoreCase)
                       && Math.Abs((call.CallDateTime - lead.OccurredAt).TotalHours) <= 3;
            });
        }

        // The source export is in Moscow time while the clinic works in Kaliningrad.
        // At midnight this requires only one technical hour from the previous date;
        // the progress itself always starts with the date selected by the user.
        var importer = new MangoCallImportService(db, api);
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            progress?.Report($"──────────────────── {day:dd.MM.yyyy} ────────────────────");
            progress?.Report($"MANGO: {day:dd.MM.yyyy} — получаем список звонков…");
            var requestFrom = day == from.Date ? day.AddHours(-1) : day;
            var requestTo = day == to.Date ? day.AddDays(1).AddHours(-1).AddSeconds(-1) : day.AddDays(1).AddSeconds(-1);
            try
            {
                await importer.ImportCallsAsync(requestFrom, requestTo, progress, token, IsCallFromSourceFile);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                progress?.Report($"MANGO: {day:dd.MM.yyyy} — ошибка: {ex.Message}");
            }
        }

        progress?.Report("MANGO: сопоставляем звонки из файла с тематиками…");
        var result = await ApplyMangoClassificationsAsync(db, from, to, token);
        progress?.Report($"MANGO: готово. Лидов с тегом — {result.WithTag:N0}; запись — {result.Booked:N0}; не запись — {result.NotBooked:N0}; сброс — {result.Dropped:N0}; без тега — {result.Unclassified:N0}.");
        return result;
    }

    private static async Task<ImplantFunnelMangoImportResult> ApplyMangoClassificationsAsync(AppDbContext db, DateTime from, DateTime to, CancellationToken token)
    {
        var leads = await db.Database.SqlQuery<ImplantFunnelMangoLead>($"""
SELECT Id, LeadType, OccurredAt, NormalizedPhone
FROM dbo.ImplantFunnelLeads
WHERE OccurredAt >= {from.Date} AND OccurredAt < {to.Date.AddDays(1)}
""").ToListAsync(token);
        var calls = await db.CallCenterCallRecords.AsNoTracking().Include(x => x.Topic)
            .Where(x => x.CallDateTime >= from.Date.AddDays(-1) && x.CallDateTime < to.Date.AddDays(2))
            .Where(x => x.TopicId != null)
            .ToListAsync(token);
        var callsByPhone = calls
            .Where(x => NormalizePhone(x.ExternalPhoneNumber) is not null)
            .GroupBy(x => NormalizePhone(x.ExternalPhoneNumber)!)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
UPDATE dbo.ImplantFunnelLeads
SET MangoCallId = NULL, MangoTopicName = NULL, MangoClassification = NULL, MangoMatchedAt = NULL
WHERE OccurredAt >= {from.Date} AND OccurredAt < {to.Date.AddDays(1)}
""", token);

        var withTag = 0; var booked = 0; var notBooked = 0; var dropped = 0;
        foreach (var lead in leads)
        {
            if (!callsByPhone.TryGetValue(lead.NormalizedPhone, out var candidates)) continue;
            var direction = string.Equals(lead.LeadType, "Звонок", StringComparison.OrdinalIgnoreCase) ? "incoming" : "outgoing";
            var match = candidates
                .Where(x => string.Equals(x.Direction, direction, StringComparison.OrdinalIgnoreCase))
                .Where(x => Math.Abs((x.CallDateTime - lead.OccurredAt).TotalHours) <= 3)
                .OrderBy(x => Math.Abs((x.CallDateTime - lead.OccurredAt).TotalMinutes))
                .FirstOrDefault();
            if (match?.Topic?.Name is not { Length: > 0 } topicName) continue;

            var classification = ClassifyMangoTopic(topicName);
            withTag++;
            if (classification == "Запись") booked++;
            else if (classification == "Не запись") notBooked++;
            else if (classification == "Сброс") dropped++;

            await db.Database.ExecuteSqlInterpolatedAsync($"""
UPDATE dbo.ImplantFunnelLeads
SET MangoCallId = {match.MangoCallId}, MangoTopicName = {topicName}, MangoClassification = {classification}, MangoMatchedAt = SYSUTCDATETIME()
WHERE Id = {lead.Id}
""", token);
        }
        return new(leads.Count, withTag, booked, notBooked, dropped, leads.Count - withTag);
    }

    private static string ClassifyMangoTopic(string topicName) => CallCenterTopicCatalog.GetKind(topicName) switch
    {
        CallCenterTopicKind.Plan or CallCenterTopicKind.Perk => "Запись",
        CallCenterTopicKind.NoAppointment => "Не запись",
        CallCenterTopicKind.Drop => "Сброс",
        _ => "Без классификации"
    };

    public async Task<ImplantFunnelDashboard> GetDashboardAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        if (from.Date > to.Date) throw new ArgumentException("Дата начала периода не может быть позже даты окончания.");
        await EnsureStorageAsync(token);
        await using var db = DbContextFactory.Create();
        await db.Database.OpenConnectionAsync(token);
        var connection = db.Database.GetDbConnection();
        const string mappedLeads = """
WITH RelevantLeads AS (
 SELECT Id, LeadType, OccurredAt, Phone, NormalizedPhone, OperatorName, MangoClassification FROM dbo.ImplantFunnelLeads
 WHERE OccurredAt >= @from AND OccurredAt < @toExclusive
), Mapped AS (
 SELECT l.*, e.ClinicDataSourceId, e.SourcePatientId, e.EventOccurredAt, e.MedmUserName, a.AppointmentDate, a.IsNoShow, a.IsCancelled, attended.AttendedAt
 FROM RelevantLeads l
 OUTER APPLY (
   SELECT TOP (1) e.ClinicDataSourceId, e.SourcePatientId, e.OccurredAt AS EventOccurredAt, e.MedmUserName
   FROM dbo.CrmPatientContactPhones c
   JOIN dbo.ImplantFunnelMedmEvents e ON e.ClinicDataSourceId = c.ClinicDataSourceId AND e.SourcePatientId = c.SourcePatientId
   WHERE e.EventTypeCode = 12 AND c.NormalizedPhone = l.NormalizedPhone
     AND e.OccurredAt >= l.OccurredAt AND e.OccurredAt < DATEADD(day, 7, l.OccurredAt)
     AND (l.OperatorName IS NULL OR EXISTS (
       SELECT 1 FROM dbo.ImplantFunnelOperatorMappings m
       WHERE l.OperatorName LIKE N'%' + m.CallTrackingOperator + N'%'
         AND e.MedmUserId = m.MedmUserId))
   ORDER BY e.OccurredAt
 ) e
 OUTER APPLY (
   SELECT TOP (1) ap.AppointmentDate, ap.IsNoShow, ap.IsCancelled
   FROM dbo.CrmAnalyticsAppointments ap
   WHERE ap.ClinicDataSourceId = e.ClinicDataSourceId AND ap.SourcePatientId = e.SourcePatientId AND ap.AppointmentDate >= e.EventOccurredAt
   ORDER BY ap.AppointmentDate
 ) a
 OUTER APPLY (
   SELECT TOP (1) e2.OccurredAt AS AttendedAt
   FROM dbo.ImplantFunnelMedmEvents e2
   WHERE e2.ClinicDataSourceId = e.ClinicDataSourceId AND e2.SourcePatientId = e.SourcePatientId AND e2.EventTypeCode = 22
     AND CAST(e2.OccurredAt AS date) = CAST(a.AppointmentDate AS date)
   ORDER BY e2.OccurredAt
 ) attended
), UniqueMapped AS (
 SELECT *, ROW_NUMBER() OVER (PARTITION BY ClinicDataSourceId, SourcePatientId ORDER BY EventOccurredAt, Id) AS PatientRow
 FROM Mapped
 WHERE ClinicDataSourceId IS NOT NULL
), MatchedPatients AS (
 SELECT Id, LeadType, OccurredAt, Phone, NormalizedPhone, OperatorName, ClinicDataSourceId, SourcePatientId, EventOccurredAt, MedmUserName, AppointmentDate, IsNoShow, IsCancelled, AttendedAt
 FROM UniqueMapped
 WHERE PatientRow = 1
)
""";
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = mappedLeads + """
SELECT
 (SELECT COUNT(*) FROM RelevantLeads) AS LeadCount,
 (SELECT COUNT(*) FROM RelevantLeads WHERE MangoClassification = N'Запись') AS MangoBooked,
 (SELECT COUNT(*) FROM RelevantLeads WHERE MangoClassification = N'Не запись') AS MangoNotBooked,
 (SELECT COUNT(*) FROM RelevantLeads WHERE MangoClassification = N'Сброс') AS MangoDropped,
 (SELECT COUNT(*) FROM RelevantLeads WHERE MangoClassification IS NULL OR MangoClassification = N'Без классификации') AS MangoWithoutTag,
 COUNT(*) AS AppointmentCount,
 SUM(CASE WHEN AppointmentDate < CAST(GETDATE() AS date) AND AttendedAt IS NOT NULL THEN 1 ELSE 0 END) AS AttendedCount,
 SUM(CASE WHEN ClinicDataSourceId IS NOT NULL AND IsNoShow = 1 THEN 1 ELSE 0 END) AS NoShowCount,
 SUM(CASE WHEN ClinicDataSourceId IS NOT NULL AND IsCancelled = 1 THEN 1 ELSE 0 END) AS CancelledCount,
 (SELECT COALESCE(SUM(p.Amount), 0) FROM dbo.CrmAnalyticsPayments p WHERE EXISTS (
   SELECT 1 FROM MatchedPatients m WHERE m.ClinicDataSourceId = p.ClinicDataSourceId AND m.SourcePatientId = p.SourcePatientId AND p.PaymentDate >= m.AppointmentDate
 )) AS PaymentTotal
FROM MatchedPatients;
""";
        Add(command, "@from", from.Date); Add(command, "@toExclusive", to.Date.AddDays(1));
        await using var reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        var leads = ReadInt(reader, 0); var mangoBooked = ReadInt(reader, 1); var mangoNotBooked = ReadInt(reader, 2); var mangoDropped = ReadInt(reader, 3); var mangoWithoutTag = ReadInt(reader, 4);
        var appointments = ReadInt(reader, 5); var notNoShow = ReadInt(reader, 6); var noShows = ReadInt(reader, 7); var cancelled = ReadInt(reader, 8); var payments = ReadDecimal(reader, 9);

        var branches = new List<ImplantFunnelBranchRow>();
        await using var branchCommand = connection.CreateCommand();
        branchCommand.CommandTimeout = 120;
        branchCommand.CommandText = mappedLeads + """
 , PaymentsByMatch AS (
 SELECT m.Id, COALESCE(SUM(p.Amount), 0) AS PaymentTotal
 FROM MatchedPatients m
 LEFT JOIN dbo.CrmAnalyticsPayments p ON p.ClinicDataSourceId = m.ClinicDataSourceId AND p.SourcePatientId = m.SourcePatientId AND p.PaymentDate >= m.AppointmentDate
 GROUP BY m.Id
 )
 , BranchTotals AS (
 SELECT m.ClinicDataSourceId, COUNT(*) AS Appointments,
  SUM(CASE WHEN m.AppointmentDate < CAST(GETDATE() AS date) AND m.AttendedAt IS NOT NULL THEN 1 ELSE 0 END) AS Attended,
  COALESCE(SUM(p.PaymentTotal), 0) AS PaymentTotal
 FROM MatchedPatients m LEFT JOIN PaymentsByMatch p ON p.Id = m.Id
 GROUP BY m.ClinicDataSourceId
 )
SELECT s.Name, COALESCE(t.Appointments, 0), COALESCE(t.Attended, 0), COALESCE(t.PaymentTotal, 0)
FROM dbo.ClinicDataSources s
LEFT JOIN BranchTotals t ON t.ClinicDataSourceId = s.Id
WHERE s.IsTest = 0
ORDER BY COALESCE(t.Appointments, 0) DESC, s.Name;
""";
        Add(branchCommand, "@from", from.Date); Add(branchCommand, "@toExclusive", to.Date.AddDays(1));
        await using var branchReader = await branchCommand.ExecuteReaderAsync(token);
        while (await branchReader.ReadAsync(token)) branches.Add(new(branchReader.GetString(0), ReadInt(branchReader, 1), ReadInt(branchReader, 2), ReadDecimal(branchReader, 3)));

        var maximum = Math.Max(leads, 1);
        var stages = new[]
        {
            new ImplantFunnelStage("Заявки и звонки", leads, " (100% от лидов)", 620d),
            new ImplantFunnelStage("Подтверждено в MedM", appointments, $" ({Percent(appointments, leads):N1}% от лидов)", Math.Max(8, 620d * appointments / maximum)),
            new ImplantFunnelStage("Подтверждённая явка", notNoShow, $" ({Percent(notNoShow, appointments):N1}% от записей)", Math.Max(8, 620d * notNoShow / maximum)),
            new ImplantFunnelStage("Оплаты после записи", payments, string.Empty, 0)
        };
        var budget = await GetBudgetAsync(from, to, token);
        var dynamics = await GetDynamicsAsync(db, from, to, token);
        return new(leads, mangoBooked, mangoNotBooked, mangoDropped, mangoWithoutTag, appointments, notNoShow, noShows, cancelled, payments, appointments == 0 ? 0m : payments / appointments, budget.Amount, budget.IsConfigured, stages, branches, dynamics);
    }

    /// <summary>CRM-only audit list for checking automatic lead-to-MedM matching.</summary>
    public async Task<IReadOnlyList<ImplantFunnelAuditRow>> GetMatchingAuditAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        await EnsureStorageAsync(token);
        await using var db = DbContextFactory.Create();
        await db.Database.OpenConnectionAsync(token);
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandTimeout = 120;
        command.CommandText = """
WITH RelevantLeads AS (
 SELECT Id, LeadType, OccurredAt, Phone, NormalizedPhone, OperatorName
 FROM dbo.ImplantFunnelLeads
 WHERE OccurredAt >= @from AND OccurredAt < @toExclusive
), Mapped AS (
 SELECT l.*, e.ClinicDataSourceId, e.SourcePatientId, e.OccurredAt AS EventOccurredAt
 FROM RelevantLeads l
 OUTER APPLY (
   SELECT TOP (1) e.ClinicDataSourceId, e.SourcePatientId, e.OccurredAt
   FROM dbo.CrmPatientContactPhones c
   JOIN dbo.ImplantFunnelMedmEvents e ON e.ClinicDataSourceId = c.ClinicDataSourceId AND e.SourcePatientId = c.SourcePatientId
   WHERE e.EventTypeCode = 12 AND c.NormalizedPhone = l.NormalizedPhone
     AND e.OccurredAt >= l.OccurredAt AND e.OccurredAt < DATEADD(day, 7, l.OccurredAt)
     AND (l.OperatorName IS NULL OR EXISTS (
       SELECT 1 FROM dbo.ImplantFunnelOperatorMappings m
       WHERE l.OperatorName LIKE N'%' + m.CallTrackingOperator + N'%'
         AND e.MedmUserId = m.MedmUserId))
   ORDER BY e.OccurredAt
 ) e
), WithAppointment AS (
 SELECT m.*, ap.AppointmentDate
 FROM Mapped m
 OUTER APPLY (
   SELECT TOP (1) a.AppointmentDate
   FROM dbo.CrmAnalyticsAppointments a
   WHERE a.ClinicDataSourceId = m.ClinicDataSourceId AND a.SourcePatientId = m.SourcePatientId AND a.AppointmentDate >= m.EventOccurredAt
   ORDER BY a.AppointmentDate
 ) ap
)
SELECT OccurredAt, LeadType, Phone, COALESCE(OperatorName, N'—') AS OperatorName,
 CASE WHEN ClinicDataSourceId IS NULL THEN N'Не найдена запись'
      WHEN AppointmentDate IS NULL THEN N'Запись найдена, дата приёма не загружена'
      ELSE N'Запись найдена' END AS Status,
 COALESCE(s.Name, N'—') AS BranchName, SourcePatientId, AppointmentDate
FROM WithAppointment a
LEFT JOIN dbo.ClinicDataSources s ON s.Id = a.ClinicDataSourceId
ORDER BY CASE WHEN ClinicDataSourceId IS NULL THEN 0 ELSE 1 END, OccurredAt;
""";
        Add(command, "@from", from.Date); Add(command, "@toExclusive", to.Date.AddDays(1));
        var result = new List<ImplantFunnelAuditRow>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            result.Add(new ImplantFunnelAuditRow(
                reader.GetDateTime(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.IsDBNull(6) ? null : Convert.ToInt64(reader.GetValue(6)), reader.IsDBNull(7) ? null : reader.GetDateTime(7)));
        }
        return result;
    }

    /// <summary>Builds local CRM comparison rows; no Mango or Firebird request is made here.</summary>
    public async Task<IReadOnlyList<ImplantFunnelComparisonRow>> GetComparisonAsync(DateTime from, DateTime to, ImplantFunnelComparisonGranularity granularity, IProgress<string>? progress = null, CancellationToken token = default)
    {
        var start = granularity == ImplantFunnelComparisonGranularity.Year
            ? new DateTime(from.Year, 1, 1)
            : new DateTime(from.Year, from.Month, 1);
        var result = new List<ImplantFunnelComparisonRow>();
        var cursor = start;
        while (cursor <= to.Date)
        {
            var next = granularity == ImplantFunnelComparisonGranularity.Year ? cursor.AddYears(1) : cursor.AddMonths(1);
            var periodTo = (next.AddDays(-1) > to.Date ? to.Date : next.AddDays(-1));
            progress?.Report($"Сравнение: рассчитываем {PeriodName(cursor, granularity)}…");
            var dashboard = await GetDashboardAsync(cursor, periodTo, token);
            result.Add(new ImplantFunnelComparisonRow(
                PeriodName(cursor, granularity), dashboard.LeadCount, dashboard.MangoBookedCount, dashboard.AppointmentCount,
                dashboard.NotMarkedNoShowCount, dashboard.PaymentTotal, dashboard.AverageCheck, dashboard.Budget,
                0, 0, 0, 0, 0, 0, 0));
            cursor = next;
        }

        var maxLeads = Math.Max(1, result.Max(x => x.Leads));
        for (var i = 0; i < result.Count; i++)
        {
            var row = result[i]; var previous = i == 0 ? null : result[i - 1];
            result[i] = row with
            {
                LeadDeltaPercent = Delta(row.Leads, previous?.Leads),
                AppointmentDeltaPercent = Delta(row.MedmAppointments, previous?.MedmAppointments),
                VisitDeltaPercent = Delta(row.Attended, previous?.Attended),
                RevenueDeltaPercent = Delta(row.Revenue, previous?.Revenue),
                LeadBarWidth = 150d * row.Leads / maxLeads,
                MedmBarWidth = row.MedmAppointments == 0 ? 0 : Math.Max(5, 150d * row.MedmAppointments / maxLeads),
                VisitBarWidth = row.Attended == 0 ? 0 : Math.Max(5, 150d * row.Attended / maxLeads)
            };
        }
        return result;
    }

    private static string PeriodName(DateTime date, ImplantFunnelComparisonGranularity granularity) =>
        granularity == ImplantFunnelComparisonGranularity.Year ? date.Year.ToString() : date.ToString("MMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
    private static decimal? Delta(decimal current, decimal? previous) => previous is null || previous == 0 ? null : 100m * (current - previous.Value) / previous.Value;

    private static async Task<IReadOnlyList<ImplantFunnelMonthlyRow>> GetDynamicsAsync(AppDbContext db, DateTime from, DateTime to, CancellationToken token)
    {
        var rows = await db.Database.SqlQuery<ImplantFunnelMonthlyRaw>($"""
SELECT DATEFROMPARTS(YEAR(OccurredAt), MONTH(OccurredAt), 1) AS MonthStart,
 COUNT(*) AS Leads,
 SUM(CASE WHEN MangoClassification = N'Запись' THEN 1 ELSE 0 END) AS MangoBooked
FROM dbo.ImplantFunnelLeads
WHERE OccurredAt >= {from.Date} AND OccurredAt < {to.Date.AddDays(1)}
GROUP BY DATEFROMPARTS(YEAR(OccurredAt), MONTH(OccurredAt), 1)
ORDER BY MonthStart
""").ToListAsync(token);
        var maximum = Math.Max(1, rows.Select(x => x.Leads).DefaultIfEmpty(1).Max());
        return rows.Select(x => new ImplantFunnelMonthlyRow(
            x.MonthStart.ToString("MMM yy", new System.Globalization.CultureInfo("ru-RU")), x.Leads, x.MangoBooked, Percent(x.MangoBooked, x.Leads),
            260d * x.Leads / maximum, x.MangoBooked == 0 ? 0 : Math.Max(6d, 260d * x.MangoBooked / maximum))).ToList();
    }

    public async Task SaveBudgetAsync(DateTime from, DateTime to, decimal amount, CancellationToken token = default)
    {
        await EnsureStorageAsync(token);
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
MERGE dbo.ImplantFunnelBudgets AS target
USING (SELECT {from.Date} AS PeriodFrom, {to.Date} AS PeriodTo) AS source
ON target.PeriodFrom = source.PeriodFrom AND target.PeriodTo = source.PeriodTo
WHEN MATCHED THEN UPDATE SET Amount = {amount}, UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (PeriodFrom, PeriodTo, Amount, UpdatedAt) VALUES ({from.Date}, {to.Date}, {amount}, SYSUTCDATETIME());
""", token);
    }

    public async Task<ImplantFunnelEventImportResult> ImportMedmEventsAsync(DateTime from, DateTime to, IProgress<string>? progress = null, CancellationToken token = default)
    {
        await EnsureStorageAsync(token);
        await using var metadataDb = DbContextFactory.Create();
        var testSourceIds = await metadataDb.ClinicDataSources.AsNoTracking().Where(x => x.IsTest).Select(x => x.Id).ToListAsync(token);
        var sources = FirebirdClinicOptionsLoader.Load().Where(x => !testSourceIds.Contains(x.ClinicDataSourceId)).ToList();
        var result = new List<ImplantFunnelEventImportSourceResult>();
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            progress?.Report($"Журнал MedM: источник {index + 1} из {sources.Count}…");
            try
            {
                var events = await new FirebirdImplantEventReader(source).ReadAppointmentEventsAsync(from, to, token);
                await ReplaceMedmEventsAsync(source.ClinicDataSourceId, from, to, events, token);
                result.Add(new(source.ClinicDataSourceId, events.Count, null));
            }
            catch (Exception ex) { result.Add(new(source.ClinicDataSourceId, 0, ex.Message)); }
        }
        return new(from, to, result);
    }

    public async Task<ImplantFunnelPatientImportResult> ImportPatientPhonesAsync(IProgress<string>? progress = null, CancellationToken token = default)
    {
        await using var metadataDb = DbContextFactory.Create();
        var testSourceIds = await metadataDb.ClinicDataSources.AsNoTracking().Where(x => x.IsTest).Select(x => x.Id).ToListAsync(token);
        var sources = FirebirdClinicOptionsLoader.Load().Where(x => !testSourceIds.Contains(x.ClinicDataSourceId)).ToList();
        var result = new List<ImplantFunnelPatientImportSourceResult>();
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            progress?.Report($"Карточки и телефоны: источник {index + 1} из {sources.Count}…");
            try
            {
                var snapshots = await new FirebirdPatientReader(source).ReadPatientsAsync(token);
                await using var db = DbContextFactory.Create();
                var sync = await new ExternalPatientSynchronizationService(db).SynchronizeAsync(source.ClinicDataSourceId, snapshots, token);
                result.Add(new(source.ClinicDataSourceId, sync.SourceCount, null));
                progress?.Report($"Карточки и телефоны: источник {index + 1} из {sources.Count} — готово, карточек: {sync.SourceCount:N0}.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                progress?.Report("Обновление карточек и телефонов остановлено.");
                throw;
            }
            catch (Exception ex)
            {
                result.Add(new(source.ClinicDataSourceId, 0, ex.Message));
                progress?.Report($"Карточки и телефоны: источник {index + 1} из {sources.Count} — ошибка: {ex.Message}");
            }
        }
        return new(result);
    }

    private static async Task ReplaceMedmEventsAsync(int sourceId, DateTime from, DateTime to, IReadOnlyList<FirebirdImplantEventRow> events, CancellationToken token)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.OpenConnectionAsync(token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var connection = db.Database.GetDbConnection();
        await using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction.GetDbTransaction();
            delete.CommandText = "DELETE FROM dbo.ImplantFunnelMedmEvents WHERE ClinicDataSourceId = @sourceId AND OccurredAt >= @from AND OccurredAt < @to";
            Add(delete, "@sourceId", sourceId); Add(delete, "@from", from.Date); Add(delete, "@to", to.Date.AddDays(1));
            await delete.ExecuteNonQueryAsync(token);
        }
        foreach (var row in events)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction.GetDbTransaction();
            insert.CommandText = "INSERT INTO dbo.ImplantFunnelMedmEvents (ClinicDataSourceId, SourceEventId, SourcePatientId, OccurredAt, MedmUserId, MedmUserName, ComputerName, EventTypeCode, EventTypeName, EventText, SyncedAt) VALUES (@sourceId, @eventId, @patientId, @occurredAt, @userId, @userName, @computer, @typeCode, @typeName, @text, SYSUTCDATETIME())";
            Add(insert, "@sourceId", sourceId); Add(insert, "@eventId", row.SourceEventId); Add(insert, "@patientId", row.SourcePatientId); Add(insert, "@occurredAt", row.OccurredAt);
            Add(insert, "@userId", row.MedmUserId); Add(insert, "@userName", row.MedmUserName); Add(insert, "@computer", row.ComputerName); Add(insert, "@typeCode", row.EventTypeCode); Add(insert, "@typeName", row.EventTypeName); Add(insert, "@text", row.EventText);
            await insert.ExecuteNonQueryAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    private async Task<ImplantFunnelBudgetValue> GetBudgetAsync(DateTime from, DateTime to, CancellationToken token)
    {
        await using var db = DbContextFactory.Create();
        return await db.Database.SqlQuery<ImplantFunnelBudgetValue>($"""
SELECT COALESCE((SELECT Amount FROM dbo.ImplantFunnelBudgets WHERE PeriodFrom = {from.Date} AND PeriodTo = {to.Date}), 0) AS Amount,
 CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.ImplantFunnelBudgets WHERE PeriodFrom = {from.Date} AND PeriodTo = {to.Date}) THEN 1 ELSE 0 END AS bit) AS IsConfigured
""").SingleAsync(token);
    }

    public async Task EnsureStorageAsync(CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.ImplantFunnelLeads', N'U') IS NULL
CREATE TABLE dbo.ImplantFunnelLeads (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, Fingerprint char(64) NOT NULL, SourceFileName nvarchar(260) NOT NULL, SourceKey nvarchar(300) NOT NULL,
 LeadType nvarchar(30) NOT NULL, OccurredAt datetime2 NOT NULL, Phone nvarchar(80) NOT NULL, NormalizedPhone nvarchar(32) NOT NULL,
 OperatorName nvarchar(300) NULL, Details nvarchar(2000) NULL, ImportedAt datetime2 NOT NULL,
 CONSTRAINT UQ_ImplantFunnelLeads_Fingerprint UNIQUE(Fingerprint));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ImplantFunnelLeads_OccurredAt') CREATE INDEX IX_ImplantFunnelLeads_OccurredAt ON dbo.ImplantFunnelLeads(OccurredAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ImplantFunnelLeads_NormalizedPhone') CREATE INDEX IX_ImplantFunnelLeads_NormalizedPhone ON dbo.ImplantFunnelLeads(NormalizedPhone);
IF COL_LENGTH(N'dbo.ImplantFunnelLeads', N'MangoCallId') IS NULL ALTER TABLE dbo.ImplantFunnelLeads ADD MangoCallId nvarchar(100) NULL;
IF COL_LENGTH(N'dbo.ImplantFunnelLeads', N'MangoTopicName') IS NULL ALTER TABLE dbo.ImplantFunnelLeads ADD MangoTopicName nvarchar(300) NULL;
IF COL_LENGTH(N'dbo.ImplantFunnelLeads', N'MangoClassification') IS NULL ALTER TABLE dbo.ImplantFunnelLeads ADD MangoClassification nvarchar(40) NULL;
IF COL_LENGTH(N'dbo.ImplantFunnelLeads', N'MangoMatchedAt') IS NULL ALTER TABLE dbo.ImplantFunnelLeads ADD MangoMatchedAt datetime2 NULL;
UPDATE dbo.ImplantFunnelLeads SET NormalizedPhone = N'+' + REPLACE(NormalizedPhone, N'+', N'') WHERE NormalizedPhone NOT LIKE N'+%';
IF OBJECT_ID(N'dbo.CrmPatientContactPhones', N'U') IS NULL
CREATE TABLE dbo.CrmPatientContactPhones (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ClinicDataSourceId int NOT NULL, SourcePatientId bigint NOT NULL,
 PhoneKind nvarchar(20) NOT NULL, OriginalPhone nvarchar(100) NOT NULL, NormalizedPhone nvarchar(32) NOT NULL, SyncedAt datetime2 NOT NULL,
 CONSTRAINT UQ_CrmPatientContactPhones UNIQUE(ClinicDataSourceId, SourcePatientId, PhoneKind, NormalizedPhone));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmPatientContactPhones_NormalizedPhone') CREATE INDEX IX_CrmPatientContactPhones_NormalizedPhone ON dbo.CrmPatientContactPhones(NormalizedPhone);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmPatientContactPhones_Match') CREATE INDEX IX_CrmPatientContactPhones_Match ON dbo.CrmPatientContactPhones(NormalizedPhone, ClinicDataSourceId, SourcePatientId);
IF OBJECT_ID(N'dbo.ImplantFunnelOperatorMappings', N'U') IS NULL
CREATE TABLE dbo.ImplantFunnelOperatorMappings (
 CallTrackingOperator nvarchar(100) NOT NULL PRIMARY KEY, MedmUserId bigint NOT NULL, MedmUserName nvarchar(200) NOT NULL, UpdatedAt datetime2 NOT NULL);
MERGE dbo.ImplantFunnelOperatorMappings AS target
USING (VALUES
 (N'КЦ Настя', CAST(1823 AS bigint), N'Боцевич Анастасия Ильинична'),
 (N'КЦ Наташа', CAST(1671 AS bigint), N'Лисовская Наталья Александровна'),
 (N'КЦ Инна', CAST(1663 AS bigint), N'Соленкова Инна Игоревна'),
 (N'КЦ Лилия', CAST(1648 AS bigint), N'Ковбасюк Лилия Сергеевна'),
 (N'КЦ Вероника', CAST(1791 AS bigint), N'Карпенко Вероника Викторовна'),
 (N'КЦ Лена', CAST(1628 AS bigint), N'Дынько Елена Петровна'),
 (N'КЦ Таня', CAST(1787 AS bigint), N'Шлейникова Татьяна Витальевна'),
 (N'КЦ Ксения', CAST(1776 AS bigint), N'Останина Ксения Константиновна')
) AS source(CallTrackingOperator, MedmUserId, MedmUserName)
ON target.CallTrackingOperator = source.CallTrackingOperator
WHEN NOT MATCHED THEN INSERT (CallTrackingOperator, MedmUserId, MedmUserName, UpdatedAt) VALUES (source.CallTrackingOperator, source.MedmUserId, source.MedmUserName, SYSUTCDATETIME());
IF OBJECT_ID(N'dbo.ImplantFunnelMedmEvents', N'U') IS NULL
CREATE TABLE dbo.ImplantFunnelMedmEvents (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ClinicDataSourceId int NOT NULL, SourceEventId bigint NOT NULL, SourcePatientId bigint NOT NULL, OccurredAt datetime2 NOT NULL,
 MedmUserId bigint NULL, MedmUserName nvarchar(200) NULL, ComputerName nvarchar(200) NULL, EventTypeCode int NULL, EventTypeName nvarchar(200) NULL, EventText nvarchar(2000) NULL, SyncedAt datetime2 NOT NULL,
 CONSTRAINT UQ_ImplantFunnelMedmEvents_Source UNIQUE(ClinicDataSourceId, SourceEventId));
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ImplantFunnelMedmEvents_PatientTime') CREATE INDEX IX_ImplantFunnelMedmEvents_PatientTime ON dbo.ImplantFunnelMedmEvents(ClinicDataSourceId, SourcePatientId, OccurredAt);
IF OBJECT_ID(N'dbo.ImplantFunnelBudgets', N'U') IS NULL
CREATE TABLE dbo.ImplantFunnelBudgets (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, PeriodFrom date NOT NULL, PeriodTo date NOT NULL, Amount decimal(18,2) NOT NULL, UpdatedAt datetime2 NOT NULL, CONSTRAINT UQ_ImplantFunnelBudgets_Period UNIQUE(PeriodFrom, PeriodTo));
""", token);
    }

    public static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits[0] is '7' or '8') digits = "7" + digits[1..];
        return digits.Length is >= 10 and <= 15 ? "+" + digits : null;
    }

    private static decimal Percent(decimal numerator, decimal denominator) => denominator == 0 ? 0 : 100m * numerator / denominator;
    private static void Add(DbCommand command, string name, object? value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value ?? DBNull.Value; command.Parameters.Add(parameter); }
    private static int ReadInt(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0 : Convert.ToInt32(reader.GetValue(ordinal));
    private static decimal ReadDecimal(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? 0m : Convert.ToDecimal(reader.GetValue(ordinal));
}

public sealed record ImplantLeadInput(string SourceKey, string Type, DateTime OccurredAt, string Phone, string? OperatorName, string? Details);
public sealed record ImplantFunnelMangoLead(int Id, string LeadType, DateTime OccurredAt, string NormalizedPhone);
public sealed record ImplantFunnelMangoImportResult(int Leads, int WithTag, int Booked, int NotBooked, int Dropped, int Unclassified);
public sealed record ImplantFunnelStage(string Name, decimal Value, string ConversionText, double BarWidth);
public sealed record ImplantFunnelBranchRow(string BranchName, int Appointments, int NotMarkedNoShow, decimal PaymentTotal)
{
    public decimal AverageCheck => Appointments == 0 ? 0m : PaymentTotal / Appointments;
}
public sealed record ImplantFunnelMonthlyRaw(DateTime MonthStart, int Leads, int MangoBooked);
public sealed record ImplantFunnelMonthlyRow(string Month, int Leads, int MangoBooked, decimal MangoPercent, double LeadBarWidth, double MangoBarWidth);
public sealed record ImplantFunnelAuditRow(DateTime LeadDate, string LeadType, string Phone, string OperatorName, string Status, string BranchName, long? PatientCardNumber, DateTime? AppointmentDate);
public sealed record ImplantFunnelBudgetValue(decimal Amount, bool IsConfigured);
public enum ImplantFunnelComparisonGranularity { Month, Year }
public sealed record ImplantFunnelComparisonRow(string Period, int Leads, int MangoBooked, int MedmAppointments, int Attended, decimal Revenue, decimal AverageCheck, decimal Budget, decimal? LeadDeltaPercent, decimal? AppointmentDeltaPercent, decimal? VisitDeltaPercent, decimal? RevenueDeltaPercent, double LeadBarWidth, double MedmBarWidth, double VisitBarWidth);
public sealed record ImplantFunnelDashboard(int LeadCount, int MangoBookedCount, int MangoNotBookedCount, int MangoDroppedCount, int MangoWithoutTagCount, int AppointmentCount, int NotMarkedNoShowCount, int NoShowCount, int CancelledCount, decimal PaymentTotal, decimal AverageCheck, decimal Budget, bool BudgetConfigured, IReadOnlyList<ImplantFunnelStage> Stages, IReadOnlyList<ImplantFunnelBranchRow> Branches, IReadOnlyList<ImplantFunnelMonthlyRow> Dynamics)
{
    public decimal CostPerLead => LeadCount == 0 ? 0m : Budget / LeadCount;
    public decimal CostPerMangoBooking => MangoBookedCount == 0 ? 0m : Budget / MangoBookedCount;
    public decimal CostPerVisit => NotMarkedNoShowCount == 0 ? 0m : Budget / NotMarkedNoShowCount;
    public decimal RevenueToBudgetPercent => Budget == 0 ? 0m : 100m * PaymentTotal / Budget;
    public int MangoBookingWithoutMedm => Math.Max(0, MangoBookedCount - AppointmentCount);
}
public sealed record ImplantFunnelEventImportResult(DateTime From, DateTime To, IReadOnlyList<ImplantFunnelEventImportSourceResult> Sources);
public sealed record ImplantFunnelEventImportSourceResult(int ClinicDataSourceId, int Events, string? Error);
public sealed record ImplantFunnelPatientImportResult(IReadOnlyList<ImplantFunnelPatientImportSourceResult> Sources);
public sealed record ImplantFunnelPatientImportSourceResult(int ClinicDataSourceId, int Cards, string? Error);
