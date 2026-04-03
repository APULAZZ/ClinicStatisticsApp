using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class AbsolutePrimaryResult
    {
        public List<AbsolutePrimaryRowViewModel> Rows { get; set; } = new();
        public AbsolutePrimaryRowViewModel Totals { get; set; } = new();
    }
}