using System.Text.Json;
using System.IO;

namespace ClinicStatisticsApp.CallCenter.Services;

/// <summary>Loads local MANGO credentials without keeping them in source control.</summary>
public static class MangoApiOptionsLoader
{
    public static MangoApiOptions Load(string? baseDirectory = null)
    {
        var path = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "appsettings.Local.json");
        if (!File.Exists(path))
            return new MangoApiOptions();

        using var stream = File.OpenRead(path);
        var document = JsonSerializer.Deserialize<MangoLocalSettings>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return document?.MangoApi ?? new MangoApiOptions();
    }

    private sealed class MangoLocalSettings
    {
        public MangoApiOptions? MangoApi { get; init; }
    }
}
