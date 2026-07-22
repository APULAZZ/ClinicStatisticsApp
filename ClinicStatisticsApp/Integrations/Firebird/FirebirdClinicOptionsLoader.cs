using System.Text.Json;
using System.IO;

namespace ClinicStatisticsApp.Integrations.Firebird;

public static class FirebirdClinicOptionsLoader
{
    public static IReadOnlyList<FirebirdClinicConnectionOptions> Load(string? baseDirectory = null)
    {
        var path = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "firebird.Local.json");
        if (!File.Exists(path)) return Array.Empty<FirebirdClinicConnectionOptions>();

        using var stream = File.OpenRead(path);
        var settings = JsonSerializer.Deserialize<FirebirdLocalSettings>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (settings?.FirebirdClinics is not { } clinics || clinics.Count == 0)
            return Array.Empty<FirebirdClinicConnectionOptions>();

        // All clinic databases currently use the same locally stored Firebird
        // credentials. Additional sources may omit them so the secret is not
        // duplicated in the configuration file.
        var credentialSource = clinics.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.User) && !string.IsNullOrWhiteSpace(x.Password));
        return clinics.Select(x => new FirebirdClinicConnectionOptions
        {
            ClinicDataSourceId = x.ClinicDataSourceId,
            Server = x.Server,
            Port = x.Port,
            ConnectionTimeoutSeconds = x.ConnectionTimeoutSeconds,
            DatabasePath = x.DatabasePath,
            Charset = x.Charset,
            User = string.IsNullOrWhiteSpace(x.User) ? credentialSource?.User ?? string.Empty : x.User,
            Password = string.IsNullOrWhiteSpace(x.Password) ? credentialSource?.Password ?? string.Empty : x.Password
        }).ToList();
    }

    private sealed class FirebirdLocalSettings
    {
        public List<FirebirdClinicConnectionOptions>? FirebirdClinics { get; init; }
    }
}
