using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class ComparativePerkRowViewModel
    {
        public string Name { get; set; } = string.Empty;

        public int January { get; set; }
        public int February { get; set; }
        public int March { get; set; }
        public int April { get; set; }
        public int May { get; set; }
        public int June { get; set; }
        public int July { get; set; }
        public int August { get; set; }
        public int September { get; set; }
        public int October { get; set; }
        public int November { get; set; }
        public int December { get; set; }

        public int MainYearTotal { get; set; }

        public Dictionary<int, int> OtherYearTotals { get; set; } = new();
    }
}