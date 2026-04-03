using System;
using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class BranchReport
    {
        public int Id { get; set; }
        public int BranchId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string Status { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Branch? Branch { get; set; }
        public User? CreatedByUser { get; set; }

        public ICollection<PerkEntry> PerkEntries { get; set; } = new List<PerkEntry>();
        public ICollection<ProfiEntry> ProfiEntries { get; set; } = new List<ProfiEntry>();
        public ICollection<HoursEntry> HoursEntries { get; set; } = new List<HoursEntry>();
        public ICollection<ReviewEntry> ReviewEntries { get; set; } = new List<ReviewEntry>();
        public ICollection<NaradEntry> NaradEntries { get; set; } = new List<NaradEntry>();
    }
}