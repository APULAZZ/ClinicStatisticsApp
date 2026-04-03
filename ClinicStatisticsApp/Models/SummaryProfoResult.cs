using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class SummaryProfoResult
    {
        public List<SummaryProfoRowViewModel> Rows { get; set; } = new();

        public int InvitedTotal { get; set; }
        public int BookedTotal { get; set; }
        public int ArrivedTotal { get; set; }
        public decimal PremiumTotal { get; set; }

        public decimal ConversionInvitedToBooked { get; set; }
        public decimal ConversionBookedToArrived { get; set; }
        public decimal ConversionInvitedToArrived { get; set; }
    }
}