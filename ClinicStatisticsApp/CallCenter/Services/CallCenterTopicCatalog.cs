using System.Text.RegularExpressions;

namespace ClinicStatisticsApp.CallCenter.Services;

public static class CallCenterTopicCatalog
{
    public static readonly IReadOnlyList<string> Clinics =
    [
        "Детство", "Сельма", "Баграмяна", "Регион", "ЦК",
        "Виктория", "Артиллерийская", "Генделя", "Альфа"
    ];

    public static string GetDisplayName(string? topicName)
    {
        var name = RemoveDirectionPrefix(topicName);
        var kind = GetKind(name);
        return TryGetClinic(name, out var clinic) && kind is CallCenterTopicKind.Perk or CallCenterTopicKind.Plan
            ? $"{GetKindName(kind)} {clinic}"
            : name;
    }

    public static bool TryGetClinic(string? topicName, out string clinic)
    {
        var value = Normalize(RemoveDirectionPrefix(topicName));
        value = Regex.Replace(value, @"\b(ПЕРК|ПЛАН)\b", " ");
        value = Normalize(value);

        if (ContainsAny(value, "ДЕТСТВО", "МСК", "МОСКОВ")) { clinic = "Детство"; return true; }
        if (ContainsAny(value, "СЕЛЬМ")) { clinic = "Сельма"; return true; }
        if (ContainsAny(value, "МЕД", "БАГР")) { clinic = "Баграмяна"; return true; }
        if (ContainsAny(value, "РЕГИОН")) { clinic = "Регион"; return true; }
        if (ContainsAny(value, "ВИКТОРИ")) { clinic = "Виктория"; return true; }
        if (ContainsAny(value, "АРТИЛ", "АРТ")) { clinic = "Артиллерийская"; return true; }
        if (ContainsAny(value, "ГЕНДЕЛ")) { clinic = "Генделя"; return true; }
        if (ContainsAny(value, "АЛЬФ")) { clinic = "Альфа"; return true; }
        if (Regex.IsMatch(value, @"(^|\s)ЦК(\s|$)")) { clinic = "ЦК"; return true; }

        clinic = string.Empty;
        return false;
    }

    public static CallCenterTopicKind GetKind(string? topicName)
    {
        var value = Normalize(topicName);
        if (value.Contains("ПЕРК", StringComparison.Ordinal)) return CallCenterTopicKind.Perk;
        if (value.Contains("ПЛАН", StringComparison.Ordinal)) return CallCenterTopicKind.Plan;
        if (value.Contains("НЕЗАПИС", StringComparison.Ordinal) || value.Contains("НЕ ЗАПИС", StringComparison.Ordinal)) return CallCenterTopicKind.NoAppointment;
        if (value.Contains("СБРОС", StringComparison.Ordinal)) return CallCenterTopicKind.Drop;
        return CallCenterTopicKind.Other;
    }

    public static bool IsTransferTopic(string? topicName) => Normalize(topicName).Contains("ПЕРЕВОД", StringComparison.Ordinal);

    private static string GetKindName(CallCenterTopicKind kind) => kind == CallCenterTopicKind.Perk ? "ПЕРК" : "ПЛАН";
    private static bool ContainsAny(string value, params string[] parts) => parts.Any(value.Contains);
    private static string Normalize(string? value) => string.Join(" ", (value ?? string.Empty).Trim().ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static string RemoveDirectionPrefix(string? topicName) => Regex.Replace(topicName?.Trim() ?? string.Empty, @"^(ВХОДЯЩИЙ|ИСХОДЯЩИЙ|ВХ\.?|ИСХ\.?)\s+", string.Empty, RegexOptions.IgnoreCase).Trim();
}

public enum CallCenterTopicKind { Other, Perk, Plan, NoAppointment, Drop }
