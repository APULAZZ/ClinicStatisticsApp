using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class DynamicsBranchBlockViewModel
    {
        public string BranchName { get; set; } = string.Empty;
        public bool IsCallCenter { get; set; }

        public List<DynamicsEmployeeRowViewModel> Employees { get; set; } = new();

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