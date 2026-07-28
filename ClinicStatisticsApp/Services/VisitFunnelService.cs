using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Integrations.Firebird;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

public sealed class VisitFunnelService
{
    public async Task<VisitFunnelImportResult> ImportWorkingSourcesAsync(DateTime from, DateTime to, IProgress<string>? progress = null, CancellationToken token = default)
    {
        if (from.Date > to.Date) throw new ArgumentException("Дата начала периода не может быть позже даты окончания.");
        await EnsureStorageAsync(token);
        var configured = FirebirdClinicOptionsLoader.Load().ToDictionary(x => x.ClinicDataSourceId);
        await using var db = DbContextFactory.Create();
        var sources = await db.ClinicDataSources.AsNoTracking()
            .Where(x => !x.IsTest && configured.Keys.Contains(x.Id)).OrderBy(x => x.Name).ToListAsync(token);
        if (sources.Count == 0) throw new InvalidOperationException("Не найдены рабочие источники Firebird. Проверьте firebird.Local.json и настройки источников.");

        var results = new List<VisitFunnelImportSourceResult>();
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            progress?.Report($"Рабочий источник {index + 1} из {sources.Count}: читаем посещения…");
            try
            {
                var visits = await new FirebirdVisitFunnelReader(configured[source.Id]).ReadAsync(from, to, token);
                await ReplacePeriodAsync(source.Id, from, to, visits, token);
                results.Add(new VisitFunnelImportSourceResult(source.Name, visits.Count, null));
            }
            catch (Exception ex) { results.Add(new VisitFunnelImportSourceResult(source.Name, 0, ex.Message)); }
        }

        // The supporting tabs use the already approved CRM analytics snapshot.
        await new CrmAnalyticsWarehouseService().ImportAsync(from, to, sources.Select(x => x.Id).ToArray(), progress, token);
        return new VisitFunnelImportResult(from, to, results);
    }

    public async Task<VisitFunnelDashboard> GetDashboardAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        await EnsureStorageAsync(token);
        await using var db = DbContextFactory.Create();
        var sourceIds = await db.ClinicDataSources.AsNoTracking().Where(x => !x.IsTest).Select(x => x.Id).ToListAsync(token);
        var start = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1).AddMonths(1);
        var entries = await db.CrmVisitFunnelEntries.AsNoTracking()
            .Where(x => sourceIds.Contains(x.ClinicDataSourceId) && x.VisitDate >= start && x.VisitDate < end)
            .Join(db.ClinicDataSources.AsNoTracking(), x => x.ClinicDataSourceId, s => s.Id, (x, s) => new { x, s.Name })
            .ToListAsync(token);

        var monthly = entries.GroupBy(x => new { x.x.ClinicDataSourceId, x.Name, Month = new DateTime(x.x.VisitDate.Year, x.x.VisitDate.Month, 1) })
            .Select(g => new VisitFunnelMonthlyRow(g.Key.ClinicDataSourceId, DisplayName(g.Key.Name), g.Key.Month,
                g.Select(x => x.x.SourcePatientId).Distinct().Count(), g.Count()))
            .OrderBy(x => x.Month).ThenBy(x => x.Branch).ToList();
        var previous = monthly.ToDictionary(x => (x.SourceId, x.Month));
        monthly = monthly.Select(x => x with { PreviousYearPatients = previous.GetValueOrDefault((x.SourceId, x.Month.AddYears(-1)))?.Patients, PreviousYearVisits = previous.GetValueOrDefault((x.SourceId, x.Month.AddYears(-1)))?.Visits }).ToList();
        var yearly = monthly.GroupBy(x => new { x.SourceId, x.Branch, x.Month.Year })
            .Select(g => new VisitFunnelYearlyRow(g.Key.SourceId, g.Key.Branch, g.Key.Year, g.Sum(x => x.Patients), g.Sum(x => x.Visits)))
            .OrderBy(x => x.Year).ThenBy(x => x.Branch).ToList();
        var yearlyPrevious = yearly.ToDictionary(x => (x.SourceId, x.Year));
        yearly = yearly.Select(x => x with { PreviousYearPatients = yearlyPrevious.GetValueOrDefault((x.SourceId, x.Year - 1))?.Patients, PreviousYearVisits = yearlyPrevious.GetValueOrDefault((x.SourceId, x.Year - 1))?.Visits }).ToList();
        return new VisitFunnelDashboard(monthly, monthly.GroupBy(x => x.Month).Select(g => new VisitFunnelTotalsRow(g.Key, g.Sum(x => x.Patients), g.Sum(x => x.Visits))).OrderBy(x => x.Month).ToList(), yearly);
    }

    public async Task<IReadOnlyList<VisitFunnelSpecialistRow>> GetSpecialistsAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        var sourceIds = await db.ClinicDataSources.AsNoTracking().Where(x => !x.IsTest).Select(x => x.Id).ToListAsync(token);
        var end = to.Date.AddDays(1);
        var rows = await db.CrmAnalyticsAppointments.AsNoTracking()
            .Where(x => sourceIds.Contains(x.ClinicDataSourceId) && x.AppointmentDate >= from.Date && x.AppointmentDate < end && !x.IsCancelled)
            .GroupBy(x => new { x.DoctorName, x.ClinicDataSourceId, x.SourcePatientId })
            .Select(g => new { Specialist = g.Key.DoctorName ?? "Не указан", Visits = g.Count(), NoShows = g.Count(x => x.IsNoShow) })
            .ToListAsync(token);
        return rows.GroupBy(x => x.Specialist)
            .Select(g => new VisitFunnelSpecialistRow(g.Key, g.Count(), g.Sum(x => x.Visits), g.Sum(x => x.NoShows)))
            .OrderByDescending(x => x.Visits).ThenBy(x => x.Specialist).ToList();
    }

    public async Task<IReadOnlyList<VisitFunnelCancellationRow>> GetCancellationsAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        var sourceIds = await db.ClinicDataSources.AsNoTracking().Where(x => !x.IsTest).Select(x => x.Id).ToListAsync(token);
        var end = to.Date.AddDays(1);
        var cancellations = await db.CrmAnalyticsAppointments.AsNoTracking()
            .Where(x => sourceIds.Contains(x.ClinicDataSourceId) && x.AppointmentDate >= from.Date && x.AppointmentDate < end && x.IsCancelled)
            .Select(x => new { x.AppointmentDate, x.ClinicDataSourceId, x.SourcePatientId, x.DoctorName, x.AdministratorName })
            .ToListAsync(token);
        if (cancellations.Count == 0) return Array.Empty<VisitFunnelCancellationRow>();

        // Only the latest active appointment for a patient is needed to decide whether a cancellation was followed by a rebooking.
        // This avoids comparing every cancellation with every appointment in the complete warehouse.
        var latestActiveDates = (await db.CrmAnalyticsAppointments.AsNoTracking()
                .Where(x => sourceIds.Contains(x.ClinicDataSourceId) && !x.IsCancelled && x.AppointmentDate > from.Date)
                .Select(x => new { x.ClinicDataSourceId, x.SourcePatientId, x.AppointmentDate })
                .ToListAsync(token))
            .GroupBy(x => (x.ClinicDataSourceId, x.SourcePatientId))
            .ToDictionary(g => g.Key, g => g.Max(x => x.AppointmentDate));

        return cancellations
            .Where(x => !latestActiveDates.TryGetValue((x.ClinicDataSourceId, x.SourcePatientId), out var laterDate) || laterDate <= x.AppointmentDate)
            .Select(x => new VisitFunnelCancellationRow(x.AppointmentDate, x.ClinicDataSourceId, x.SourcePatientId, x.DoctorName ?? "Не указан", x.AdministratorName ?? "Не указан", "Снята и не перезаписана"))
            .OrderByDescending(x => x.Date).ToList();
    }

    public async Task<VisitFunnelChecksResult> GetChecksAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        const int displayLimit = 2500;
        await using var db = DbContextFactory.Create();
        var sourceIds = await db.ClinicDataSources.AsNoTracking().Where(x => !x.IsTest).Select(x => x.Id).ToListAsync(token);
        var end = to.Date.AddDays(1);
        var query = db.CrmAnalyticsAppointments.AsNoTracking()
            .Where(x => sourceIds.Contains(x.ClinicDataSourceId) && x.AppointmentDate >= from.Date && x.AppointmentDate < end);
        var total = await query.CountAsync(token);
        var checks = await query.OrderByDescending(x => x.AppointmentDate).Take(displayLimit)
            .Select(x => new VisitFunnelCheckRow(x.AppointmentDate, x.ClinicDataSourceId, x.SourcePatientId, x.DoctorName ?? "Не указан", x.AdministratorName ?? "Не указан", x.IsCancelled ? "Снята" : x.IsNoShow ? "Неявка" : "Активная запись"))
            .ToListAsync(token);
        return new VisitFunnelChecksResult(checks, total, displayLimit);
    }

    [Obsolete("Используйте отдельные методы загрузки вкладок, чтобы не загружать данные, которые пользователь ещё не открыл.")]
    public async Task<VisitFunnelDetails> GetDetailsAsync(DateTime from, DateTime to, CancellationToken token = default)
    {
        var specialists = await GetSpecialistsAsync(from, to, token);
        var cancellations = await GetCancellationsAsync(from, to, token);
        var checks = await GetChecksAsync(from, to, token);
        return new VisitFunnelDetails(specialists, cancellations, checks.Rows);
    }

    public static IReadOnlyList<VisitFunnelMatrixRow> BuildMatrix(IReadOnlyList<VisitFunnelMonthlyRow> monthly, int year, VisitFunnelMetric metric)
    {
        var rows = monthly.Where(x => x.Month.Year == year).GroupBy(x => new { x.SourceId, x.Branch }).OrderBy(x => x.Key.Branch).Select(group =>
        {
            var current = group.ToDictionary(x => x.Month.Month);
            var previous = monthly.Where(x => x.SourceId == group.Key.SourceId && x.Month.Year == year - 1).ToDictionary(x => x.Month.Month);
            var values = Enumerable.Range(1, 12).Select(month => Format(current.GetValueOrDefault(month), metric)).ToArray();
            var backgrounds = Enumerable.Range(1, 12).Select(month => ComparisonColor(current.GetValueOrDefault(month), previous.GetValueOrDefault(month), metric)).ToArray();
            var total = CalculateTotal(current.Values, metric);
            var previousTotal = previous.Count == 0 ? null : CalculateTotal(previous.Values, metric);
            return new VisitFunnelMatrixRow(group.Key.Branch, values, backgrounds, Format(total, metric), ComparisonColor(total, previousTotal, metric), FormatDelta(total, previousTotal, metric));
        }).ToList();
        return rows;
    }

    public static IReadOnlyList<VisitFunnelTripleMatrixRow> BuildTripleMatrix(IReadOnlyList<VisitFunnelMonthlyRow> monthly, int year) =>
        monthly.Where(x => x.Month.Year == year).GroupBy(x => new { x.SourceId, x.Branch }).OrderBy(x => x.Key.Branch).Select(group =>
        {
            var values = group.ToDictionary(x => x.Month.Month);
            var previous = monthly.Where(x => x.SourceId == group.Key.SourceId && x.Month.Year == year - 1).ToDictionary(x => x.Month.Month);
            var patientBackgrounds = Enumerable.Range(1, 12).Select(m => ComparisonColor(values.GetValueOrDefault(m), previous.GetValueOrDefault(m), VisitFunnelMetric.Patients)).ToArray();
            var visitBackgrounds = Enumerable.Range(1, 12).Select(m => ComparisonColor(values.GetValueOrDefault(m), previous.GetValueOrDefault(m), VisitFunnelMetric.Visits)).ToArray();
            var coefficientBackgrounds = Enumerable.Range(1, 12).Select(m => ComparisonColor(values.GetValueOrDefault(m), previous.GetValueOrDefault(m), VisitFunnelMetric.Coefficient)).ToArray();
            return new VisitFunnelTripleMatrixRow(group.Key.Branch,
                Enumerable.Range(1, 12).Select(m => values.GetValueOrDefault(m)?.Patients.ToString("N0") ?? "—").ToArray(),
                Enumerable.Range(1, 12).Select(m => values.GetValueOrDefault(m)?.Visits.ToString("N0") ?? "—").ToArray(),
                Enumerable.Range(1, 12).Select(m => values.GetValueOrDefault(m)?.Coefficient.ToString("N2") ?? "—").ToArray(),
                patientBackgrounds, visitBackgrounds, coefficientBackgrounds,
                Enumerable.Range(1, 12).Select(m => ComparisonTooltip(values.GetValueOrDefault(m), previous.GetValueOrDefault(m), VisitFunnelMetric.Patients, year - 1)).ToArray(),
                Enumerable.Range(1, 12).Select(m => ComparisonTooltip(values.GetValueOrDefault(m), previous.GetValueOrDefault(m), VisitFunnelMetric.Visits, year - 1)).ToArray(),
                Enumerable.Range(1, 12).Select(m => ComparisonTooltip(values.GetValueOrDefault(m), previous.GetValueOrDefault(m), VisitFunnelMetric.Coefficient, year - 1)).ToArray(),
                group.Sum(x => x.Patients).ToString("N0"), group.Sum(x => x.Visits).ToString("N0"),
                (group.Sum(x => x.Patients) == 0 ? 0m : (decimal)group.Sum(x => x.Visits) / group.Sum(x => x.Patients)).ToString("N2"));
        }).ToList();

    public static decimal MetricValue(VisitFunnelMonthlyRow row, VisitFunnelMetric metric) => metric switch { VisitFunnelMetric.Patients => row.Patients, VisitFunnelMetric.Visits => row.Visits, _ => row.Coefficient };
    private static decimal? CalculateTotal(IEnumerable<VisitFunnelMonthlyRow> rows, VisitFunnelMetric metric)
    {
        var list = rows.ToList();
        if (list.Count == 0) return null;
        return metric == VisitFunnelMetric.Coefficient ? (list.Sum(x => x.Patients) == 0 ? 0m : (decimal)list.Sum(x => x.Visits) / list.Sum(x => x.Patients)) : list.Sum(x => MetricValue(x, metric));
    }
    private static string Format(VisitFunnelMonthlyRow? row, VisitFunnelMetric metric) => row is null ? "—" : Format(MetricValue(row, metric), metric);
    private static string Format(decimal? value, VisitFunnelMetric metric) => value is null ? "—" : value.Value.ToString(metric == VisitFunnelMetric.Coefficient ? "N2" : "N0");
    private static string FormatDelta(decimal? current, decimal? previous, VisitFunnelMetric metric) => current is null || previous is null ? "—" : (current.Value - previous.Value).ToString(metric == VisitFunnelMetric.Coefficient ? "+0.00;-0.00;0.00" : "+0;-0;0");
    private static string ComparisonColor(VisitFunnelMonthlyRow? current, VisitFunnelMonthlyRow? previous, VisitFunnelMetric metric) => current is null || previous is null ? "#FFFFFF" : ComparisonColor(MetricValue(current, metric), MetricValue(previous, metric), metric);
    private static string ComparisonColor(decimal? current, decimal? previous, VisitFunnelMetric metric) => current is null || previous is null ? "#FFFFFF" : current > previous ? "#DCFCE7" : current < previous ? "#FEE2E2" : "#FFF7ED";
    private static string ComparisonTooltip(VisitFunnelMonthlyRow? current, VisitFunnelMonthlyRow? previous, VisitFunnelMetric metric, int previousYear)
    {
        if (current is null) return "Нет данных за выбранный месяц.";
        if (previous is null) return $"Нет данных за {previousYear} год для сравнения.";
        var now = MetricValue(current, metric); var before = MetricValue(previous, metric); var format = metric == VisitFunnelMetric.Coefficient ? "N2" : "N0";
        return $"{previousYear}: {before.ToString(format)}\nРазница: {(now - before).ToString(metric == VisitFunnelMetric.Coefficient ? "+0.00;-0.00;0.00" : "+0;-0;0")}";
    }

    private static async Task ReplacePeriodAsync(int sourceId, DateTime from, DateTime to, IReadOnlyList<FirebirdVisitFunnelRow> rows, CancellationToken token)
    {
        await using var db = DbContextFactory.Create();
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.CrmVisitFunnelEntries.Where(x => x.ClinicDataSourceId == sourceId && x.VisitDate >= from.Date && x.VisitDate < to.Date.AddDays(1)).ExecuteDeleteAsync(token);
        var stamp = DateTime.UtcNow;
        foreach (var batch in rows.Chunk(500))
        {
            db.CrmVisitFunnelEntries.AddRange(batch.Select(x => new CrmVisitFunnelEntry { ClinicDataSourceId = sourceId, SourceVisitId = x.Id, SourcePatientId = x.PatientId, VisitDate = x.Date, SyncedAt = stamp }));
            await db.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    private static string DisplayName(string name) => name.Contains("Москов", StringComparison.OrdinalIgnoreCase) ? "Детство (Моск)" : name.Contains("Сельма", StringComparison.OrdinalIgnoreCase) ? "Детство (Сельма)" : name;

    public async Task EnsureStorageAsync(CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        await db.Database.ExecuteSqlRawAsync("""
IF OBJECT_ID(N'dbo.CrmVisitFunnelEntries', N'U') IS NULL
CREATE TABLE dbo.CrmVisitFunnelEntries (
 Id int IDENTITY(1,1) NOT NULL PRIMARY KEY, ClinicDataSourceId int NOT NULL, SourceVisitId bigint NOT NULL, SourcePatientId bigint NOT NULL,
 VisitDate datetime2 NOT NULL, SyncedAt datetime2 NOT NULL, CONSTRAINT UQ_CrmVisitFunnelEntries UNIQUE(ClinicDataSourceId, SourceVisitId))
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name=N'IX_CrmVisitFunnelEntries_Date') CREATE INDEX IX_CrmVisitFunnelEntries_Date ON dbo.CrmVisitFunnelEntries(VisitDate)
""", token);
    }
}

public sealed record VisitFunnelImportResult(DateTime From, DateTime To, IReadOnlyList<VisitFunnelImportSourceResult> Sources);
public sealed record VisitFunnelImportSourceResult(string Source, int Visits, string? Error);
public sealed record VisitFunnelMonthlyRow(int SourceId, string Branch, DateTime Month, int Patients, int Visits, int? PreviousYearPatients = null, int? PreviousYearVisits = null)
{
    public decimal Coefficient => Patients == 0 ? 0m : (decimal)Visits / Patients;
    public int? VisitsYearOverYear => PreviousYearVisits is null ? null : Visits - PreviousYearVisits;
    public string ComparisonBackground => VisitsYearOverYear is > 0 ? "#DCFCE7" : VisitsYearOverYear is < 0 ? "#FEE2E2" : "#FFFFFF";
}
public sealed record VisitFunnelTotalsRow(DateTime Month, int Patients, int Visits) { public decimal Coefficient => Patients == 0 ? 0m : (decimal)Visits / Patients; }
public sealed record VisitFunnelYearlyRow(int SourceId, string Branch, int Year, int Patients, int Visits, int? PreviousYearPatients = null, int? PreviousYearVisits = null)
{
    public decimal Coefficient => Patients == 0 ? 0m : (decimal)Visits / Patients;
    public int? VisitsYearOverYear => PreviousYearVisits is null ? null : Visits - PreviousYearVisits;
    public string ComparisonBackground => VisitsYearOverYear is > 0 ? "#DCFCE7" : VisitsYearOverYear is < 0 ? "#FEE2E2" : "#FFFFFF";
}
public sealed record VisitFunnelDashboard(IReadOnlyList<VisitFunnelMonthlyRow> Monthly, IReadOnlyList<VisitFunnelTotalsRow> Totals, IReadOnlyList<VisitFunnelYearlyRow> Yearly);
public sealed record VisitFunnelSpecialistRow(string Specialist, int Patients, int Visits, int NoShows);
public sealed record VisitFunnelCancellationRow(DateTime Date, int SourceId, long PatientId, string Doctor, string Administrator, string Status);
public sealed record VisitFunnelCheckRow(DateTime Date, int SourceId, long PatientId, string Doctor, string Administrator, string Status);
public sealed record VisitFunnelChecksResult(IReadOnlyList<VisitFunnelCheckRow> Rows, int Total, int DisplayLimit)
{
    public bool IsTruncated => Total > DisplayLimit;
}
public sealed record VisitFunnelDetails(IReadOnlyList<VisitFunnelSpecialistRow> Specialists, IReadOnlyList<VisitFunnelCancellationRow> Cancellations, IReadOnlyList<VisitFunnelCheckRow> Checks);
public enum VisitFunnelMetric { Patients, Visits, Coefficient }
public sealed record VisitFunnelMatrixRow(string Branch, string[] Values, string[] Backgrounds, string Total, string TotalBackground, string Delta)
{
    public string M01 => Values[0]; public string M02 => Values[1]; public string M03 => Values[2]; public string M04 => Values[3]; public string M05 => Values[4]; public string M06 => Values[5];
    public string M07 => Values[6]; public string M08 => Values[7]; public string M09 => Values[8]; public string M10 => Values[9]; public string M11 => Values[10]; public string M12 => Values[11];
    public string M01Background => Backgrounds[0]; public string M02Background => Backgrounds[1]; public string M03Background => Backgrounds[2]; public string M04Background => Backgrounds[3]; public string M05Background => Backgrounds[4]; public string M06Background => Backgrounds[5];
    public string M07Background => Backgrounds[6]; public string M08Background => Backgrounds[7]; public string M09Background => Backgrounds[8]; public string M10Background => Backgrounds[9]; public string M11Background => Backgrounds[10]; public string M12Background => Backgrounds[11];
}
public sealed record VisitFunnelTripleMatrixRow(string Branch, string[] Patients, string[] Visits, string[] Coefficients, string[] PatientBackgrounds, string[] VisitBackgrounds, string[] CoefficientBackgrounds, string[] PatientTooltips, string[] VisitTooltips, string[] CoefficientTooltips, string TotalPatients, string TotalVisits, string TotalCoefficient)
{
    public string P01 => Patients[0]; public string P02 => Patients[1]; public string P03 => Patients[2]; public string P04 => Patients[3]; public string P05 => Patients[4]; public string P06 => Patients[5]; public string P07 => Patients[6]; public string P08 => Patients[7]; public string P09 => Patients[8]; public string P10 => Patients[9]; public string P11 => Patients[10]; public string P12 => Patients[11];
    public string V01 => Visits[0]; public string V02 => Visits[1]; public string V03 => Visits[2]; public string V04 => Visits[3]; public string V05 => Visits[4]; public string V06 => Visits[5]; public string V07 => Visits[6]; public string V08 => Visits[7]; public string V09 => Visits[8]; public string V10 => Visits[9]; public string V11 => Visits[10]; public string V12 => Visits[11];
    public string C01 => Coefficients[0]; public string C02 => Coefficients[1]; public string C03 => Coefficients[2]; public string C04 => Coefficients[3]; public string C05 => Coefficients[4]; public string C06 => Coefficients[5]; public string C07 => Coefficients[6]; public string C08 => Coefficients[7]; public string C09 => Coefficients[8]; public string C10 => Coefficients[9]; public string C11 => Coefficients[10]; public string C12 => Coefficients[11];
}
