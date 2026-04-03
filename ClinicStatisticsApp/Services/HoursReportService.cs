using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class HoursReportService
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

        public List<HoursEntryViewModel> GetHoursEntries(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.HoursEntries)
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

                return new List<HoursEntryViewModel>();
            }

            return report.HoursEntries
                .OrderBy(x => x.Employee!.FullName)
                .Select(x => new HoursEntryViewModel
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    EmployeeFullName = x.Employee!.FullName,
                    WorkedHours = x.WorkedHours
                })
                .ToList();
        }

        public void SaveHoursEntries(int branchId, int year, int month, int userId, List<HoursEntryViewModel> items)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.HoursEntries)
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

            var existingEntries = db.HoursEntries
                .Where(h => h.BranchReportId == report.Id)
                .ToList();

            db.HoursEntries.RemoveRange(existingEntries);
            db.SaveChanges();

            foreach (var item in items)
            {
                var entry = new HoursEntry
                {
                    BranchReportId = report.Id,
                    EmployeeId = item.EmployeeId,
                    WorkedHours = item.WorkedHours,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.HoursEntries.Add(entry);
            }

            report.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }
    }
}