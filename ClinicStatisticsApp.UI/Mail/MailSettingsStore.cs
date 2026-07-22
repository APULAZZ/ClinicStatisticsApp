using System.Security.Cryptography;
using System.Text.Json;
using System.IO;

namespace ClinicStatisticsApp.UI.Mail;

public sealed class MailSettings
{
    public string Address { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public static class MailSettingsStore
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicStatisticsApp");

    public static MailSettings Load(int userId)
    {
        var path = GetPath(userId);
        if (!File.Exists(path)) return new MailSettings();
        try
        {
            var saved = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(path)) ?? new SavedSettings();
            return new MailSettings
            {
                Address = saved.Address ?? string.Empty,
                Password = string.IsNullOrEmpty(saved.Password) ? string.Empty : System.Text.Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(saved.Password), null, DataProtectionScope.CurrentUser))
            };
        }
        catch { return new MailSettings(); }
    }

    public static void Save(int userId, MailSettings settings)
    {
        Directory.CreateDirectory(Folder);
        var saved = new SavedSettings
        {
            Address = settings.Address.Trim(),
            Password = string.IsNullOrEmpty(settings.Password) ? string.Empty : Convert.ToBase64String(ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(settings.Password), null, DataProtectionScope.CurrentUser))
        };
        File.WriteAllText(GetPath(userId), JsonSerializer.Serialize(saved));
    }

    private static string GetPath(int userId) => Path.Combine(Folder, $"mail-settings-{userId}.json");
    private sealed class SavedSettings { public string? Address { get; set; } public string? Password { get; set; } }
}
