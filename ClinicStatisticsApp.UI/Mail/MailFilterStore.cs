using System.IO;
using System.Text.Json;

namespace ClinicStatisticsApp.UI.Mail;

public sealed class MailFilterRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SenderContains { get; set; } = string.Empty;
    public string SubjectContains { get; set; } = string.Empty;
    public string DestinationFolder { get; set; } = string.Empty;
    public bool MarkAsRead { get; set; }
    public string Description => $"От: {SenderContains}  •  Тема: {SubjectContains}  →  {DestinationFolder}{(MarkAsRead ? "  (прочитано)" : string.Empty)}";
}

public static class MailFilterStore
{
    private static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicStatisticsApp");
    public static List<MailFilterRule> Load(int userId)
    {
        var path = Path.Combine(Folder, $"mail-filters-{userId}.json");
        if (!File.Exists(path)) return [];
        try { return JsonSerializer.Deserialize<List<MailFilterRule>>(File.ReadAllText(path)) ?? []; }
        catch { return []; }
    }
    public static void Save(int userId, IEnumerable<MailFilterRule> rules)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, $"mail-filters-{userId}.json"), JsonSerializer.Serialize(rules));
    }
}
