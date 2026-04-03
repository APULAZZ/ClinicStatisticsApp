using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class DynamicsService
    {
        public DynamicsResult Build(int mainYear, List<int>? comparisonYears = null)
        {
            using var db = DbContextFactory.Create();

            var allNeededYears = new List<int> { mainYear };
            if (comparisonYears != null)
            {
                allNeededYears.AddRange(comparisonYears.Where(y => y != mainYear));
            }

            allNeededYears = allNeededYears.Distinct().OrderByDescending(x => x).ToList();

            var perkEntries = db.PerkEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
                .Include(x => x.Employee)
                .Where(x => allNeededYears.Contains(x.BranchReport!.Year))
                .ToList();

            var branches = db.Branches
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var result = new DynamicsResult();

            // Основной год: филиальные блоки
            foreach (var branch in branches)
            {
                var branchEntries = perkEntries
                    .Where(x =>
                        x.BranchReport!.BranchId == branch.Id &&
                        x.BranchReport.Year == mainYear &&
                        !x.Employee!.IsCallCenter)
                    .ToList();

                var employees = branchEntries
                    .GroupBy(x => new { x.EmployeeId, x.Employee!.FullName })
                    .OrderBy(g => g.Key.FullName)
                    .Select(g => new DynamicsEmployeeRowViewModel
                    {
                        EmployeeFullName = g.Key.FullName,
                        January = g.Where(x => x.BranchReport!.Month == 1).Sum(x => x.AttendanceCount),
                        February = g.Where(x => x.BranchReport!.Month == 2).Sum(x => x.AttendanceCount),
                        March = g.Where(x => x.BranchReport!.Month == 3).Sum(x => x.AttendanceCount),
                        April = g.Where(x => x.BranchReport!.Month == 4).Sum(x => x.AttendanceCount),
                        May = g.Where(x => x.BranchReport!.Month == 5).Sum(x => x.AttendanceCount),
                        June = g.Where(x => x.BranchReport!.Month == 6).Sum(x => x.AttendanceCount),
                        July = g.Where(x => x.BranchReport!.Month == 7).Sum(x => x.AttendanceCount),
                        August = g.Where(x => x.BranchReport!.Month == 8).Sum(x => x.AttendanceCount),
                        September = g.Where(x => x.BranchReport!.Month == 9).Sum(x => x.AttendanceCount),
                        October = g.Where(x => x.BranchReport!.Month == 10).Sum(x => x.AttendanceCount),
                        November = g.Where(x => x.BranchReport!.Month == 11).Sum(x => x.AttendanceCount),
                        December = g.Where(x => x.BranchReport!.Month == 12).Sum(x => x.AttendanceCount)
                    })
                    .ToList();

                result.BranchBlocks.Add(new DynamicsBranchBlockViewModel
                {
                    BranchName = branch.Name,
                    IsCallCenter = false,
                    Employees = employees,
                    TotalJanuary = employees.Sum(x => x.January),
                    TotalFebruary = employees.Sum(x => x.February),
                    TotalMarch = employees.Sum(x => x.March),
                    TotalApril = employees.Sum(x => x.April),
                    TotalMay = employees.Sum(x => x.May),
                    TotalJune = employees.Sum(x => x.June),
                    TotalJuly = employees.Sum(x => x.July),
                    TotalAugust = employees.Sum(x => x.August),
                    TotalSeptember = employees.Sum(x => x.September),
                    TotalOctober = employees.Sum(x => x.October),
                    TotalNovember = employees.Sum(x => x.November),
                    TotalDecember = employees.Sum(x => x.December)
                });
            }

            // Основной год: колл-центр
            var callCenterEntries = perkEntries
                .Where(x => x.BranchReport!.Year == mainYear && x.Employee!.IsCallCenter)
                .ToList();

            var callCenterEmployees = callCenterEntries
                .GroupBy(x => new { x.EmployeeId, x.Employee!.FullName })
                .OrderBy(g => g.Key.FullName)
                .Select(g => new DynamicsEmployeeRowViewModel
                {
                    EmployeeFullName = g.Key.FullName,
                    January = g.Where(x => x.BranchReport!.Month == 1).Sum(x => x.AttendanceCount),
                    February = g.Where(x => x.BranchReport!.Month == 2).Sum(x => x.AttendanceCount),
                    March = g.Where(x => x.BranchReport!.Month == 3).Sum(x => x.AttendanceCount),
                    April = g.Where(x => x.BranchReport!.Month == 4).Sum(x => x.AttendanceCount),
                    May = g.Where(x => x.BranchReport!.Month == 5).Sum(x => x.AttendanceCount),
                    June = g.Where(x => x.BranchReport!.Month == 6).Sum(x => x.AttendanceCount),
                    July = g.Where(x => x.BranchReport!.Month == 7).Sum(x => x.AttendanceCount),
                    August = g.Where(x => x.BranchReport!.Month == 8).Sum(x => x.AttendanceCount),
                    September = g.Where(x => x.BranchReport!.Month == 9).Sum(x => x.AttendanceCount),
                    October = g.Where(x => x.BranchReport!.Month == 10).Sum(x => x.AttendanceCount),
                    November = g.Where(x => x.BranchReport!.Month == 11).Sum(x => x.AttendanceCount),
                    December = g.Where(x => x.BranchReport!.Month == 12).Sum(x => x.AttendanceCount)
                })
                .ToList();

            result.CallCenterBlock = new DynamicsBranchBlockViewModel
            {
                BranchName = "Колл-центр",
                IsCallCenter = true,
                Employees = callCenterEmployees,
                TotalJanuary = callCenterEmployees.Sum(x => x.January),
                TotalFebruary = callCenterEmployees.Sum(x => x.February),
                TotalMarch = callCenterEmployees.Sum(x => x.March),
                TotalApril = callCenterEmployees.Sum(x => x.April),
                TotalMay = callCenterEmployees.Sum(x => x.May),
                TotalJune = callCenterEmployees.Sum(x => x.June),
                TotalJuly = callCenterEmployees.Sum(x => x.July),
                TotalAugust = callCenterEmployees.Sum(x => x.August),
                TotalSeptember = callCenterEmployees.Sum(x => x.September),
                TotalOctober = callCenterEmployees.Sum(x => x.October),
                TotalNovember = callCenterEmployees.Sum(x => x.November),
                TotalDecember = callCenterEmployees.Sum(x => x.December)
            };

            // Сравнение по годам
            foreach (var year in allNeededYears)
            {
                var branchYearEntries = perkEntries
                    .Where(x => x.BranchReport!.Year == year && !x.Employee!.IsCallCenter)
                    .ToList();

                var callCenterYearEntries = perkEntries
                    .Where(x => x.BranchReport!.Year == year && x.Employee!.IsCallCenter)
                    .ToList();

                var branchRow = BuildComparisonRow(year, branchYearEntries);
                var callCenterRow = BuildComparisonRow(year, callCenterYearEntries);

                result.BranchComparisonRows.Add(branchRow);
                result.CallCenterComparisonRows.Add(callCenterRow);

                result.SystemComparisonRows.Add(new DynamicsComparisonRowViewModel
                {
                    Year = year,
                    January = branchRow.January + callCenterRow.January,
                    February = branchRow.February + callCenterRow.February,
                    March = branchRow.March + callCenterRow.March,
                    April = branchRow.April + callCenterRow.April,
                    May = branchRow.May + callCenterRow.May,
                    June = branchRow.June + callCenterRow.June,
                    July = branchRow.July + callCenterRow.July,
                    August = branchRow.August + callCenterRow.August,
                    September = branchRow.September + callCenterRow.September,
                    October = branchRow.October + callCenterRow.October,
                    November = branchRow.November + callCenterRow.November,
                    December = branchRow.December + callCenterRow.December
                });
            }

            return result;
        }

        private DynamicsComparisonRowViewModel BuildComparisonRow(int year, List<PerkEntry> entries)
        {
            return new DynamicsComparisonRowViewModel
            {
                Year = year,
                January = entries.Where(x => x.BranchReport!.Month == 1).Sum(x => x.AttendanceCount),
                February = entries.Where(x => x.BranchReport!.Month == 2).Sum(x => x.AttendanceCount),
                March = entries.Where(x => x.BranchReport!.Month == 3).Sum(x => x.AttendanceCount),
                April = entries.Where(x => x.BranchReport!.Month == 4).Sum(x => x.AttendanceCount),
                May = entries.Where(x => x.BranchReport!.Month == 5).Sum(x => x.AttendanceCount),
                June = entries.Where(x => x.BranchReport!.Month == 6).Sum(x => x.AttendanceCount),
                July = entries.Where(x => x.BranchReport!.Month == 7).Sum(x => x.AttendanceCount),
                August = entries.Where(x => x.BranchReport!.Month == 8).Sum(x => x.AttendanceCount),
                September = entries.Where(x => x.BranchReport!.Month == 9).Sum(x => x.AttendanceCount),
                October = entries.Where(x => x.BranchReport!.Month == 10).Sum(x => x.AttendanceCount),
                November = entries.Where(x => x.BranchReport!.Month == 11).Sum(x => x.AttendanceCount),
                December = entries.Where(x => x.BranchReport!.Month == 12).Sum(x => x.AttendanceCount)
            };
        }
    }
}