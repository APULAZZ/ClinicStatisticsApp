using System.Text.Json;
using System.IO;

namespace ClinicStatisticsApp.UI;

internal static class ChatServerEndpoint
{
    public static Uri GetBaseUri()
    {
        const string fallback = "http://localhost:5088";
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");
            if (!File.Exists(path)) return new Uri(fallback);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var url = document.RootElement.TryGetProperty("ChatServer", out var section) && section.TryGetProperty("BaseUrl", out var value)
                ? value.GetString() : null;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? new Uri(uri.ToString().TrimEnd('/') + "/") : new Uri(fallback);
        }
        catch { return new Uri(fallback); }
    }
}
