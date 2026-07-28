using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;

namespace ClinicStatisticsApp.Services;

/// <summary>Builds a read model for the schedule from the CRM-owned snapshot.</summary>
public sealed class ScheduleService
{
    public async Task<IReadOnlyList<ScheduleDoctorDirectoryItem>> GetDoctorDirectoryAsync(IReadOnlyCollection<int> sourceIds, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        var rows = await (from a in db.CrmAnalyticsAppointments.AsNoTracking()
                      join s in db.ClinicDataSources.AsNoTracking() on a.ClinicDataSourceId equals s.Id
                      join b in db.Branches.AsNoTracking() on s.BranchId equals b.Id
                      where sourceIds.Contains(a.ClinicDataSourceId) && a.SourceDoctorId != null && !string.IsNullOrWhiteSpace(a.DoctorName)
                      select new ScheduleDoctorDirectoryItem(a.ClinicDataSourceId, a.SourceDoctorId!.Value, a.DoctorName!, b.Name))
            .ToListAsync(token);
        return rows.Distinct().OrderBy(x => x.BranchName).ThenBy(x => x.DoctorName).ToList();
    }
    public async Task<IReadOnlyList<ScheduleAppointment>> GetDayAsync(DateTime day, IReadOnlyCollection<int> sourceIds, string? doctor, CancellationToken token = default)
        => await GetRangeAsync(day.Date, day.Date, sourceIds, doctor, token);

    public async Task<IReadOnlyList<ScheduleAppointment>> GetRangeAsync(DateTime dateFrom, DateTime dateTo, IReadOnlyCollection<int> sourceIds, string? doctor, CancellationToken token = default)
    {
        await using var db = DbContextFactory.Create();
        var rangeEnd = dateTo.Date.AddDays(1);
        var profileLinks = string.IsNullOrWhiteSpace(doctor) ? [] : await new ScheduleDoctorProfileService().GetLinksAsync(doctor, token);
        var query = from appointment in db.CrmAnalyticsAppointments.AsNoTracking()
                    join source in db.ClinicDataSources.AsNoTracking() on appointment.ClinicDataSourceId equals source.Id
                    join branch in db.Branches.AsNoTracking() on source.BranchId equals branch.Id
                    where sourceIds.Contains(appointment.ClinicDataSourceId)
                          && !appointment.IsCancelled
                          && appointment.AppointmentDate >= dateFrom.Date
                          && appointment.AppointmentDate < rangeEnd
                    select new { appointment, SourceName = source.Name, BranchName = branch.Name };

        if (!string.IsNullOrWhiteSpace(doctor) && profileLinks.Count == 0)
            query = query.Where(x => x.appointment.DoctorName == doctor);

        var rows = await query.OrderBy(x => x.BranchName).ThenBy(x => x.appointment.DoctorName).ThenBy(x => x.appointment.AppointmentDate)
            .Select(x => new ScheduleAppointment(
                x.appointment.ClinicDataSourceId,
                x.SourceName,
                x.BranchName,
                x.appointment.SourceDoctorId,
                x.appointment.DoctorName ?? "Врач не указан",
                x.appointment.AppointmentDate,
                Math.Max(30, x.appointment.DurationMinutes),
                string.IsNullOrWhiteSpace(x.appointment.PatientName) ? $"Пациент {x.appointment.SourcePatientId}" : x.appointment.PatientName!,
                x.appointment.AppointmentType,
                x.appointment.Room,
                x.appointment.Info,
                x.appointment.IsNoShow,
                x.appointment.SourcePatientId,
                false,
                false,
                false,
                null))
            .ToListAsync(token);

        if (profileLinks.Count > 0)
            rows = rows.Where(x => x.DoctorId.HasValue && profileLinks.Contains((x.SourceId, x.DoctorId.Value))).ToList();

        // In MedM ID_PAC = 10 is a technical placeholder for an open slot. It
        // has no patient card and must stay visually empty in the schedule.
        var scheduleRows = rows.Select(x => x with
        {
            IsOpenSlot = x.SourcePatientId == 10,
            NeedsPatientNameRefresh = x.SourcePatientId != 10 && x.PatientName.StartsWith("Пациент ", StringComparison.Ordinal),
            PatientName = x.SourcePatientId == 350000 || x.PatientName.Contains("резерв", StringComparison.OrdinalIgnoreCase) || (x.Info?.Contains("резерв", StringComparison.OrdinalIgnoreCase) ?? false) ? "РЕЗЕРВ" : x.PatientName
        }).ToList();
        await new ScheduleBlockService().EnsureStorageAsync(token);
        var blocks = await (from block in db.CrmScheduleBlocks.AsNoTracking()
                            join source in db.ClinicDataSources.AsNoTracking() on block.ClinicDataSourceId equals source.Id
                            join branch in db.Branches.AsNoTracking() on source.BranchId equals branch.Id
                            where sourceIds.Contains(block.ClinicDataSourceId) && block.StartsAt < rangeEnd && block.EndsAt > dateFrom.Date
                            select new { block, SourceName = source.Name, BranchName = branch.Name }).ToListAsync(token);
        var names = scheduleRows.Where(x => x.DoctorId.HasValue).GroupBy(x => (x.SourceId, x.DoctorId!.Value)).ToDictionary(x => x.Key, x => x.First().DoctorName);
        scheduleRows.AddRange(blocks.Select(x => new ScheduleAppointment(x.block.ClinicDataSourceId, x.SourceName, x.BranchName, x.block.SourceDoctorId, names.GetValueOrDefault((x.block.ClinicDataSourceId, x.block.SourceDoctorId), "Врач"), x.block.StartsAt, Math.Max(30, (int)(x.block.EndsAt - x.block.StartsAt).TotalMinutes), x.block.Title, x.block.Kind, null, null, false, 0, false, false, true, x.block.Id)));
        return scheduleRows;
    }
}

public sealed record ScheduleDoctorDirectoryItem(int SourceId, long DoctorId, string DoctorName, string BranchName);

public sealed record ScheduleAppointment(int SourceId, string SourceName, string BranchName, long? DoctorId, string DoctorName, DateTime StartsAt, int DurationMinutes, string PatientName, string? AppointmentType, string? Room, string? Info, bool IsNoShow, long SourcePatientId, bool IsOpenSlot, bool NeedsPatientNameRefresh, bool IsServiceBlock, int? ScheduleBlockId)
{
    public string ResourceKey => $"{SourceId}:{DoctorId?.ToString() ?? DoctorName}";
    public string ResourceTitle => $"{DoctorName}\n{BranchName}";
}
