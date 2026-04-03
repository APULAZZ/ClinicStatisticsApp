using System.Collections.Generic;

namespace ClinicStatisticsApp.Models
{
    public class ProfoCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal SalaryRub { get; set; }
        public int? NormForRate1 { get; set; }
        public int? NormForRate05 { get; set; }
        public decimal BasePaymentPerPatient { get; set; }
        public decimal ExtraPaymentPerPatient { get; set; }
        public bool IsNoNorm { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }

        public ICollection<SummaryProfoManualEntry> SummaryProfoManualEntries { get; set; } = new List<SummaryProfoManualEntry>();
    }
}