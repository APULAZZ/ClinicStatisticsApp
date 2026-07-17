using ClinicStatisticsApp.CallCenter.Models;
using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ClinicStatisticsApp.CallCenter.Services;

public sealed class CallCenterStatisticsService(AppDbContext db)
{
    public async Task<List<CallCenterEmployeeStatRow>> GetEmployeeStatsAsync(
        DateTime from, DateTime to, CallCenterEmployeeStatisticsFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        filter ??= new CallCenterEmployeeStatisticsFilter();
        IQueryable<CallCenterEmployee> employeesQuery = db.CallCenterEmployees.AsNoTracking();

        if (filter.EmployeeIds is { Count: > 0 } && !filter.WithoutEmployees)
        {
            var selectedNames = await db.CallCenterEmployees.AsNoTracking()
                .Where(x => filter.EmployeeIds.Contains(x.Id)).Select(x => x.FullName).Distinct()
                .ToListAsync(cancellationToken);
            employeesQuery = employeesQuery.Where(x => selectedNames.Contains(x.FullName));
        }
        if (filter.WithoutEmployees) employeesQuery = employeesQuery.Where(_ => false);

        if (filter.LimitEmployeesToGroups && filter.GroupIds is { Count: > 0 })
        {
            var selectedGroups = await db.CallCenterGroups.AsNoTracking()
                .Where(x => filter.GroupIds.Contains(x.Id)).Select(x => new { x.Id, x.Name })
                .ToListAsync(cancellationToken);
            var ordinaryGroupIds = selectedGroups.Where(x => !string.Equals(x.Name, "Коллцентр", StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToList();
            var linkedEmployeeIds = await db.CallCenterEmployeeGroups.AsNoTracking()
                .Where(x => ordinaryGroupIds.Contains(x.GroupId)).Select(x => x.EmployeeId).ToListAsync(cancellationToken);
            var historicalEmployeeIds = await db.CallCenterCallRecords.AsNoTracking()
                .Where(x => x.GroupId.HasValue && ordinaryGroupIds.Contains(x.GroupId.Value) && x.EmployeeId.HasValue)
                .Select(x => x.EmployeeId!.Value).Distinct().ToListAsync(cancellationToken);
            var employeeIds = linkedEmployeeIds.Concat(historicalEmployeeIds).Distinct().ToList();
            var callCenterSelected = selectedGroups.Any(x => string.Equals(x.Name, "Коллцентр", StringComparison.OrdinalIgnoreCase));
            employeesQuery = callCenterSelected
                ? employeesQuery.Where(x => employeeIds.Contains(x.Id) || x.FullName.StartsWith("КЦ ") || x.FullName == "Зоя Ершова")
                : employeesQuery.Where(x => employeeIds.Contains(x.Id));
        }

        var employees = await employeesQuery.OrderBy(x => x.FullName).ToListAsync(cancellationToken);
        var employeeIdsByMangoId = await db.CallCenterEmployees.AsNoTracking()
            .Where(x => !string.IsNullOrWhiteSpace(x.MangoUserId))
            .ToDictionaryAsync(x => x.MangoUserId!, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var callsQuery = db.CallCenterCallRecords.AsNoTracking().Where(x => x.CallDateTime >= from && x.CallDateTime <= to);
        if (filter.WithoutGroups) callsQuery = callsQuery.Where(x => x.GroupId == null);
        else if (filter.GroupIds is { Count: > 0 }) callsQuery = callsQuery.Where(x => x.GroupId.HasValue && filter.GroupIds.Contains(x.GroupId.Value));
        if (!filter.IgnoreTopics)
        {
            if (filter.WithoutTopics) callsQuery = callsQuery.Where(x => x.TopicId == null);
            else if (filter.TopicIds is { Count: > 0 }) callsQuery = callsQuery.Where(x => x.TopicId.HasValue && filter.TopicIds.Contains(x.TopicId.Value));
        }

        var callsWithEmployee = (await callsQuery.ToListAsync(cancellationToken))
            .Select(x => new { Call = x, EmployeeId = GetResponsibleEmployeeId(x, employeeIdsByMangoId) });
        if (filter.WithoutEmployees) callsWithEmployee = callsWithEmployee.Where(x => !x.EmployeeId.HasValue);
        else if (filter.EmployeeIds is { Count: > 0 }) callsWithEmployee = callsWithEmployee.Where(x => x.EmployeeId.HasValue && filter.EmployeeIds.Contains(x.EmployeeId.Value));
        var callsByEmployee = callsWithEmployee.Where(x => x.EmployeeId.HasValue).GroupBy(x => x.EmployeeId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Call).ToList());

        var rows = employees.GroupBy(x => x.FullName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => CreateRow(group.First().Id, group.First().FullName, group.SelectMany(x => callsByEmployee.GetValueOrDefault(x.Id) ?? []).ToList()))
            .OrderBy(x => x.EmployeeName).ToList();
        if (filter.WithoutEmployees)
            rows.Add(CreateRow(null, "Не определён", callsWithEmployee.Where(x => !x.EmployeeId.HasValue).Select(x => x.Call).ToList()));
        rows.Add(CreateTotal(rows));
        return rows;
    }

    public async Task<List<CallCenterGroupStatRow>> GetGroupStatsAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
        => await db.CallCenterCallRecords.AsNoTracking().Include(x => x.Group).Where(x => x.CallDateTime >= from && x.CallDateTime <= to)
            .GroupBy(x => new { x.GroupId, GroupName = x.Group != null ? x.Group.Name : "Не определена" })
            .Select(x => new CallCenterGroupStatRow
            {
                GroupId = x.Key.GroupId, GroupName = x.Key.GroupName, IncomingCount = x.Count(y => y.IsIncoming),
                MissedCount = x.Count(y => y.IsMissedIncoming), OutgoingCount = x.Count(y => y.IsOutgoing),
                OutgoingNoAnswerCount = x.Count(y => y.IsOutgoingNoAnswer)
            }).OrderBy(x => x.GroupName).ToListAsync(cancellationToken);

    private static CallCenterEmployeeStatRow CreateRow(int? employeeId, string employeeName, IReadOnlyCollection<CallCenterCallRecord> calls) => new()
    {
        EmployeeId = employeeId, EmployeeName = employeeName,
        IncomingAcceptedCount = calls.Count(x => x.IsIncoming && x.IsAnswered),
        IncomingAcceptedWithoutTransfersCount = calls.Count(x => x.IsIncoming && x.IsAnswered && !IsTransfer(x)),
        OutgoingCount = calls.Count(x => x.IsOutgoing), OutgoingNoAnswerCount = calls.Count(x => x.IsOutgoingNoAnswer),
        InternalOutgoingCount = calls.Count(x => string.Equals(x.Direction, "internal", StringComparison.OrdinalIgnoreCase)),
        TransfersCount = calls.Count(IsTransfer), MissedCount = calls.Count(x => x.IsMissedIncoming),
        TopicsTotalCount = calls.Count(x => x.TopicId.HasValue), TopicsWithoutTransfersCount = calls.Count(x => x.TopicId.HasValue && !IsTransfer(x)),
        TopicsInTransfersCount = calls.Count(x => x.TopicId.HasValue && IsTransfer(x))
    };

    private static CallCenterEmployeeStatRow CreateTotal(IEnumerable<CallCenterEmployeeStatRow> rows) => new()
    {
        EmployeeName = "Всего", IsTotal = true,
        IncomingAcceptedCount = rows.Sum(x => x.IncomingAcceptedCount), IncomingAcceptedWithoutTransfersCount = rows.Sum(x => x.IncomingAcceptedWithoutTransfersCount),
        OutgoingCount = rows.Sum(x => x.OutgoingCount), OutgoingNoAnswerCount = rows.Sum(x => x.OutgoingNoAnswerCount),
        InternalOutgoingCount = rows.Sum(x => x.InternalOutgoingCount), TransfersCount = rows.Sum(x => x.TransfersCount), MissedCount = rows.Sum(x => x.MissedCount),
        TopicsTotalCount = rows.Sum(x => x.TopicsTotalCount), TopicsWithoutTransfersCount = rows.Sum(x => x.TopicsWithoutTransfersCount), TopicsInTransfersCount = rows.Sum(x => x.TopicsInTransfersCount)
    };

    private static int? GetResponsibleEmployeeId(CallCenterCallRecord call, IReadOnlyDictionary<string, int> employeeIdsByMangoId)
    {
        if (!string.Equals(call.Direction, "internal", StringComparison.OrdinalIgnoreCase)) return call.EmployeeId;
        try
        {
            using var document = JsonDocument.Parse(call.RawJson ?? "{}");
            if (!document.RootElement.TryGetProperty("caller_id", out var callerId)) return call.EmployeeId;
            var mangoUserId = callerId.ValueKind switch { JsonValueKind.String => callerId.GetString(), JsonValueKind.Number => callerId.ToString(), _ => null };
            return mangoUserId != null && employeeIdsByMangoId.TryGetValue(mangoUserId, out var employeeId) ? employeeId : call.EmployeeId;
        }
        catch (JsonException) { return call.EmployeeId; }
    }

    private static bool IsTransfer(CallCenterCallRecord call) => call.RawJson?.Contains("\"BlindTransfer\":true", StringComparison.OrdinalIgnoreCase) == true || call.RawJson?.Contains("\"ConsultTransfer\":true", StringComparison.OrdinalIgnoreCase) == true;
}

public sealed class CallCenterEmployeeStatisticsFilter
{
    public IReadOnlyCollection<int>? EmployeeIds { get; init; }
    public IReadOnlyCollection<int>? GroupIds { get; init; }
    public IReadOnlyCollection<int>? TopicIds { get; init; }
    public bool WithoutEmployees { get; init; }
    public bool WithoutGroups { get; init; }
    public bool WithoutTopics { get; init; }
    public bool IgnoreTopics { get; init; }
    public bool LimitEmployeesToGroups { get; init; }
}

public sealed class CallCenterEmployeeStatRow
{
    public int? EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public bool IsTotal { get; init; }
    public int IncomingAcceptedCount { get; init; }
    public int IncomingAcceptedWithoutTransfersCount { get; init; }
    public int OutgoingCount { get; init; }
    public int OutgoingNoAnswerCount { get; init; }
    public int InternalOutgoingCount { get; init; }
    public int TransfersCount { get; init; }
    public int MissedCount { get; init; }
    public int TopicsTotalCount { get; init; }
    public int TopicsWithoutTransfersCount { get; init; }
    public int TopicsInTransfersCount { get; init; }
}

public sealed class CallCenterGroupStatRow
{
    public int? GroupId { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public int IncomingCount { get; init; }
    public int IncomingAcceptedCount { get; init; }
    public int MissedCount { get; init; }
    public int OutgoingCount { get; init; }
    public int OutgoingNoAnswerCount { get; init; }
    public int TransfersCount { get; init; }
}
