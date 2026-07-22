using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public sealed class CrmAnalyticsWarehouseService
{
    public async Task<CrmAnalyticsSummary> GetSummaryAsync(IReadOnlyCollection<int>? sourceIds = null, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        var payments = db.CrmAnalyticsPayments.AsNoTracking().AsQueryable();
        var appointments = db.CrmAnalyticsAppointments.AsNoTracking().AsQueryable();
        if (sourceIds is not null) { payments = payments.Where(x => sourceIds.Contains(x.ClinicDataSourceId)); appointments = appointments.Where(x => sourceIds.Contains(x.ClinicDataSourceId)); }
        var paymentCount = await payments.CountAsync(token);
        var totalPaid = paymentCount == 0 ? 0m : await payments.SumAsync(x => x.Amount, token);
        var appointmentCount = await appointments.CountAsync(token);
        var noShows = await appointments.CountAsync(x => x.IsNoShow, token);
        var patients = await appointments.Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        var doctors = await appointments.Where(x => x.DoctorName != null && x.DoctorName != "").Select(x => x.DoctorName!).Distinct().CountAsync(token);
        var rooms = await appointments.Where(x => x.Room != null && x.Room != "").Select(x => x.Room!).Distinct().CountAsync(token);
        var cutoff = DateTime.Today.AddMonths(-6);
        var inactivePatients = await appointments.GroupBy(x => new { x.ClinicDataSourceId, x.SourcePatientId }).CountAsync(group => group.Max(x => x.AppointmentDate) < cutoff, token);
        var upcomingPatients = await appointments.Where(x => x.AppointmentDate >= DateTime.Today).Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        var attendedPatients = await appointments.Where(x => x.AppointmentDate < DateTime.Today && !x.IsNoShow).Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        var noShowPatients = await appointments.Where(x => x.AppointmentDate < DateTime.Today && x.IsNoShow).Select(x => new { x.ClinicDataSourceId, x.SourcePatientId }).Distinct().CountAsync(token);
        return new CrmAnalyticsSummary(paymentCount, totalPaid, appointmentCount, noShows, patients, doctors, rooms, inactivePatients, upcomingPatients, attendedPatients, noShowPatients);
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
 AppointmentDate datetime2 NOT NULL, DoctorName nvarchar(200) NULL, Room nvarchar(100) NULL, IsNoShow bit NOT NULL, Info nvarchar(2000) NULL, SyncedAt datetime2 NOT NULL,
 CONSTRAINT UQ_CrmAnalyticsAppointments UNIQUE (ClinicDataSourceId, SourceAppointmentId))
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CrmAnalyticsAppointments_AppointmentDate') CREATE INDEX IX_CrmAnalyticsAppointments_AppointmentDate ON dbo.CrmAnalyticsAppointments(AppointmentDate)
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
        foreach (var batch in appointments.Chunk(500)) { db.CrmAnalyticsAppointments.AddRange(batch.Select(x => new CrmAnalyticsAppointment { ClinicDataSourceId = sourceId, SourceAppointmentId = x.Id, SourcePatientId = x.PatientId, AppointmentDate = x.Date, DoctorName = x.Doctor, Room = x.Room, IsNoShow = x.IsNoShow, Info = x.Info, SyncedAt = stamp })); await db.SaveChangesAsync(token); }
        await transaction.CommitAsync(token);
    }
}

public sealed record CrmAnalyticsImportResult(DateTime From, DateTime To, IReadOnlyList<CrmAnalyticsImportSourceResult> Sources);
public sealed record CrmAnalyticsImportSourceResult(int ClinicDataSourceId, int Payments, int Appointments, string? Error);
public sealed record CrmAnalyticsSummary(int PaymentCount, decimal TotalPaid, int AppointmentCount, int NoShowCount, int UniquePatients, int DoctorCount, int RoomCount, int InactivePatientCount, int UpcomingPatientCount, int AttendedPatientCount, int NoShowPatientCount);
