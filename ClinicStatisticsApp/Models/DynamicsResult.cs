using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class DynamicsResult
    {
        public List<DynamicsBranchBlockViewModel> BranchBlocks { get; set; } = new();
        public DynamicsBranchBlockViewModel? CallCenterBlock { get; set; }

        public List<DynamicsComparisonRowViewModel> BranchComparisonRows { get; set; } = new();
        public List<DynamicsComparisonRowViewModel> CallCenterComparisonRows { get; set; } = new();
        public List<DynamicsComparisonRowViewModel> SystemComparisonRows { get; set; } = new();
    }
}