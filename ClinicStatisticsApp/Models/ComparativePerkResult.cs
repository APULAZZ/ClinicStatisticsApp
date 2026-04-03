using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class ComparativePerkResult
    {
        public int MainYear { get; set; }
        public List<int> OtherYears { get; set; } = new();
        public List<ComparativePerkRowViewModel> Rows { get; set; } = new();
    }
}