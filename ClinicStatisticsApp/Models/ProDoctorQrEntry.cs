using System;

namespace ClinicStatisticsApp.Models
{
    public class ProDoctorQrEntry
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int BranchId { get; set; }
        public int QrCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public Branch? Branch { get; set; }
    }
}