using System.Text;

namespace ClinicStatisticsApp.UI;

internal static class RussianText
{
    public static string Fix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || (!value.Contains('Р') && !value.Contains('С'))) return value ?? string.Empty;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            var fixedValue = Encoding.UTF8.GetString(Encoding.GetEncoding(1251).GetBytes(value));
            return fixedValue.Count(ch => ch == '�') == 0 ? fixedValue : value;
        }
        catch { return value; }
    }
}
