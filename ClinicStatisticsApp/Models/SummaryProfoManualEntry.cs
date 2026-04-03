using System;

namespace ClinicStatisticsApp.Models
{
    public class SummaryProfoManualEntry
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int BranchId { get; set; }
        public int EmployeeId { get; set; }
        public decimal? Rate { get; set; }
        public int? ProfoCategoryId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Branch? Branch { get; set; }
        public Employee? Employee { get; set; }
        public ProfoCategory? ProfoCategory { get; set; }
    }
}