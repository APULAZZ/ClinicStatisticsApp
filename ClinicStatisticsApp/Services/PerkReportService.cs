using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class PerkReportService
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

        public BranchReport GetOrCreateBranchReport(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == year && r.Month == month);

            if (report != null)
                return report;

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

            return report;
        }

        public List<PerkEntryViewModel> GetPerkEntries(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.PerkEntries)
                .ThenInclude(p => p.Employee)
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

                return new List<PerkEntryViewModel>();
            }

            return report.PerkEntries
                .OrderBy(x => x.Employee.FullName)
                .Select(x => new PerkEntryViewModel
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    EmployeeFullName = x.Employee.FullName,
                    AttendanceCount = x.AttendanceCount,
                    AbsenceCount = x.AbsenceCount
                })
                .ToList();
        }

        public void SavePerkEntries(int branchId, int year, int month, int userId, List<PerkEntryViewModel> items)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.PerkEntries)
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

            var existingEntries = db.PerkEntries
                .Where(p => p.BranchReportId == report.Id)
                .ToList();

            db.PerkEntries.RemoveRange(existingEntries);
            db.SaveChanges();

            foreach (var item in items)
            {
                var entry = new PerkEntry
                {
                    BranchReportId = report.Id,
                    EmployeeId = item.EmployeeId,
                    AttendanceCount = item.AttendanceCount,
                    AbsenceCount = item.AbsenceCount,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.PerkEntries.Add(entry);
            }

            report.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }
    }
}