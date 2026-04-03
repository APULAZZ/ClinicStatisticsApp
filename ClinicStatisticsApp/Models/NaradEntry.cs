using System;

namespace ClinicStatisticsApp.Models
{
    public class NaradEntry
    {
        public int Id { get; set; }
        public int BranchReportId { get; set; }
        public int EmployeeId { get; set; }
        public bool IsIncluded { get; set; }
        public decimal? PaymentPerReview { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public BranchReport? BranchReport { get; set; }
        public Employee? Employee { get; set; }
    }
}