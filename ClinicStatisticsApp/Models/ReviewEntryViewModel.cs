namespace ClinicStatisticsApp.Models
{
    public class ReviewEntryViewModel
    {
        public int? Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;
        public int SmsSentCount { get; set; }
        public int ReviewsLeftCount { get; set; }
    }
}