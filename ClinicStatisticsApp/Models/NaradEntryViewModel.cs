namespace ClinicStatisticsApp.Models
{
    public class NaradEntryViewModel
    {
        public int? Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;
        public bool IsIncluded { get; set; }
        public int SmsSentCount { get; set; }
        public int ReviewsLeftCount { get; set; }
        public decimal PaymentPerReview { get; set; }
        public decimal TotalPayment => ReviewsLeftCount * PaymentPerReview;
    }
}