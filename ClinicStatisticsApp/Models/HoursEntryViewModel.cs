namespace ClinicStatisticsApp.Models
{
    public class HoursEntryViewModel
    {
        public int? Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;
        public decimal WorkedHours { get; set; }
    }
}