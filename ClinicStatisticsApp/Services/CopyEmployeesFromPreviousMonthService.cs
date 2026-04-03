using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class CopyEmployeesFromPreviousMonthService
    {
        public void CopyPerkEmployees(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var (prevYear, prevMonth) = GetPreviousPeriod(year, month);

            var prevReport = db.BranchReports
                .Include(r => r.PerkEntries)
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == prevYear && r.Month == prevMonth);

            if (prevReport == null || !prevReport.PerkEntries.Any())
                throw new Exception("За предыдущий месяц нет сотрудников в блоке ПЕРК.");

            var currentReport = GetOrCreateReport(db, branchId, year, month, userId);

            if (currentReport.Status == "Closed")
                throw new Exception("Текущий период закрыт.");

            var currentEntries = db.PerkEntries.Where(x => x.BranchReportId == currentReport.Id).ToList();
            db.PerkEntries.RemoveRange(currentEntries);
            db.SaveChanges();

            foreach (var prev in prevReport.PerkEntries)
            {
                db.PerkEntries.Add(new PerkEntry
                {
                    BranchReportId = currentReport.Id,
                    EmployeeId = prev.EmployeeId,
                    AttendanceCount = 0,
                    AbsenceCount = 0,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            currentReport.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }

        public void CopyProfiEmployees(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var (prevYear, prevMonth) = GetPreviousPeriod(year, month);

            var prevReport = db.BranchReports
                .Include(r => r.ProfiEntries)
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == prevYear && r.Month == prevMonth);

            if (prevReport == null || !prevReport.ProfiEntries.Any())
                throw new Exception("За предыдущий месяц нет сотрудников в блоке ПРОФЫ.");

            var currentReport = GetOrCreateReport(db, branchId, year, month, userId);

            if (currentReport.Status == "Closed")
                throw new Exception("Текущий период закрыт.");

            var currentEntries = db.ProfiEntries.Where(x => x.BranchReportId == currentReport.Id).ToList();
            db.ProfiEntries.RemoveRange(currentEntries);
            db.SaveChanges();

            foreach (var prev in prevReport.ProfiEntries)
            {
                db.ProfiEntries.Add(new ProfiEntry
                {
                    BranchReportId = currentReport.Id,
                    EmployeeId = prev.EmployeeId,
                    InvitedCount = 0,
                    BookedCount = 0,
                    ArrivedCount = 0,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            currentReport.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }

        public void CopyHoursEmployees(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var (prevYear, prevMonth) = GetPreviousPeriod(year, month);

            var prevReport = db.BranchReports
                .Include(r => r.HoursEntries)
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == prevYear && r.Month == prevMonth);

            if (prevReport == null || !prevReport.HoursEntries.Any())
                throw new Exception("За предыдущий месяц нет сотрудников в блоке ЧАСЫ.");

            var currentReport = GetOrCreateReport(db, branchId, year, month, userId);

            if (currentReport.Status == "Closed")
                throw new Exception("Текущий период закрыт.");

            var currentEntries = db.HoursEntries.Where(x => x.BranchReportId == currentReport.Id).ToList();
            db.HoursEntries.RemoveRange(currentEntries);
            db.SaveChanges();

            foreach (var prev in prevReport.HoursEntries)
            {
                db.HoursEntries.Add(new HoursEntry
                {
                    BranchReportId = currentReport.Id,
                    EmployeeId = prev.EmployeeId,
                    WorkedHours = 0,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            currentReport.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }

        public void CopyReviewEmployees(int branchId, int year, int month, int userId)
        {
            using var db = DbContextFactory.Create();

            var (prevYear, prevMonth) = GetPreviousPeriod(year, month);

            var prevReport = db.BranchReports
                .Include(r => r.ReviewEntries)
                .FirstOrDefault(r => r.BranchId == branchId && r.Year == prevYear && r.Month == prevMonth);

            if (prevReport == null || !prevReport.ReviewEntries.Any())
                throw new Exception("За предыдущий месяц нет сотрудников в блоке ОТЗЫВЫ.");

            var currentReport = GetOrCreateReport(db, branchId, year, month, userId);

            if (currentReport.Status == "Closed")
                throw new Exception("Текущий период закрыт.");

            var currentEntries = db.ReviewEntries.Where(x => x.BranchReportId == currentReport.Id).ToList();
            db.ReviewEntries.RemoveRange(currentEntries);
            db.SaveChanges();

            foreach (var prev in prevReport.ReviewEntries)
            {
                db.ReviewEntries.Add(new ReviewEntry
                {
                    BranchReportId = currentReport.Id,
                    EmployeeId = prev.EmployeeId,
                    SmsSentCount = 0,
                    ReviewsLeftCount = 0,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            currentReport.UpdatedAt = DateTime.Now;
            db.SaveChanges();
        }

        private BranchReport GetOrCreateReport(AppDbContext db, int branchId, int year, int month, int userId)
        {
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

        private (int year, int month) GetPreviousPeriod(int year, int month)
        {
            if (month == 1)
                return (year - 1, 12);

            return (year, month - 1);
        }
    }
}