using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class SummaryGeneralService
    {
        public SummaryGeneralResult Build(int year, int month)
        {
            using var db = DbContextFactory.Create();

            var perkData = db.PerkEntries
                .AsNoTracking()
                .Include(p => p.BranchReport)
                .ThenInclude(r => r.Branch)
                .Include(p => p.Employee)
                .Where(p => p.BranchReport!.Year == year && p.BranchReport.Month == month)
                .ToList();

            var branchRows = BuildRows(perkData.Where(x => !x.Employee!.IsCallCenter).ToList());
            var callCenterRows = BuildRows(perkData.Where(x => x.Employee!.IsCallCenter).ToList());

            var result = new SummaryGeneralResult
            {
                BranchRows = branchRows,
                CallCenterRows = callCenterRows,
                BranchTotals = BuildTotals(branchRows),
                CallCenterTotals = BuildTotals(callCenterRows)
            };

            result.SystemTotals = new SummaryGeneralTotalsViewModel
            {
                AttendanceTotal = result.BranchTotals.AttendanceTotal + result.CallCenterTotals.AttendanceTotal,
                AbsenceTotal = result.BranchTotals.AbsenceTotal + result.CallCenterTotals.AbsenceTotal,
                Ck = result.BranchTotals.Ck + result.CallCenterTotals.Ck,
                Comfort = result.BranchTotals.Comfort + result.CallCenterTotals.Comfort,
                Bagramyana = result.BranchTotals.Bagramyana + result.CallCenterTotals.Bagramyana,
                Detstvo = result.BranchTotals.Detstvo + result.CallCenterTotals.Detstvo,
                Gendelya = result.BranchTotals.Gendelya + result.CallCenterTotals.Gendelya,
                Viktoriya = result.BranchTotals.Viktoriya + result.CallCenterTotals.Viktoriya,
                Alfa = result.BranchTotals.Alfa + result.CallCenterTotals.Alfa,
                Region = result.BranchTotals.Region + result.CallCenterTotals.Region,
                Artilleriyskaya = result.BranchTotals.Artilleriyskaya + result.CallCenterTotals.Artilleriyskaya,
                Selma = result.BranchTotals.Selma + result.CallCenterTotals.Selma
            };

            return result;
        }

        private List<SummaryGeneralRowViewModel> BuildRows(List<PerkEntry> perkData)
        {
            var grouped = perkData
                .GroupBy(x => new { x.EmployeeId, x.Employee!.FullName })
                .OrderBy(g => g.Key.FullName)
                .ToList();

            var result = new List<SummaryGeneralRowViewModel>();
            int number = 1;

            foreach (var group in grouped)
            {
                var row = new SummaryGeneralRowViewModel
                {
                    Number = number++,
                    EmployeeFullName = group.Key.FullName,
                    AttendanceTotal = group.Sum(x => x.AttendanceCount),
                    AbsenceTotal = group.Sum(x => x.AbsenceCount),
                    Ck = group.Where(x => x.BranchReport!.Branch!.Name == "ЦК").Sum(x => x.AttendanceCount),
                    Comfort = group.Where(x => x.BranchReport!.Branch!.Name == "Комфорт").Sum(x => x.AttendanceCount),
                    Bagramyana = group.Where(x => x.BranchReport!.Branch!.Name == "Баграмяна").Sum(x => x.AttendanceCount),
                    Detstvo = group.Where(x => x.BranchReport!.Branch!.Name == "Детство").Sum(x => x.AttendanceCount),
                    Gendelya = group.Where(x => x.BranchReport!.Branch!.Name == "Генделя").Sum(x => x.AttendanceCount),
                    Viktoriya = group.Where(x => x.BranchReport!.Branch!.Name == "Виктория").Sum(x => x.AttendanceCount),
                    Alfa = group.Where(x => x.BranchReport!.Branch!.Name == "Альфа").Sum(x => x.AttendanceCount),
                    Region = group.Where(x => x.BranchReport!.Branch!.Name == "Регион").Sum(x => x.AttendanceCount),
                    Artilleriyskaya = group.Where(x => x.BranchReport!.Branch!.Name == "Артиллерийская").Sum(x => x.AttendanceCount),
                    Selma = group.Where(x => x.BranchReport!.Branch!.Name == "Сельма").Sum(x => x.AttendanceCount)
                };

                result.Add(row);
            }

            return result;
        }

        private SummaryGeneralTotalsViewModel BuildTotals(List<SummaryGeneralRowViewModel> rows)
        {
            return new SummaryGeneralTotalsViewModel
            {
                AttendanceTotal = rows.Sum(x => x.AttendanceTotal),
                AbsenceTotal = rows.Sum(x => x.AbsenceTotal),
                Ck = rows.Sum(x => x.Ck),
                Comfort = rows.Sum(x => x.Comfort),
                Bagramyana = rows.Sum(x => x.Bagramyana),
                Detstvo = rows.Sum(x => x.Detstvo),
                Gendelya = rows.Sum(x => x.Gendelya),
                Viktoriya = rows.Sum(x => x.Viktoriya),
                Alfa = rows.Sum(x => x.Alfa),
                Region = rows.Sum(x => x.Region),
                Artilleriyskaya = rows.Sum(x => x.Artilleriyskaya),
                Selma = rows.Sum(x => x.Selma)
            };
        }
    }
}