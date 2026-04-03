using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class SummaryAdminResult
    {
        public List<SummaryAdminRowViewModel> BranchRows { get; set; } = new();
        public List<SummaryAdminRowViewModel> CallCenterRows { get; set; } = new();

        public int BranchAttendanceTotal { get; set; }
        public int BranchAbsenceTotal { get; set; }
        public decimal BranchPremiumTotal { get; set; }

        public int CallCenterAttendanceTotal { get; set; }
        public int CallCenterAbsenceTotal { get; set; }

        public int SystemAttendanceTotal { get; set; }
        public int SystemAbsenceTotal { get; set; }
        public decimal SystemPremiumTotal { get; set; }
    }
}