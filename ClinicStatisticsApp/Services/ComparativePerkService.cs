using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class ComparativePerkService
    {
        public ComparativePerkResult Build(int mainYear, List<int> otherYears)
        {
            using var db = DbContextFactory.Create();

            var years = new List<int> { mainYear };
            years.AddRange(otherYears.Where(y => y != mainYear));
            years = years.Distinct().OrderByDescending(x => x).ToList();

            var perkEntries = db.PerkEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
                .Include(x => x.Employee)
                .Where(x => years.Contains(x.BranchReport!.Year))
                .ToList();

            var branches = db.Branches
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var result = new ComparativePerkResult
            {
                MainYear = mainYear,
                OtherYears = otherYears.Distinct().Where(y => y != mainYear).OrderByDescending(x => x).ToList()
            };

            // Филиалы
            foreach (var branch in branches)
            {
                var row = BuildBranchRow(branch.Name, perkEntries, branch.Id, mainYear, false, result.OtherYears);
                result.Rows.Add(row);
            }

            // Коллцентр
            var callCenterRow = BuildCallCenterRow(perkEntries, mainYear, result.OtherYears);
            result.Rows.Add(callCenterRow);

            // Итого по филиалам
            var branchTotalRow = BuildBranchTotalRow(branches, perkEntries, mainYear, result.OtherYears);
            result.Rows.Add(branchTotalRow);

            // Итого по коллцентру
            var callCenterTotalRow = BuildCallCenterTotalRow(perkEntries, mainYear, result.OtherYears);
            result.Rows.Add(callCenterTotalRow);

            // Итого по системе
            var systemTotalRow = BuildSystemTotalRow(branches, perkEntries, mainYear, result.OtherYears);
            result.Rows.Add(systemTotalRow);

            return result;
        }

        private ComparativePerkRowViewModel BuildBranchRow(string name, List<PerkEntry> perkEntries, int branchId, int mainYear, bool isCallCenter, List<int> otherYears)
        {
            var main = perkEntries
                .Where(x => x.BranchReport!.BranchId == branchId && x.BranchReport.Year == mainYear && x.Employee!.IsCallCenter == isCallCenter)
                .ToList();

            var row = new ComparativePerkRowViewModel
            {
                Name = name,
                January = main.Where(x => x.BranchReport!.Month == 1).Sum(x => x.AttendanceCount),
                February = main.Where(x => x.BranchReport!.Month == 2).Sum(x => x.AttendanceCount),
                March = main.Where(x => x.BranchReport!.Month == 3).Sum(x => x.AttendanceCount),
                April = main.Where(x => x.BranchReport!.Month == 4).Sum(x => x.AttendanceCount),
                May = main.Where(x => x.BranchReport!.Month == 5).Sum(x => x.AttendanceCount),
                June = main.Where(x => x.BranchReport!.Month == 6).Sum(x => x.AttendanceCount),
                July = main.Where(x => x.BranchReport!.Month == 7).Sum(x => x.AttendanceCount),
                August = main.Where(x => x.BranchReport!.Month == 8).Sum(x => x.AttendanceCount),
                September = main.Where(x => x.BranchReport!.Month == 9).Sum(x => x.AttendanceCount),
                October = main.Where(x => x.BranchReport!.Month == 10).Sum(x => x.AttendanceCount),
                November = main.Where(x => x.BranchReport!.Month == 11).Sum(x => x.AttendanceCount),
                December = main.Where(x => x.BranchReport!.Month == 12).Sum(x => x.AttendanceCount)
            };

            row.MainYearTotal =
                row.January + row.February + row.March + row.April + row.May + row.June +
                row.July + row.August + row.September + row.October + row.November + row.December;

            foreach (var year in otherYears)
            {
                row.OtherYearTotals[year] = perkEntries
                    .Where(x => x.BranchReport!.BranchId == branchId && x.BranchReport.Year == year && x.Employee!.IsCallCenter == isCallCenter)
                    .Sum(x => x.AttendanceCount);
            }

            return row;
        }

        private ComparativePerkRowViewModel BuildCallCenterRow(List<PerkEntry> perkEntries, int mainYear, List<int> otherYears)
        {
            var main = perkEntries
                .Where(x => x.BranchReport!.Year == mainYear && x.Employee!.IsCallCenter)
                .ToList();

            var row = new ComparativePerkRowViewModel
            {
                Name = "Коллцентр",
                January = main.Where(x => x.BranchReport!.Month == 1).Sum(x => x.AttendanceCount),
                February = main.Where(x => x.BranchReport!.Month == 2).Sum(x => x.AttendanceCount),
                March = main.Where(x => x.BranchReport!.Month == 3).Sum(x => x.AttendanceCount),
                April = main.Where(x => x.BranchReport!.Month == 4).Sum(x => x.AttendanceCount),
                May = main.Where(x => x.BranchReport!.Month == 5).Sum(x => x.AttendanceCount),
                June = main.Where(x => x.BranchReport!.Month == 6).Sum(x => x.AttendanceCount),
                July = main.Where(x => x.BranchReport!.Month == 7).Sum(x => x.AttendanceCount),
                August = main.Where(x => x.BranchReport!.Month == 8).Sum(x => x.AttendanceCount),
                September = main.Where(x => x.BranchReport!.Month == 9).Sum(x => x.AttendanceCount),
                October = main.Where(x => x.BranchReport!.Month == 10).Sum(x => x.AttendanceCount),
                November = main.Where(x => x.BranchReport!.Month == 11).Sum(x => x.AttendanceCount),
                December = main.Where(x => x.BranchReport!.Month == 12).Sum(x => x.AttendanceCount)
            };

            row.MainYearTotal =
                row.January + row.February + row.March + row.April + row.May + row.June +
                row.July + row.August + row.September + row.October + row.November + row.December;

            foreach (var year in otherYears)
            {
                row.OtherYearTotals[year] = perkEntries
                    .Where(x => x.BranchReport!.Year == year && x.Employee!.IsCallCenter)
                    .Sum(x => x.AttendanceCount);
            }

            return row;
        }

        private ComparativePerkRowViewModel BuildBranchTotalRow(List<Branch> branches, List<PerkEntry> perkEntries, int mainYear, List<int> otherYears)
        {
            var main = perkEntries
                .Where(x => x.BranchReport!.Year == mainYear && !x.Employee!.IsCallCenter)
                .ToList();

            var row = new ComparativePerkRowViewModel
            {
                Name = $"Итого по филиалам {mainYear}",
                January = main.Where(x => x.BranchReport!.Month == 1).Sum(x => x.AttendanceCount),
                February = main.Where(x => x.BranchReport!.Month == 2).Sum(x => x.AttendanceCount),
                March = main.Where(x => x.BranchReport!.Month == 3).Sum(x => x.AttendanceCount),
                April = main.Where(x => x.BranchReport!.Month == 4).Sum(x => x.AttendanceCount),
                May = main.Where(x => x.BranchReport!.Month == 5).Sum(x => x.AttendanceCount),
                June = main.Where(x => x.BranchReport!.Month == 6).Sum(x => x.AttendanceCount),
                July = main.Where(x => x.BranchReport!.Month == 7).Sum(x => x.AttendanceCount),
                August = main.Where(x => x.BranchReport!.Month == 8).Sum(x => x.AttendanceCount),
                September = main.Where(x => x.BranchReport!.Month == 9).Sum(x => x.AttendanceCount),
                October = main.Where(x => x.BranchReport!.Month == 10).Sum(x => x.AttendanceCount),
                November = main.Where(x => x.BranchReport!.Month == 11).Sum(x => x.AttendanceCount),
                December = main.Where(x => x.BranchReport!.Month == 12).Sum(x => x.AttendanceCount)
            };

            row.MainYearTotal =
                row.January + row.February + row.March + row.April + row.May + row.June +
                row.July + row.August + row.September + row.October + row.November + row.December;

            foreach (var year in otherYears)
            {
                row.OtherYearTotals[year] = perkEntries
                    .Where(x => x.BranchReport!.Year == year && !x.Employee!.IsCallCenter)
                    .Sum(x => x.AttendanceCount);
            }

            return row;
        }

        private ComparativePerkRowViewModel BuildCallCenterTotalRow(List<PerkEntry> perkEntries, int mainYear, List<int> otherYears)
        {
            var row = BuildCallCenterRow(perkEntries, mainYear, otherYears);
            row.Name = $"Итого по Коллцентру {mainYear}";
            return row;
        }

        private ComparativePerkRowViewModel BuildSystemTotalRow(List<Branch> branches, List<PerkEntry> perkEntries, int mainYear, List<int> otherYears)
        {
            var main = perkEntries
                .Where(x => x.BranchReport!.Year == mainYear)
                .ToList();

            var row = new ComparativePerkRowViewModel
            {
                Name = $"Итого по системе клиник {mainYear}",
                January = main.Where(x => x.BranchReport!.Month == 1).Sum(x => x.AttendanceCount),
                February = main.Where(x => x.BranchReport!.Month == 2).Sum(x => x.AttendanceCount),
                March = main.Where(x => x.BranchReport!.Month == 3).Sum(x => x.AttendanceCount),
                April = main.Where(x => x.BranchReport!.Month == 4).Sum(x => x.AttendanceCount),
                May = main.Where(x => x.BranchReport!.Month == 5).Sum(x => x.AttendanceCount),
                June = main.Where(x => x.BranchReport!.Month == 6).Sum(x => x.AttendanceCount),
                July = main.Where(x => x.BranchReport!.Month == 7).Sum(x => x.AttendanceCount),
                August = main.Where(x => x.BranchReport!.Month == 8).Sum(x => x.AttendanceCount),
                September = main.Where(x => x.BranchReport!.Month == 9).Sum(x => x.AttendanceCount),
                October = main.Where(x => x.BranchReport!.Month == 10).Sum(x => x.AttendanceCount),
                November = main.Where(x => x.BranchReport!.Month == 11).Sum(x => x.AttendanceCount),
                December = main.Where(x => x.BranchReport!.Month == 12).Sum(x => x.AttendanceCount)
            };

            row.MainYearTotal =
                row.January + row.February + row.March + row.April + row.May + row.June +
                row.July + row.August + row.September + row.October + row.November + row.December;

            foreach (var year in otherYears)
            {
                row.OtherYearTotals[year] = perkEntries
                    .Where(x => x.BranchReport!.Year == year)
                    .Sum(x => x.AttendanceCount);
            }

            return row;
        }
    }
}