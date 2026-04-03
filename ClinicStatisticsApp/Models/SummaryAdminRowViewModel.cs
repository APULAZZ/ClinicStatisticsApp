namespace ClinicStatisticsApp.Models
{
    public class SummaryAdminRowViewModel
    {
        public string BranchName { get; set; } = string.Empty;
        public string EmployeeFullName { get; set; } = string.Empty;
        public int AttendanceCount { get; set; }
        public int AbsenceCount { get; set; }
        public decimal Premium { get; set; }
        public bool IsCallCenter { get; set; }
    }
}