using System;
using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class Employee
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsCallCenter { get; set; }

        public decimal? DefaultReviewPaymentRate { get; set; }

        public decimal? DefaultProfoRate { get; set; }
        public int? DefaultProfoCategoryId { get; set; }
        public ProfoCategory? DefaultProfoCategory { get; set; }

        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<PerkEntry> PerkEntries { get; set; } = new List<PerkEntry>();
        public ICollection<ProfiEntry> ProfiEntries { get; set; } = new List<ProfiEntry>();
        public ICollection<HoursEntry> HoursEntries { get; set; } = new List<HoursEntry>();
        public ICollection<ReviewEntry> ReviewEntries { get; set; } = new List<ReviewEntry>();
        public ICollection<NaradEntry> NaradEntries { get; set; } = new List<NaradEntry>();
        public ICollection<SummaryProfoManualEntry> SummaryProfoManualEntries { get; set; } = new List<SummaryProfoManualEntry>();
    }
}