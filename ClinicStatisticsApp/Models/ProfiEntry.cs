using System;

namespace ClinicStatisticsApp.Models
{
    public class ProfiEntry
    {
        public int Id { get; set; }
        public int BranchReportId { get; set; }
        public int EmployeeId { get; set; }
        public int InvitedCount { get; set; }
        public int BookedCount { get; set; }
        public int ArrivedCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public BranchReport? BranchReport { get; set; }
        public Employee? Employee { get; set; }
    }
}