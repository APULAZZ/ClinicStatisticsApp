namespace ClinicStatisticsApp.Models;

/// <summary>Read-only snapshot of a completed MedM visit, stored in SQL CRM for visit analytics.</summary>
public class CrmVisitFunnelEntry
{
    public int Id { get; set; }
    public int ClinicDataSourceId { get; set; }
    public long SourceVisitId { get; set; }
    public long SourcePatientId { get; set; }
    public DateTime VisitDate { get; set; }
    public DateTime SyncedAt { get; set; }
}
