using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class ReviewReportService
    {
        public List<Employee> GetActiveEmployees()
        {
            using var db = DbContextFactory.Create();

            return db.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.FullName)
                .ToList();
        }

        public List<ReviewEntryViewModel> GetReviewEntries(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.ReviewEntries)
                .ThenInclude(h => h.Employee)
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

                return new List<ReviewEntryViewModel>();
            }

            return report.ReviewEntries
                .OrderBy(x => x.Employee!.FullName)
                .Select(x => new ReviewEntryViewModel
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    EmployeeFullName = x.Employee!.FullName,
                    SmsSentCount = x.SmsSentCount,
                    ReviewsLeftCount = x.ReviewsLeftCount
                })
                .ToList();
        }

        public void SaveReviewEntries(int branchId, int year, int month, int userId, List<ReviewEntryViewModel> items)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.ReviewEntries)
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
            }

            if (report.Status == "Closed")
                throw new Exception("Отчет закрыт и недоступен для редактирования.");

            var existingEntries = db.ReviewEntries
                .Where(h => h.BranchReportId == report.Id)
                .ToList();

            db.ReviewEntries.RemoveRange(existingEntries);
            db.SaveChanges();

            foreach (var item in items)
            {
                var entry = new ReviewEntry
                {
                    BranchReportId = report.Id,
                    EmployeeId = item.EmployeeId,
                    SmsSentCount = item.SmsSentCount,
                    ReviewsLeftCount = item.ReviewsLeftCount,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.ReviewEntries.Add(entry);
            }

            report.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }
    }
}