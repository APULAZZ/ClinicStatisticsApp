using System;

namespace ClinicStatisticsApp.Models
{
    public class HoursEntry
    {
        public int Id { get; set; }
        public int BranchReportId { get; set; }
        public int EmployeeId { get; set; }
        public decimal WorkedHours { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public BranchReport? BranchReport { get; set; }
        public Employee? Employee { get; set; }
    }
}