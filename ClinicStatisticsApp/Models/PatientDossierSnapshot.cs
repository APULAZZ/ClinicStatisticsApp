namespace ClinicStatisticsApp.Models;

/// <summary>Read-only snapshot copied from Firebird into the CRM database.</summary>
public class PatientDossierSnapshot
{
    public int Id { get; set; }
    public int ExternalPatientCardId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime RefreshedAt { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorText { get; set; }
    public ExternalPatientCard? ExternalPatientCard { get; set; }
}
