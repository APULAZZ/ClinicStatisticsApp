using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class SummaryAdminService
    {
        public SummaryAdminResult Build(int year, int month)
        {
            using var db = DbContextFactory.Create();

            var profiEntries = db.ProfiEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
                .Include(x => x.Employee)
                .Where(x => x.BranchReport!.Year == year && x.BranchReport.Month == month)
                .ToList();

            var perkEntries = db.PerkEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
                .Include(x => x.Employee)
                .Where(x => x.BranchReport!.Year == year && x.BranchReport.Month == month)
                .ToList();

            var branchRows = new List<SummaryAdminRowViewModel>();
            var callCenterRows = new List<SummaryAdminRowViewModel>();

            foreach (var profi in profiEntries.OrderBy(x => x.BranchReport!.Branch!.SortOrder).ThenBy(x => x.Employee!.FullName))
            {
                var matchingPerk = perkEntries.FirstOrDefault(p =>
                    p.BranchReport!.BranchId == profi.BranchReport!.BranchId &&
                    p.EmployeeId == profi.EmployeeId);

                var attendance = matchingPerk?.AttendanceCount ?? 0;
                var absence = matchingPerk?.AbsenceCount ?? 0;

                var row = new SummaryAdminRowViewModel
                {
                    BranchName = profi.BranchReport!.Branch!.Name,
                    EmployeeFullName = profi.Employee!.FullName,
                    AttendanceCount = attendance,
                    AbsenceCount = absence,
                    Premium = profi.Employee.IsCallCenter ? 0 : attendance * 40,
                    IsCallCenter = profi.Employee.IsCallCenter
                };

                if (profi.Employee.IsCallCenter)
                    callCenterRows.Add(row);
                else
                    branchRows.Add(row);
            }

            var result = new SummaryAdminResult
            {
                BranchRows = branchRows,
                CallCenterRows = callCenterRows,

                BranchAttendanceTotal = branchRows.Sum(x => x.AttendanceCount),
                BranchAbsenceTotal = branchRows.Sum(x => x.AbsenceCount),
                BranchPremiumTotal = branchRows.Sum(x => x.Premium),

                CallCenterAttendanceTotal = callCenterRows.Sum(x => x.AttendanceCount),
                CallCenterAbsenceTotal = callCenterRows.Sum(x => x.AbsenceCount)
            };

            result.SystemAttendanceTotal = result.BranchAttendanceTotal + result.CallCenterAttendanceTotal;
            result.SystemAbsenceTotal = result.BranchAbsenceTotal + result.CallCenterAbsenceTotal;
            result.SystemPremiumTotal = result.BranchPremiumTotal;

            return result;
        }
    }
}