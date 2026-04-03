namespace ClinicStatisticsApp.Models
{
    public class PerkEntryViewModel
    {
        public int? Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;
        public int AttendanceCount { get; set; }
        public int AbsenceCount { get; set; }
        public int Total => AttendanceCount + AbsenceCount;
    }
}