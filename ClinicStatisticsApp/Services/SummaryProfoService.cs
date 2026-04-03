using ClinicStatisticsApp.Data;
using ClinicStatisticsApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClinicStatisticsApp.Services
{
    public class SummaryProfoService
    {
        public List<ProfoCategory> GetCategories()
        {
            using var db = DbContextFactory.Create();

            return db.ProfoCategories
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToList();
        }

        public SummaryProfoResult Build(int year, int month)
        {
            using var db = DbContextFactory.Create();

            var profiEntries = db.ProfiEntries
                .AsNoTracking()
                .Include(x => x.BranchReport)
                .ThenInclude(x => x.Branch)
                .Include(x => x.Employee)
                .Where(x => x.BranchReport!.Year == year && x.BranchReport.Month == month)
                .ToList();

            var manualEntries = db.SummaryProfoManualEntries
                .AsNoTracking()
                .Include(x => x.ProfoCategory)
                .Where(x => x.Year == year && x.Month == month)
                .ToList();

            var categories = db.ProfoCategories
                .AsNoTracking()
                .Where(x => x.IsActive)
                .ToList();

            var rows = profiEntries
                .OrderBy(x => x.BranchReport!.Branch!.SortOrder)
                .ThenBy(x => x.Employee!.FullName)
                .Select(x =>
                {
                    var manual = manualEntries.FirstOrDefault(m =>
                        m.BranchId == x.BranchReport!.BranchId &&
                        m.EmployeeId == x.EmployeeId);

                    var premium = CalculatePremium(
                        x.ArrivedCount,
                        manual?.Rate,
                        categories.FirstOrDefault(c => c.Id == manual?.ProfoCategoryId));

                    return new SummaryProfoRowViewModel
                    {
                        BranchId = x.BranchReport!.BranchId,
                        BranchName = x.BranchReport.Branch!.Name,
                        EmployeeId = x.EmployeeId,
                        EmployeeFullName = x.Employee!.FullName,
                        Rate = manual?.Rate,
                        InvitedCount = x.InvitedCount,
                        BookedCount = x.BookedCount,
                        ArrivedCount = x.ArrivedCount,
                        ProfoCategoryId = manual?.ProfoCategoryId,
                        ProfoCategoryName = manual?.ProfoCategory?.Name,
                        Premium = premium
                    };
                })
                .ToList();

            var result = new SummaryProfoResult
            {
                Rows = rows,
                InvitedTotal = rows.Sum(x => x.InvitedCount),
                BookedTotal = rows.Sum(x => x.BookedCount),
                ArrivedTotal = rows.Sum(x => x.ArrivedCount),
                PremiumTotal = rows.Sum(x => x.Premium)
            };

            result.ConversionInvitedToBooked = result.InvitedTotal == 0
                ? 0
                : Math.Round((decimal)result.BookedTotal * 100m / result.InvitedTotal, 1);

            result.ConversionBookedToArrived = result.BookedTotal == 0
                ? 0
                : Math.Round((decimal)result.ArrivedTotal * 100m / result.BookedTotal, 1);

            result.ConversionInvitedToArrived = result.InvitedTotal == 0
                ? 0
                : Math.Round((decimal)result.ArrivedTotal * 100m / result.InvitedTotal, 1);

            return result;
        }

        public void SaveManualValues(int year, int month, List<SummaryProfoRowViewModel> rows)
        {
            using var db = DbContextFactory.Create();

            foreach (var row in rows)
            {
                var existing = db.SummaryProfoManualEntries
                    .FirstOrDefault(x =>
                        x.Year == year &&
                        x.Month == month &&
                        x.BranchId == row.BranchId &&
                        x.EmployeeId == row.EmployeeId);

                if (existing == null)
                {
                    existing = new SummaryProfoManualEntry
                    {
                        Year = year,
                        Month = month,
                        BranchId = row.BranchId,
                        EmployeeId = row.EmployeeId,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    db.SummaryProfoManualEntries.Add(existing);
                }

                existing.Rate = row.Rate;
                existing.ProfoCategoryId = row.ProfoCategoryId;
                existing.UpdatedAt = DateTime.Now;
            }

            db.SaveChanges();
        }

        private decimal CalculatePremium(int arrivedCount, decimal? rate, ProfoCategory? category)
        {
            if (rate == null || category == null)
                return 0;

            if (category.IsNoNorm)
            {
                return arrivedCount * category.BasePaymentPerPatient;
            }

            int norm = 0;

            if (rate == 1m)
                norm = category.NormForRate1 ?? 0;
            else if (rate == 0.5m)
                norm = category.NormForRate05 ?? 0;
            else
                return 0;

            var inNorm = Math.Min(arrivedCount, norm);
            var aboveNorm = Math.Max(arrivedCount - norm, 0);

            return inNorm * category.BasePaymentPerPatient
                   + aboveNorm * category.ExtraPaymentPerPatient;
        }
    }
}