namespace ClinicStatisticsApp.Integrations.Firebird;

/// <summary>
/// Local connection details for a single clinic Firebird database.
/// Store real values only in appsettings.Local.json; never in source control.
/// </summary>
public sealed class FirebirdClinicConnectionOptions
{
    public int ClinicDataSourceId { get; init; }
    public string Server { get; init; } = string.Empty;
    public int Port { get; init; } = 3050;
    public int ConnectionTimeoutSeconds { get; init; } = 60;
    public string DatabasePath { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Charset { get; init; } = "WIN1251";
}
