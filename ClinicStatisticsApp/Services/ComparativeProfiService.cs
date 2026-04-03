using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class ComparativeProfiService
    {
        public ComparativePerkResult Build(int mainYear, List<int> otherYears)
        {
            using var db = DbContextFactory.Create();

            var years = new List<int> { mainYear };
            years.AddRange(otherYears.Where(y => y != mainYear));
            years = years.Distinct().OrderByDescending(x => x).ToList();

            var profiEntries = db.ProfiEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
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

            foreach (var branch in branches)
            {
                var row = BuildBranchRow(branch.Name, profiEntries, branch.Id, mainYear, result.OtherYears);
                result.Rows.Add(row);
            }

            var totalRow = BuildTotalRow(profiEntries, mainYear, result.OtherYears);
            result.Rows.Add(totalRow);

            return result;
        }

        private ComparativePerkRowViewModel BuildBranchRow(string name, List<ProfiEntry> profiEntries, int branchId, int mainYear, List<int> otherYears)
        {
            var main = profiEntries
                .Where(x => x.BranchReport!.BranchId == branchId && x.BranchReport.Year == mainYear)
                .ToList();

            var row = new ComparativePerkRowViewModel
            {
                Name = name,
                January = main.Where(x => x.BranchReport!.Month == 1).Sum(x => x.ArrivedCount),
                February = main.Where(x => x.BranchReport!.Month == 2).Sum(x => x.ArrivedCount),
                March = main.Where(x => x.BranchReport!.Month == 3).Sum(x => x.ArrivedCount),
                April = main.Where(x => x.BranchReport!.Month == 4).Sum(x => x.ArrivedCount),
                May = main.Where(x => x.BranchReport!.Month == 5).Sum(x => x.ArrivedCount),
                June = main.Where(x => x.BranchReport!.Month == 6).Sum(x => x.ArrivedCount),
                July = main.Where(x => x.BranchReport!.Month == 7).Sum(x => x.ArrivedCount),
                August = main.Where(x => x.BranchReport!.Month == 8).Sum(x => x.ArrivedCount),
                September = main.Where(x => x.BranchReport!.Month == 9).Sum(x => x.ArrivedCount),
                October = main.Where(x => x.BranchReport!.Month == 10).Sum(x => x.ArrivedCount),
                November = main.Where(x => x.BranchReport!.Month == 11).Sum(x => x.ArrivedCount),
                December = main.Where(x => x.BranchReport!.Month == 12).Sum(x => x.ArrivedCount)
            };

            row.MainYearTotal =
                row.January + row.February + row.March + row.April + row.May + row.June +
                row.July + row.August + row.September + row.October + row.November + row.December;

            foreach (var year in otherYears)
            {
                row.OtherYearTotals[year] = profiEntries
                    .Where(x => x.BranchReport!.BranchId == branchId && x.BranchReport.Year == year)
                    .Sum(x => x.ArrivedCount);
            }

            return row;
        }

        private ComparativePerkRowViewModel BuildTotalRow(List<ProfiEntry> profiEntries, int mainYear, List<int> otherYears)
        {
            var main = profiEntries
                .Where(x => x.BranchReport!.Year == mainYear)
                .ToList();

            var row = new ComparativePerkRowViewModel
            {
                Name = $"Итого по филиалам {mainYear}",
                January = main.Where(x => x.BranchReport!.Month == 1).Sum(x => x.ArrivedCount),
                February = main.Where(x => x.BranchReport!.Month == 2).Sum(x => x.ArrivedCount),
                March = main.Where(x => x.BranchReport!.Month == 3).Sum(x => x.ArrivedCount),
                April = main.Where(x => x.BranchReport!.Month == 4).Sum(x => x.ArrivedCount),
                May = main.Where(x => x.BranchReport!.Month == 5).Sum(x => x.ArrivedCount),
                June = main.Where(x => x.BranchReport!.Month == 6).Sum(x => x.ArrivedCount),
                July = main.Where(x => x.BranchReport!.Month == 7).Sum(x => x.ArrivedCount),
                August = main.Where(x => x.BranchReport!.Month == 8).Sum(x => x.ArrivedCount),
                September = main.Where(x => x.BranchReport!.Month == 9).Sum(x => x.ArrivedCount),
                October = main.Where(x => x.BranchReport!.Month == 10).Sum(x => x.ArrivedCount),
                November = main.Where(x => x.BranchReport!.Month == 11).Sum(x => x.ArrivedCount),
                December = main.Where(x => x.BranchReport!.Month == 12).Sum(x => x.ArrivedCount)
            };

            row.MainYearTotal =
                row.January + row.February + row.March + row.April + row.May + row.June +
                row.July + row.August + row.September + row.October + row.November + row.December;

            foreach (var year in otherYears)
            {
                row.OtherYearTotals[year] = profiEntries
                    .Where(x => x.BranchReport!.Year == year)
                    .Sum(x => x.ArrivedCount);
            }

            return row;
        }
    }
}