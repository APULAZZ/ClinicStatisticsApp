using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class SummaryProDoctorService
    {
        public SummaryProDoctorResult Build(int year)
        {
            using var db = DbContextFactory.Create();

            var reviewEntries = db.ReviewEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
                .Include(x => x.Employee)
                .Where(x => x.BranchReport!.Year == year)
                .ToList();

            var qrEntries = db.ProDoctorQrEntries
                .AsNoTracking()
                .Where(x => x.Year == year)
                .ToList();

            var branches = db.Branches
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToList();

            var result = new SummaryProDoctorResult();

            foreach (var branch in branches)
            {
                var branchEntries = reviewEntries
                    .Where(x => x.BranchReport!.BranchId == branch.Id)
                    .ToList();

                var employees = branchEntries
                    .GroupBy(x => new { x.EmployeeId, x.Employee!.FullName })
                    .OrderBy(g => g.Key.FullName)
                    .Select(g => new SummaryProDoctorEmployeeRowViewModel
                    {
                        EmployeeFullName = g.Key.FullName,
                        January = g.Where(x => x.BranchReport!.Month == 1).Sum(x => x.ReviewsLeftCount),
                        February = g.Where(x => x.BranchReport!.Month == 2).Sum(x => x.ReviewsLeftCount),
                        March = g.Where(x => x.BranchReport!.Month == 3).Sum(x => x.ReviewsLeftCount),
                        April = g.Where(x => x.BranchReport!.Month == 4).Sum(x => x.ReviewsLeftCount),
                        May = g.Where(x => x.BranchReport!.Month == 5).Sum(x => x.ReviewsLeftCount),
                        June = g.Where(x => x.BranchReport!.Month == 6).Sum(x => x.ReviewsLeftCount),
                        July = g.Where(x => x.BranchReport!.Month == 7).Sum(x => x.ReviewsLeftCount),
                        August = g.Where(x => x.BranchReport!.Month == 8).Sum(x => x.ReviewsLeftCount),
                        September = g.Where(x => x.BranchReport!.Month == 9).Sum(x => x.ReviewsLeftCount),
                        October = g.Where(x => x.BranchReport!.Month == 10).Sum(x => x.ReviewsLeftCount),
                        November = g.Where(x => x.BranchReport!.Month == 11).Sum(x => x.ReviewsLeftCount),
                        December = g.Where(x => x.BranchReport!.Month == 12).Sum(x => x.ReviewsLeftCount)
                    })
                    .ToList();

                var block = new SummaryProDoctorBranchBlockViewModel
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    Employees = employees,

                    QrJanuary = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 1).Sum(x => x.QrCount),
                    QrFebruary = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 2).Sum(x => x.QrCount),
                    QrMarch = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 3).Sum(x => x.QrCount),
                    QrApril = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 4).Sum(x => x.QrCount),
                    QrMay = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 5).Sum(x => x.QrCount),
                    QrJune = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 6).Sum(x => x.QrCount),
                    QrJuly = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 7).Sum(x => x.QrCount),
                    QrAugust = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 8).Sum(x => x.QrCount),
                    QrSeptember = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 9).Sum(x => x.QrCount),
                    QrOctober = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 10).Sum(x => x.QrCount),
                    QrNovember = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 11).Sum(x => x.QrCount),
                    QrDecember = qrEntries.Where(x => x.BranchId == branch.Id && x.Month == 12).Sum(x => x.QrCount)
                };

                block.TotalJanuary = employees.Sum(x => x.January) + block.QrJanuary;
                block.TotalFebruary = employees.Sum(x => x.February) + block.QrFebruary;
                block.TotalMarch = employees.Sum(x => x.March) + block.QrMarch;
                block.TotalApril = employees.Sum(x => x.April) + block.QrApril;
                block.TotalMay = employees.Sum(x => x.May) + block.QrMay;
                block.TotalJune = employees.Sum(x => x.June) + block.QrJune;
                block.TotalJuly = employees.Sum(x => x.July) + block.QrJuly;
                block.TotalAugust = employees.Sum(x => x.August) + block.QrAugust;
                block.TotalSeptember = employees.Sum(x => x.September) + block.QrSeptember;
                block.TotalOctober = employees.Sum(x => x.October) + block.QrOctober;
                block.TotalNovember = employees.Sum(x => x.November) + block.QrNovember;
                block.TotalDecember = employees.Sum(x => x.December) + block.QrDecember;

                result.BranchBlocks.Add(block);
            }

            result.GrandTotalJanuary = result.BranchBlocks.Sum(x => x.TotalJanuary);
            result.GrandTotalFebruary = result.BranchBlocks.Sum(x => x.TotalFebruary);
            result.GrandTotalMarch = result.BranchBlocks.Sum(x => x.TotalMarch);
            result.GrandTotalApril = result.BranchBlocks.Sum(x => x.TotalApril);
            result.GrandTotalMay = result.BranchBlocks.Sum(x => x.TotalMay);
            result.GrandTotalJune = result.BranchBlocks.Sum(x => x.TotalJune);
            result.GrandTotalJuly = result.BranchBlocks.Sum(x => x.TotalJuly);
            result.GrandTotalAugust = result.BranchBlocks.Sum(x => x.TotalAugust);
            result.GrandTotalSeptember = result.BranchBlocks.Sum(x => x.TotalSeptember);
            result.GrandTotalOctober = result.BranchBlocks.Sum(x => x.TotalOctober);
            result.GrandTotalNovember = result.BranchBlocks.Sum(x => x.TotalNovember);
            result.GrandTotalDecember = result.BranchBlocks.Sum(x => x.TotalDecember);

            return result;
        }

        public void SaveQrValues(int year, List<SummaryProDoctorBranchBlockViewModel> blocks)
        {
            using var db = DbContextFactory.Create();

            foreach (var block in blocks)
            {
                SaveQr(db, year, block.BranchId, 1, block.QrJanuary);
                SaveQr(db, year, block.BranchId, 2, block.QrFebruary);
                SaveQr(db, year, block.BranchId, 3, block.QrMarch);
                SaveQr(db, year, block.BranchId, 4, block.QrApril);
                SaveQr(db, year, block.BranchId, 5, block.QrMay);
                SaveQr(db, year, block.BranchId, 6, block.QrJune);
                SaveQr(db, year, block.BranchId, 7, block.QrJuly);
                SaveQr(db, year, block.BranchId, 8, block.QrAugust);
                SaveQr(db, year, block.BranchId, 9, block.QrSeptember);
                SaveQr(db, year, block.BranchId, 10, block.QrOctober);
                SaveQr(db, year, block.BranchId, 11, block.QrNovember);
                SaveQr(db, year, block.BranchId, 12, block.QrDecember);
            }

            db.SaveChanges();
        }

        private void SaveQr(AppDbContext db, int year, int branchId, int month, int value)
        {
            var existing = db.ProDoctorQrEntries
                .FirstOrDefault(x => x.Year == year && x.BranchId == branchId && x.Month == month);

            if (existing == null)
            {
                existing = new ProDoctorQrEntry
                {
                    Year = year,
                    BranchId = branchId,
                    Month = month,
                    QrCount = value,
                    CreatedAt = System.DateTime.Now,
                    UpdatedAt = System.DateTime.Now
                };

                db.ProDoctorQrEntries.Add(existing);
            }
            else
            {
                existing.QrCount = value;
                existing.UpdatedAt = System.DateTime.Now;
            }
        }
    }
}