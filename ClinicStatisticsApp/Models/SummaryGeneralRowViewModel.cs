namespace ClinicStatisticsApp.Models
{
    public class SummaryGeneralRowViewModel
    {
        public int Number { get; set; }
        public string EmployeeFullName { get; set; } = string.Empty;

        public int AttendanceTotal { get; set; }
        public int AbsenceTotal { get; set; }
        public int GrandTotal => AttendanceTotal + AbsenceTotal;

        public int Ck { get; set; }
        public int Comfort { get; set; }
        public int Bagramyana { get; set; }
        public int Detstvo { get; set; }
        public int Gendelya { get; set; }
        public int Viktoriya { get; set; }
        public int Alfa { get; set; }
        public int Region { get; set; }
        public int Artilleriyskaya { get; set; }
        public int Selma { get; set; }
    }
}