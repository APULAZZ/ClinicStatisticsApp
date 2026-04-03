using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class NaradService
    {
        public List<NaradEntryViewModel> GetNaradEntries(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.ReviewEntries)
                .ThenInclude(x => x.Employee)
                .Include(r => r.NaradEntries)
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == year && r.Month == month);

            if (report == null)
            {
                report = new BranchReport
                {
                    BranchId = branchId,
                    Year = year,
                    Month = month,
                    Status = "Draft",
                    CreatedByUserId = userId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.BranchReports.Add(report);
                db.SaveChanges();
                return new List<NaradEntryViewModel>();
            }

            if (!report.ReviewEntries.Any())
                return new List<NaradEntryViewModel>();

            foreach (var review in report.ReviewEntries)
            {
                var existingNarad = report.NaradEntries.FirstOrDefault(n => n.EmployeeId == review.EmployeeId);
                if (existingNarad == null)
                {
                    var lastRate = db.NaradEntries
                        .Where(n => n.EmployeeId == review.EmployeeId && n.PaymentPerReview != null)
                        .OrderByDescending(n => n.Id)
                        .Select(n => n.PaymentPerReview)
                        .FirstOrDefault();

                    var naradEntry = new NaradEntry
                    {
                        BranchReportId = report.Id,
                        EmployeeId = review.EmployeeId,
                        IsIncluded = true,
                        PaymentPerReview = lastRate ?? review.Employee?.DefaultReviewPaymentRate ?? 0,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    db.NaradEntries.Add(naradEntry);
                }
            }

            db.SaveChanges();

            report = db.BranchReports
                .Include(r => r.ReviewEntries)
                .ThenInclude(x => x.Employee)
                .Include(r => r.NaradEntries)
                .ThenInclude(x => x.Employee)
                .First(r => r.Id == report.Id);

            var result = report.ReviewEntries
                .OrderBy(x => x.Employee!.FullName)
                .Select(review =>
                {
                    var narad = report.NaradEntries.First(n => n.EmployeeId == review.EmployeeId);

                    return new NaradEntryViewModel
                    {
                        Id = narad.Id,
                        EmployeeId = review.EmployeeId,
                        EmployeeFullName = review.Employee!.FullName,
                        IsIncluded = narad.IsIncluded,
                        SmsSentCount = review.SmsSentCount,
                        ReviewsLeftCount = review.ReviewsLeftCount,
                        PaymentPerReview = narad.PaymentPerReview ?? 0
                    };
                })
                .ToList();

            return result;
        }

        public void SaveNaradEntries(int branchId, int year, int month, List<NaradEntryViewModel> items)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.NaradEntries)
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == year && r.Month == month);

            if (report == null)
                throw new Exception("Отчет филиала не найден.");

            if (report.Status == "Closed")
                throw new Exception("Отчет закрыт и недоступен для редактирования.");

            foreach (var item in items)
            {
                var narad = report.NaradEntries.FirstOrDefault(n => n.EmployeeId == item.EmployeeId);
                if (narad == null)
                    continue;

                narad.IsIncluded = item.IsIncluded;
                narad.PaymentPerReview = item.PaymentPerReview;
                narad.UpdatedAt = DateTime.Now;
            }

            report.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }
    }
}