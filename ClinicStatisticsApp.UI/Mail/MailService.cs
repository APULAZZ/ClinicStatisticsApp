using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;
using System.IO;

namespace ClinicStatisticsApp.UI.Mail;

public sealed record MailMessageItem(uint Uid, string Sender, string Subject, string Preview, DateTimeOffset Date, bool IsUnread);
public sealed record MailAttachment(string FileName, string ContentType, int Index);
public sealed record MailMessageDetails(string Sender, string Recipients, string Subject, DateTimeOffset Date, string Body, IReadOnlyList<MailAttachment> Attachments);

public sealed class MailService
{
    private readonly MailSettings _settings;
    public MailService(MailSettings settings) => _settings = settings;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.Address) && !string.IsNullOrWhiteSpace(_settings.Password);
    public string AccountKey => _settings.Address;

    public async Task<IReadOnlyList<string>> GetFoldersAsync()
    {
        using var client = await ConnectImapAsync();
        var folders = await client.GetFoldersAsync(client.PersonalNamespaces[0]);
        return folders.Where(x => x.Attributes is not FolderAttributes.NoSelect).Select(x => x.FullName).OrderBy(x => x).ToList();
    }

    public async Task<IReadOnlyList<MailMessageItem>> GetMessagesAsync(string folderName)
    {
        using var client = await ConnectImapAsync();
        return await GetMessagesAsync(client, folderName);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<MailMessageItem>>> PrefetchAsync(IEnumerable<string> folderNames)
    {
        using var client = await ConnectImapAsync();
        var result = new Dictionary<string, IReadOnlyList<MailMessageItem>>();
        foreach (var folderName in folderNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { result[folderName] = await GetMessagesAsync(client, folderName); }
            catch { /* Недоступная папка не должна останавливать предзагрузку остальных. */ }
        }
        return result;
    }

    private static async Task<IReadOnlyList<MailMessageItem>> GetMessagesAsync(ImapClient client, string folderName)
    {
        var folder = await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadWrite);
        var uids = await folder.SearchAsync(SearchQuery.All);
        var summaries = await folder.FetchAsync(uids.TakeLast(100).ToList(), MessageSummaryItems.Envelope | MessageSummaryItems.Flags | MessageSummaryItems.InternalDate | MessageSummaryItems.UniqueId);
        return summaries.OrderByDescending(x => x.Date).Select(x => new MailMessageItem(
            x.UniqueId.Id,
            x.Envelope?.From.Mailboxes.FirstOrDefault()?.ToString() ?? "Без отправителя",
            x.Envelope?.Subject ?? "(без темы)",
            string.Empty,
            x.Date,
            x.Flags is null || !x.Flags.Value.HasFlag(MessageFlags.Seen))).ToList();
    }

    public async Task<MailMessageDetails> GetMessageAsync(string folderName, uint uid)
    {
        using var client = await ConnectImapAsync();
        var folder = await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadWrite);
        var message = await folder.GetMessageAsync(new UniqueId(uid));
        await folder.AddFlagsAsync(new UniqueId(uid), MessageFlags.Seen, true);
        var attachments = message.Attachments.Select((part, index) => new MailAttachment(
            part.ContentDisposition?.FileName ?? part.ContentType.Name ?? $"Вложение {index + 1}",
            part.ContentType.MimeType,
            index)).ToList();
        return new MailMessageDetails(message.From.ToString(), message.To.ToString(), message.Subject ?? "(без темы)", message.Date, message.TextBody ?? HtmlToText(message.HtmlBody), attachments);
    }

    public async Task SetReadAsync(string folderName, IEnumerable<uint> uids, bool isRead)
    {
        using var client = await ConnectImapAsync();
        var folder = await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadWrite);
        var ids = uids.Select(x => new UniqueId(x)).ToList();
        if (isRead) await folder.AddFlagsAsync(ids, MessageFlags.Seen, true);
        else await folder.RemoveFlagsAsync(ids, MessageFlags.Seen, true);
    }

    public async Task MoveAsync(string sourceFolderName, string destinationFolderName, IEnumerable<uint> uids)
    {
        using var client = await ConnectImapAsync();
        var source = await client.GetFolderAsync(sourceFolderName);
        var destination = await client.GetFolderAsync(destinationFolderName);
        await source.OpenAsync(FolderAccess.ReadWrite);
        await source.MoveToAsync(uids.Select(x => new UniqueId(x)).ToList(), destination);
    }

    public async Task ApplyFiltersAsync(string sourceFolderName, IEnumerable<(uint Uid, string DestinationFolder, bool MarkAsRead)> actions)
    {
        var prepared = actions.ToList();
        if (prepared.Count == 0) return;
        using var client = await ConnectImapAsync();
        var source = await client.GetFolderAsync(sourceFolderName);
        await source.OpenAsync(FolderAccess.ReadWrite);
        var readIds = prepared.Where(x => x.MarkAsRead).Select(x => new UniqueId(x.Uid)).ToList();
        if (readIds.Count > 0) await source.AddFlagsAsync(readIds, MessageFlags.Seen, true);
        foreach (var group in prepared.Where(x => !string.IsNullOrWhiteSpace(x.DestinationFolder)).GroupBy(x => x.DestinationFolder))
        {
            var destination = await client.GetFolderAsync(group.Key);
            await source.MoveToAsync(group.Select(x => new UniqueId(x.Uid)).ToList(), destination);
        }
    }

    public async Task SaveAttachmentAsync(string folderName, uint uid, int attachmentIndex, string destinationPath)
    {
        using var client = await ConnectImapAsync();
        var folder = await client.GetFolderAsync(folderName);
        await folder.OpenAsync(FolderAccess.ReadOnly);
        var message = await folder.GetMessageAsync(new UniqueId(uid));
        var part = message.Attachments.ElementAt(attachmentIndex);
        await using var output = File.Create(destinationPath);
        if (part is MimePart { Content: not null } mimePart) await mimePart.Content.DecodeToAsync(output);
        else if (part is MessagePart { Message: not null } messagePart) await messagePart.Message.WriteToAsync(output);
        else throw new InvalidOperationException("Вложение недоступно для сохранения.");
    }

    public async Task SendAsync(string recipients, string subject, string body, IEnumerable<string>? attachmentPaths = null)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_settings.Address));
        foreach (var address in recipients.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) message.To.Add(MailboxAddress.Parse(address));
        message.Subject = subject;
        var builder = new BodyBuilder { TextBody = body };
        foreach (var path in attachmentPaths ?? []) builder.Attachments.Add(path);
        message.Body = builder.ToMessageBody();
        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.mail.ru", 465, MailKit.Security.SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(_settings.Address, _settings.Password);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task TestAsync()
    {
        using var client = await ConnectImapAsync();
        await client.DisconnectAsync(true);
    }

    private async Task<ImapClient> ConnectImapAsync()
    {
        if (!IsConfigured) throw new InvalidOperationException("Сначала укажите почтовый ящик и пароль в настройках почты.");
        var client = new ImapClient();
        await client.ConnectAsync("imap.mail.ru", 993, MailKit.Security.SecureSocketOptions.SslOnConnect);
        await client.AuthenticateAsync(_settings.Address, _settings.Password);
        return client;
    }

    private static string HtmlToText(string? html) => string.IsNullOrWhiteSpace(html) ? string.Empty : System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
}
