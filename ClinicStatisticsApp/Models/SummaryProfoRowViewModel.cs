namespace ClinicStatisticsApp.Models
{
    public class SummaryProfoRowViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;

        public int EmployeeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;

        public decimal? Rate { get; set; }
        public int InvitedCount { get; set; }
        public int BookedCount { get; set; }
        public int ArrivedCount { get; set; }

        public int? ProfoCategoryId { get; set; }
        public string? ProfoCategoryName { get; set; }

        public decimal Premium { get; set; }
    }
}