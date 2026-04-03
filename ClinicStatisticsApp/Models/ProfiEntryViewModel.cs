namespace ClinicStatisticsApp.Models
{
    public class ProfiEntryViewModel
    {
        public int? Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;
        public int InvitedCount { get; set; }
        public int BookedCount { get; set; }
        public int ArrivedCount { get; set; }
    }
}