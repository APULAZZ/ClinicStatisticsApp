namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>
/// Minimal, non-clinical patient data read from a Firebird source database.
/// </summary>
public sealed class FirebirdPatientSnapshot
{
    public long SourcePatientId { get; init; }
    public string? SourceCardNumber { get; init; }
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string? MiddleName { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? MobilePhone { get; init; }
    public string? WorkPhone { get; init; }
    public string? HomePhone { get; init; }
    public string? Email { get; init; }
}
