using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class ProfiReportService
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

        public List<ProfiEntryViewModel> GetProfiEntries(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.ProfiEntries)
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

                return new List<ProfiEntryViewModel>();
            }

            return report.ProfiEntries
                .OrderBy(x => x.Employee!.FullName)
                .Select(x => new ProfiEntryViewModel
                {
                    Id = x.Id,
                    EmployeeId = x.EmployeeId,
                    EmployeeFullName = x.Employee!.FullName,
                    InvitedCount = x.InvitedCount,
                    BookedCount = x.BookedCount,
                    ArrivedCount = x.ArrivedCount
                })
                .ToList();
        }

        public void SaveProfiEntries(int branchId, int year, int month, int userId, List<ProfiEntryViewModel> items)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .Include(r => r.ProfiEntries)
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

            var existingEntries = db.ProfiEntries
                .Where(p => p.BranchReportId == report.Id)
                .ToList();

            db.ProfiEntries.RemoveRange(existingEntries);
            db.SaveChanges();

            foreach (var item in items)
            {
                var entry = new ProfiEntry
                {
                    BranchReportId = report.Id,
                    EmployeeId = item.EmployeeId,
                    InvitedCount = item.InvitedCount,
                    BookedCount = item.BookedCount,
                    ArrivedCount = item.ArrivedCount,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.ProfiEntries.Add(entry);
            }

            report.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }
    }
}