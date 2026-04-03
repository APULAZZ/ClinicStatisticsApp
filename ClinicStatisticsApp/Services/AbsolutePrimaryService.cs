using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class AbsolutePrimaryService
    {
        public AbsolutePrimaryResult Build(int year)
        {
            using var db = DbContextFactory.Create();

            var perkEntries = db.PerkEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
                .Include(x => x.Employee)
                .Where(x => x.BranchReport!.Year == year)
                .ToList();

            var branches = db.Branches
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var result = new AbsolutePrimaryResult();

            // Общие показатели колл-центра по месяцам
            var callCenterByMonth = new Dictionary<int, int>();
            for (int month = 1; month <= 12; month++)
            {
                callCenterByMonth[month] = perkEntries
                    .Where(x => x.BranchReport!.Month == month && x.Employee!.IsCallCenter)
                    .Sum(x => x.AttendanceCount);
            }

            foreach (var branch in branches)
            {
                var row = new AbsolutePrimaryRowViewModel
                {
                    BranchName = branch.Name,

                    JanuaryBranch = GetBranchMonth(branch.Id, 1, perkEntries),
                    JanuaryCallCenter = callCenterByMonth[1],

                    FebruaryBranch = GetBranchMonth(branch.Id, 2, perkEntries),
                    FebruaryCallCenter = callCenterByMonth[2],

                    MarchBranch = GetBranchMonth(branch.Id, 3, perkEntries),
                    MarchCallCenter = callCenterByMonth[3],

                    AprilBranch = GetBranchMonth(branch.Id, 4, perkEntries),
                    AprilCallCenter = callCenterByMonth[4],

                    MayBranch = GetBranchMonth(branch.Id, 5, perkEntries),
                    MayCallCenter = callCenterByMonth[5],

                    JuneBranch = GetBranchMonth(branch.Id, 6, perkEntries),
                    JuneCallCenter = callCenterByMonth[6],

                    JulyBranch = GetBranchMonth(branch.Id, 7, perkEntries),
                    JulyCallCenter = callCenterByMonth[7],

                    AugustBranch = GetBranchMonth(branch.Id, 8, perkEntries),
                    AugustCallCenter = callCenterByMonth[8],

                    SeptemberBranch = GetBranchMonth(branch.Id, 9, perkEntries),
                    SeptemberCallCenter = callCenterByMonth[9],

                    OctoberBranch = GetBranchMonth(branch.Id, 10, perkEntries),
                    OctoberCallCenter = callCenterByMonth[10],

                    NovemberBranch = GetBranchMonth(branch.Id, 11, perkEntries),
                    NovemberCallCenter = callCenterByMonth[11],

                    DecemberBranch = GetBranchMonth(branch.Id, 12, perkEntries),
                    DecemberCallCenter = callCenterByMonth[12]
                };

                row.JanuaryTotal = row.JanuaryBranch + row.JanuaryCallCenter;
                row.FebruaryTotal = row.FebruaryBranch + row.FebruaryCallCenter;
                row.MarchTotal = row.MarchBranch + row.MarchCallCenter;
                row.AprilTotal = row.AprilBranch + row.AprilCallCenter;
                row.MayTotal = row.MayBranch + row.MayCallCenter;
                row.JuneTotal = row.JuneBranch + row.JuneCallCenter;
                row.JulyTotal = row.JulyBranch + row.JulyCallCenter;
                row.AugustTotal = row.AugustBranch + row.AugustCallCenter;
                row.SeptemberTotal = row.SeptemberBranch + row.SeptemberCallCenter;
                row.OctoberTotal = row.OctoberBranch + row.OctoberCallCenter;
                row.NovemberTotal = row.NovemberBranch + row.NovemberCallCenter;
                row.DecemberTotal = row.DecemberBranch + row.DecemberCallCenter;

                result.Rows.Add(row);
            }

            result.Totals = new AbsolutePrimaryRowViewModel
            {
                BranchName = "Итого",

                JanuaryBranch = result.Rows.Sum(x => x.JanuaryBranch),
                JanuaryCallCenter = result.Rows.FirstOrDefault()?.JanuaryCallCenter ?? 0,

                FebruaryBranch = result.Rows.Sum(x => x.FebruaryBranch),
                FebruaryCallCenter = result.Rows.FirstOrDefault()?.FebruaryCallCenter ?? 0,

                MarchBranch = result.Rows.Sum(x => x.MarchBranch),
                MarchCallCenter = result.Rows.FirstOrDefault()?.MarchCallCenter ?? 0,

                AprilBranch = result.Rows.Sum(x => x.AprilBranch),
                AprilCallCenter = result.Rows.FirstOrDefault()?.AprilCallCenter ?? 0,

                MayBranch = result.Rows.Sum(x => x.MayBranch),
                MayCallCenter = result.Rows.FirstOrDefault()?.MayCallCenter ?? 0,

                JuneBranch = result.Rows.Sum(x => x.JuneBranch),
                JuneCallCenter = result.Rows.FirstOrDefault()?.JuneCallCenter ?? 0,

                JulyBranch = result.Rows.Sum(x => x.JulyBranch),
                JulyCallCenter = result.Rows.FirstOrDefault()?.JulyCallCenter ?? 0,

                AugustBranch = result.Rows.Sum(x => x.AugustBranch),
                AugustCallCenter = result.Rows.FirstOrDefault()?.AugustCallCenter ?? 0,

                SeptemberBranch = result.Rows.Sum(x => x.SeptemberBranch),
                SeptemberCallCenter = result.Rows.FirstOrDefault()?.SeptemberCallCenter ?? 0,

                OctoberBranch = result.Rows.Sum(x => x.OctoberBranch),
                OctoberCallCenter = result.Rows.FirstOrDefault()?.OctoberCallCenter ?? 0,

                NovemberBranch = result.Rows.Sum(x => x.NovemberBranch),
                NovemberCallCenter = result.Rows.FirstOrDefault()?.NovemberCallCenter ?? 0,

                DecemberBranch = result.Rows.Sum(x => x.DecemberBranch),
                DecemberCallCenter = result.Rows.FirstOrDefault()?.DecemberCallCenter ?? 0
            };

            result.Totals.JanuaryTotal = result.Totals.JanuaryBranch + result.Totals.JanuaryCallCenter;
            result.Totals.FebruaryTotal = result.Totals.FebruaryBranch + result.Totals.FebruaryCallCenter;
            result.Totals.MarchTotal = result.Totals.MarchBranch + result.Totals.MarchCallCenter;
            result.Totals.AprilTotal = result.Totals.AprilBranch + result.Totals.AprilCallCenter;
            result.Totals.MayTotal = result.Totals.MayBranch + result.Totals.MayCallCenter;
            result.Totals.JuneTotal = result.Totals.JuneBranch + result.Totals.JuneCallCenter;
            result.Totals.JulyTotal = result.Totals.JulyBranch + result.Totals.JulyCallCenter;
            result.Totals.AugustTotal = result.Totals.AugustBranch + result.Totals.AugustCallCenter;
            result.Totals.SeptemberTotal = result.Totals.SeptemberBranch + result.Totals.SeptemberCallCenter;
            result.Totals.OctoberTotal = result.Totals.OctoberBranch + result.Totals.OctoberCallCenter;
            result.Totals.NovemberTotal = result.Totals.NovemberBranch + result.Totals.NovemberCallCenter;
            result.Totals.DecemberTotal = result.Totals.DecemberBranch + result.Totals.DecemberCallCenter;

            return result;
        }

        private int GetBranchMonth(int branchId, int month, List<PerkEntry> perkEntries)
        {
            return perkEntries
                .Where(x =>
                    x.BranchReport!.BranchId == branchId &&
                    x.BranchReport.Month == month &&
                    !x.Employee!.IsCallCenter)
                .Sum(x => x.AttendanceCount);
        }
    }
}