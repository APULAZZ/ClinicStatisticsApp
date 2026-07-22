namespace ClinicStatisticsApp.UI.Mail;

public static class MailFolderCache
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, CacheEntry> Entries = [];
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public static bool TryGet(string account, string folder, out IReadOnlyList<MailMessageItem> messages)
    {
        lock (Sync)
        {
            if (Entries.TryGetValue(Key(account, folder), out var entry) && DateTimeOffset.UtcNow - entry.LoadedAt < Lifetime)
            {
                messages = entry.Messages;
                return true;
            }
        }
        messages = [];
        return false;
    }

    public static void Store(string account, string folder, IReadOnlyList<MailMessageItem> messages)
    {
        lock (Sync) Entries[Key(account, folder)] = new CacheEntry(messages, DateTimeOffset.UtcNow);
    }

    public static void Invalidate(string account, string folder)
    {
        lock (Sync) Entries.Remove(Key(account, folder));
    }

    private static string Key(string account, string folder) => $"{account.Trim().ToLowerInvariant()}::{folder}";
    private sealed record CacheEntry(IReadOnlyList<MailMessageItem> Messages, DateTimeOffset LoadedAt);
}
