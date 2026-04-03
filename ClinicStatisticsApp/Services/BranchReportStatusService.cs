using ClinicStatisticsApp.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class BranchReportStatusService
    {
        public string GetStatus(int branchId, int year, int month)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .AsNoTracking()
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == year && r.Month == month);

            return report?.Status ?? "Draft";
        }

        public void ClosePeriod(int branchId, int year, int month)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == year && r.Month == month);

            if (report == null)
                throw new Exception("Отчет за этот период не найден.");

            report.Status = "Closed";
            report.UpdatedAt = DateTime.Now;

            db.SaveChanges();
        }

        public void ReopenPeriod(int branchId, int year, int month)
        {
            using var db = DbContextFactory.Create();

            var report = db.BranchReports
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == year && r.Month == month);

            if (report == null)
                throw new Exception("Отчет за этот период не найден.");

            report.Status = "Draft";
            report.UpdatedAt = DateTime.Now;

            db.SaveChanges();
        }

        public bool Exists(int branchId, int year, int month)
        {
            using var db = DbContextFactory.Create();

            return db.BranchReports.Any(r => r.BranchId == branchId && r.Year == year && r.Month == month);
        }
    }
}