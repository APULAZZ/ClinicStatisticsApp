using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class SummaryProDoctorBranchBlockViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;

        public List<SummaryProDoctorEmployeeRowViewModel> Employees { get; set; } = new();

        public int QrJanuary { get; set; }
        public int QrFebruary { get; set; }
        public int QrMarch { get; set; }
        public int QrApril { get; set; }
        public int QrMay { get; set; }
        public int QrJune { get; set; }
        public int QrJuly { get; set; }
        public int QrAugust { get; set; }
        public int QrSeptember { get; set; }
        public int QrOctober { get; set; }
        public int QrNovember { get; set; }
        public int QrDecember { get; set; }

        public int TotalJanuary { get; set; }
        public int TotalFebruary { get; set; }
        public int TotalMarch { get; set; }
        public int TotalApril { get; set; }
        public int TotalMay { get; set; }
        public int TotalJune { get; set; }
        public int TotalJuly { get; set; }
        public int TotalAugust { get; set; }
        public int TotalSeptember { get; set; }
        public int TotalOctober { get; set; }
        public int TotalNovember { get; set; }
        public int TotalDecember { get; set; }
    }
}