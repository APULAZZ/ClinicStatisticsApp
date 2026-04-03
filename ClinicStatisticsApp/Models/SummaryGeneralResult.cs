using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class SummaryGeneralResult
    {
        public List<SummaryGeneralRowViewModel> BranchRows { get; set; } = new();
        public List<SummaryGeneralRowViewModel> CallCenterRows { get; set; } = new();

        public SummaryGeneralTotalsViewModel BranchTotals { get; set; } = new();
        public SummaryGeneralTotalsViewModel CallCenterTotals { get; set; } = new();
        public SummaryGeneralTotalsViewModel SystemTotals { get; set; } = new();
    }
}