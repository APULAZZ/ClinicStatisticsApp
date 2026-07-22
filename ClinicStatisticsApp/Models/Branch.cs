using System;
using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class Branch
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsCallCenterBranch { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<BranchReport> BranchReports { get; set; } = new List<BranchReport>();
        public ICollection<SummaryProfoManualEntry> SummaryProfoManualEntries { get; set; } = new List<SummaryProfoManualEntry>();
        public ICollection<ProDoctorQrEntry> ProDoctorQrEntries { get; set; } = new List<ProDoctorQrEntry>();
        public ICollection<ExternalPatientCard> ExternalPatientCards { get; set; } = new List<ExternalPatientCard>();
    }
}
