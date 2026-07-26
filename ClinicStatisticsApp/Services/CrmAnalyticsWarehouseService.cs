using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public sealed class CrmAnalyticsWarehouseService
{
    public async Task<CrmPrimaryPatientsAnalytics> GetPrimaryPatientsAsync(DateTime from, DateTime to, IReadOnlyCollection<int>? sourceIds = null, CancellationToken token = default)
    {
        if (from.Date > to.Date) throw new ArgumentException("Дата начала периода не может быть позже даты окончания.");
        await EnsureStorageAsync(token);
        await using var db = DbContextFactory.Create();
        var periodEndExclusive = to.Date.AddDays(1);
        var appointments = db.CrmAnalyticsAppointments.AsNoTracking()
            .Where(x => !x.IsCancelled && x.AppointmentDate >= from.Date && x.AppointmentDate < periodEndExclusive);
        if (sourceIds is not null) appointments = appointments.Where(x => sourceIds.Contains(x.ClinicDataSourceId));

        var allAppointments = await appointments.ToListAsync(token);
        var primary = allAppointments.Where(x => x.AppointmentType is "80" or "60").ToList();
        var primaryKeys = primary.Select(x => (x.ClinicDataSourceId, x.SourcePatientId)).Distinct().ToList();
        var sourceIdSet = primaryKeys.Select(x => x.ClinicDataSourceId).ToHashSet();
        var patientIdSet = primaryKeys.Select(x => x.SourcePatientId).ToHashSet();
        var cards = primaryKeys.Count == 0
            ? []
            : await db.ExternalPatientCards.AsNoTracking()
                .Where(x => x.ClinicDataSourceId.HasValue && sourceIdSet.Contains(x.ClinicDataSourceId.Value) && patientIdSet.Contains(x.SourcePatientId))
                .ToListAsync(token);
        var cardsByKey = cards
            .Where(x => x.ClinicDataSourceId.HasValue)
            .GroupBy(x => (x.ClinicDataSourceId!.Value, x.SourcePatientId))
            .ToDictionary(x => x.Key, x => x.First());
        var reasonNames = await db.CrmDepartureReasonMappings.AsNoTracking()
            .ToDictionaryAsync(x => x.SourceCode, x => x.Name, token);

        var allByPatient = allAppointments.GroupBy(x => (x.ClinicDataSourceId, x.SourcePatientId))
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.AppointmentDate).ToList());
        var rows = primary.GroupBy(x => (x.ClinicDataSourceId, x.SourcePatientId)).Select(group =>
        {
            var key = group.Key;
            var first = group.OrderBy(x => x.AppointmentDate).First();
            cardsByKey.TryGetValue(key, out var card);
            var allVisits = allByPatient[key];
            var fullName = card is null
                ? $"Пациент {key.SourcePatientId}"
                : string.Join(" ", new[] { card.LastName, card.FirstName, card.MiddleName }.Where(x => !string.IsNullOrWhiteSpace(x)));
            return new CrmPrimaryPatientRow(
                key.ClinicDataSourceId,
                key.SourcePatientId,
                fullName,
                card?.SourceCardNumber ?? key.SourcePatientId.ToString(),
                first.AppointmentType == "80" ? "ПерК" : "ПерВ",
                first.DoctorName ?? "Не указан",
                first.AppointmentDate,
                allVisits.Count,
                card?.ClinicDataSource?.Name ?? string.Empty,
                first.AdministratorName ?? "Не указан",
                first.DepartureReasonCode is null ? "Не задана" : reasonNames.GetValueOrDefault(first.DepartureReasonCode.Value, $"Код {first.DepartureReasonCode.Value} (не расшифрован)"));
        }).OrderBy(x => x.FirstAppointmentDate).ToList();

        var byDoctor = rows.GroupBy(x => x.DoctorName).Select(group => new CrmPrimaryDoctorRow(
            group.Key,
            group.Count(),
            group.Count(x => x.VisitCount == 1),
            group.Count(x => x.VisitCount == 2),
            group.Count(x => x.VisitCount >= 3),
            group.Average(x => x.VisitCount),
            group.Count(x => x.PrimaryType == "ПерК"),
            group.Count(x => x.PrimaryType == "ПерВ")))
            .OrderByDescending(x => x.TotalPatients).ThenBy(x => x.DoctorName).ToList();

        // Use the same definition as the funnel: a patient with both appointment
        // types belongs to both indicators, while the detail list shows one row.
        var newClinicPatientCount = primary.Where(x => x.AppointmentType == "80")
            .Select(x => (x.ClinicDataSourceId, x.SourcePatientId)).Distinct().Count();
        var newDoctorPatientCount = primary.Where(x => x.AppointmentType == "60")
            .Select(x => (x.ClinicDataSourceId, x.SourcePatientId)).Distinct().Count();
        return new CrmPrimaryPatientsAnalytics(newClinicPatientCount, newDoctorPatientCount, byDoctor, rows);
    }

    public async Task<CrmAnalyticsSummary> GetSummaryAsync(DateTime from, DateTime to, IReadOnlyCollection<int>? sourceIds = null, CancellationToken token = default)
    {
        if (from.Date > to.Date) throw new ArgumentException("Дата начала периода не может быть позже даты окончания.");
        await using var db = DbContextFactory.Create();
        var periodEndExclusive = to.Date.AddDays(1);
        var payments = db.CrmAnalyticsPayments.AsNoTracking().Where(x => x.PaymentDate >= from.Date && x.PaymentDate < periodEndExclusive);
        var appointments = db.CrmAnalyticsAppointments.AsNoTracking().Where(x => x.AppointmentDate >= from.Date && x.AppointmentDate < periodEndExclusive);
        if (sourceIds is not null) { payments = payments.Where(x => sourceIds.Contains(x.ClinicDataSourceId)); appointments = appointments.Where(x => sourceIds.Contains(x.ClinicDataSourceId)); }
        var paymentCount = await payments.CountAsync(token);
        var totalPaid = paymentCount == 0 ? 0m : await payments.SumAsync(x => x.Amount, token);
        var activeAppointments = appointments.Where(x => !x.IsCancelled);
        var appointmentCount = await activeAppointments.CountAsync(token);
        var cancelledCount = await appointments.CountAsync(x => x.IsCancelled, token);
        var noShows = await activeAppointments.CountAsync(x => x.IsNoShow, token);
        var patients = await activeAppointments.Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        var doctors = await activeAppointments.Where(x => x.DoctorName != null && x.DoctorName != "").Select(x => x.DoctorName!).Distinct().CountAsync(token);
        var rooms = await activeAppointments.Where(x => x.Room != null && x.Room != "").Select(x => x.Room!).Distinct().CountAsync(token);
        var cutoff = DateTime.Today.AddMonths(-6);
        var inactivePatients = await activeAppointments.GroupBy(x => new { x.ClinicDataSourceId, x.SourcePatientId }).CountAsync(group => group.Max(x => x.AppointmentDate) < cutoff, token);
        var upcomingPatients = await activeAppointments.Where(x => x.AppointmentDate >= DateTime.Today).Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        // NOTCOMING is an explicit no-show marker. Its absence does not prove that
        // the visit happened, so the UI calls this value "not marked as no-show".
        var notMarkedNoShowPatients = await activeAppointments.Where(x => x.AppointmentDate < DateTime.Today && !x.IsNoShow).Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        var noShowPatients = await activeAppointments.Where(x => x.AppointmentDate < DateTime.Today && x.IsNoShow).Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        // SETKA.TYP_NAZ is a numeric code: 80 = "ПерК", 60 = "ПерВ".
        var newClinicPatients = await activeAppointments.Where(x => x.AppointmentType == "80").Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        var newDoctorPatients = await activeAppointments.Where(x => x.AppointmentType == "60").Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        var cancelledPatients = await appointments.Where(x => x.IsCancelled).Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        return new CrmAnalyticsSummary(paymentCount, totalPaid, appointmentCount, noShows, cancelledCount, patients, doctors, rooms, inactivePatients, upcomingPatients, notMarkedNoShowPatients, noShowPatients, cancelledPatients, newClinicPatients, newDoctorPatients);
    }
    public async Task EnsureStorageAsync(CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.CrmAnalyticsPayments', N'U') IS NULL
CREATE TABLE dbo.CrmAnalyticsPayments (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ClinicDataSourceId int NOT NULL, SourcePaymentId bigint NOT NULL, SourcePatientId bigint NOT NULL,
 PaymentDate datetime2 NOT NULL, Amount decimal(18,2) NOT NULL, Description nvarchar(1000) NULL, CashDesk nvarchar(100) NULL, SyncedAt datetime2 NOT NULL,
 CONSTRAINT UQ_CrmAnalyticsPayments UNIQUE (ClinicDataSourceId, SourcePaymentId))
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmAnalyticsPayments_PaymentDate') CREATE INDEX IX_CrmAnalyticsPayments_PaymentDate ON dbo.CrmAnalyticsPayments(PaymentDate)
IF OBJECT_ID(N'dbo.CrmAnalyticsAppointments', N'U') IS NULL
CREATE TABLE dbo.CrmAnalyticsAppointments (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ClinicDataSourceId int NOT NULL, SourceAppointmentId bigint NOT NULL, SourcePatientId bigint NOT NULL,
 AppointmentDate datetime2 NOT NULL, DoctorName nvarchar(200) NULL, AdministratorName nvarchar(200) NULL, DepartureReasonCode int NULL, Room nvarchar(100) NULL, IsNoShow bit NOT NULL, IsCancelled bit NOT NULL CONSTRAINT DF_CrmAnalyticsAppointments_IsCancelled DEFAULT(0), Info nvarchar(2000) NULL, SyncedAt datetime2 NOT NULL,
 CONSTRAINT UQ_CrmAnalyticsAppointments UNIQUE (ClinicDataSourceId, SourceAppointmentId))
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmAnalyticsAppointments_AppointmentDate') CREATE INDEX IX_CrmAnalyticsAppointments_AppointmentDate ON dbo.CrmAnalyticsAppointments(AppointmentDate)
IF COL_LENGTH(N'dbo.CrmAnalyticsAppointments', N'AppointmentType') IS NULL ALTER TABLE dbo.CrmAnalyticsAppointments ADD AppointmentType nvarchar(50) NULL
IF COL_LENGTH(N'dbo.CrmAnalyticsAppointments', N'IsCancelled') IS NULL ALTER TABLE dbo.CrmAnalyticsAppointments ADD IsCancelled bit NOT NULL CONSTRAINT DF_CrmAnalyticsAppointments_IsCancelled_Existing DEFAULT(0)
IF COL_LENGTH(N'dbo.CrmAnalyticsAppointments', N'AdministratorName') IS NULL ALTER TABLE dbo.CrmAnalyticsAppointments ADD AdministratorName nvarchar(200) NULL
IF COL_LENGTH(N'dbo.CrmAnalyticsAppointments', N'DepartureReasonCode') IS NULL ALTER TABLE dbo.CrmAnalyticsAppointments ADD DepartureReasonCode int NULL
IF OBJECT_ID(N'dbo.CrmDepartureReasonMappings', N'U') IS NULL
CREATE TABLE dbo.CrmDepartureReasonMappings (Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, SourceCode int NOT NULL, Name nvarchar(200) NOT NULL, IsConfirmed bit NOT NULL, UpdatedAt datetime2 NOT NULL, CONSTRAINT UQ_CrmDepartureReasonMappings_SourceCode UNIQUE(SourceCode))
IF NOT EXISTS (SELECT 1 FROM dbo.CrmDepartureReasonMappings WHERE SourceCode = 0)
INSERT INTO dbo.CrmDepartureReasonMappings (SourceCode, Name, IsConfirmed, UpdatedAt) VALUES (0, N'Не определено', 1, SYSUTCDATETIME())
""", token);
    }

    public async Task<CrmAnalyticsImportResult> ImportAsync(DateTime from, DateTime to, IReadOnlyCollection<int>? sourceIds = null, IProgress<string>? progress = null, CancellationToken token = default)
    {
        if (from.Date > to.Date) throw new InvalidOperationException("Дата начала периода не может быть позже даты окончания.");
        await EnsureStorageAsync(token);
        var sources = FirebirdClinicOptionsLoader.Load().Where(x => sourceIds is null || sourceIds.Contains(x.ClinicDataSourceId)).ToList();
        if (sources.Count == 0) throw new InvalidOperationException("Не найдено настроенных источников Firebird для выбранного режима.");
        var rows = new List<CrmAnalyticsImportSourceResult>();
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            progress?.Report($"Источник {index + 1} из {sources.Count}: получаем оплаты и записи…");
            try
            {
                var data = await new FirebirdAnalyticsReader(source).ReadAsync(from, to, token);
                await ReplaceSourcePeriodAsync(source.ClinicDataSourceId, from, to, data.Payments, data.Appointments, token);
                rows.Add(new(source.ClinicDataSourceId, data.Payments.Count, data.Appointments.Count, null));
                progress?.Report($"Источник {index + 1} из {sources.Count}: готово — оплат {data.Payments.Count}, записей {data.Appointments.Count}.");
            }
            catch (Exception ex) { rows.Add(new(source.ClinicDataSourceId, 0, 0, ex.Message)); progress?.Report($"Источник {index + 1} из {sources.Count}: ошибка, продолжаем со следующим."); }
        }
        return new CrmAnalyticsImportResult(from, to, rows);
    }

    private static async Task ReplaceSourcePeriodAsync(int sourceId, DateTime from, DateTime to, IReadOnlyList<FirebirdPaymentRow> payments, IReadOnlyList<FirebirdAppointmentRow> appointments, CancellationToken token)
    {
        await using var db = DbContextFactory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.CrmAnalyticsPayments.Where(x => x.ClinicDataSourceId == sourceId && x.PaymentDate >= from.Date && x.PaymentDate < to.Date.AddDays(1)).ExecuteDeleteAsync(token);
        await db.CrmAnalyticsAppointments.Where(x => x.ClinicDataSourceId == sourceId && x.AppointmentDate >= from.Date && x.AppointmentDate < to.Date.AddDays(1)).ExecuteDeleteAsync(token);
        var stamp = DateTime.UtcNow;
        foreach (var batch in payments.Chunk(500)) { db.CrmAnalyticsPayments.AddRange(batch.Select(x => new CrmAnalyticsPayment { ClinicDataSourceId = sourceId, SourcePaymentId = x.Id, SourcePatientId = x.PatientId, PaymentDate = x.Date, Amount = x.Amount, Description = x.Description, CashDesk = x.CashDesk, SyncedAt = stamp })); await db.SaveChangesAsync(token); }
        foreach (var batch in appointments.Chunk(500)) { db.CrmAnalyticsAppointments.AddRange(batch.Select(x => new CrmAnalyticsAppointment { ClinicDataSourceId = sourceId, SourceAppointmentId = x.Id, SourcePatientId = x.PatientId, AppointmentDate = x.Date, DoctorName = x.Doctor, AdministratorName = x.Administrator, DepartureReasonCode = x.DepartureReasonCode, AppointmentType = x.AppointmentType, Room = x.Room, IsNoShow = x.IsNoShow, IsCancelled = x.IsCancelled, Info = x.Info, SyncedAt = stamp })); await db.SaveChangesAsync(token); }
        await transaction.CommitAsync(token);
    }
}

public sealed record CrmAnalyticsImportResult(DateTime From, DateTime To, IReadOnlyList<CrmAnalyticsImportSourceResult> Sources);
public sealed record CrmAnalyticsImportSourceResult(int ClinicDataSourceId, int Payments, int Appointments, string? Error);
public sealed record CrmAnalyticsSummary(int PaymentCount, decimal TotalPaid, int AppointmentCount, int NoShowCount, int CancelledAppointmentCount, int UniquePatients, int DoctorCount, int RoomCount, int InactivePatientCount, int UpcomingPatientCount, int AttendedPatientCount, int NoShowPatientCount, int CancelledPatientCount, int NewClinicPatientCount, int NewDoctorPatientCount);
public sealed record CrmPrimaryPatientsAnalytics(int NewClinicPatientCount, int NewDoctorPatientCount, IReadOnlyList<CrmPrimaryDoctorRow> Doctors, IReadOnlyList<CrmPrimaryPatientRow> Patients);
public sealed record CrmPrimaryDoctorRow(string DoctorName, int TotalPatients, int OneVisitPatients, int TwoVisitPatients, int ThreeOrMoreVisitPatients, double AverageVisits, int NewClinicPatients, int NewDoctorPatients);
public sealed record CrmPrimaryPatientRow(int ClinicDataSourceId, long SourcePatientId, string PatientName, string CardNumber, string PrimaryType, string DoctorName, DateTime FirstAppointmentDate, int VisitCount, string SourceName, string AdministratorName, string DepartureReasonName);
