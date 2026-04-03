using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class SummaryProDoctorResult
    {
        public List<SummaryProDoctorBranchBlockViewModel> BranchBlocks { get; set; } = new();

        public int GrandTotalJanuary { get; set; }
        public int GrandTotalFebruary { get; set; }
        public int GrandTotalMarch { get; set; }
        public int GrandTotalApril { get; set; }
        public int GrandTotalMay { get; set; }
        public int GrandTotalJune { get; set; }
        public int GrandTotalJuly { get; set; }
        public int GrandTotalAugust { get; set; }
        public int GrandTotalSeptember { get; set; }
        public int GrandTotalOctober { get; set; }
        public int GrandTotalNovember { get; set; }
        public int GrandTotalDecember { get; set; }
    }
}