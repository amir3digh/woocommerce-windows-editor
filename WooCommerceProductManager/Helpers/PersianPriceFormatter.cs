using System.Globalization;
using System.Text;

namespace WooCommerceProductManager.Helpers;

public static class PersianPriceFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string FormatForDisplay(string? apiPrice, bool showPlaceholderWhenEmpty = true)
    {
        if (string.IsNullOrWhiteSpace(apiPrice))
        {
            return showPlaceholderWhenEmpty ? "—" : string.Empty;
        }

        if (!TryParseAmount(apiPrice, out var amount))
        {
            return showPlaceholderWhenEmpty ? "—" : string.Empty;
        }

        return $"{FormatNumber(amount)} {UiStrings.Currency}";
    }

    public static string FormatNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !TryParseAmount(text, out var amount))
        {
            return text ?? string.Empty;
        }

        return FormatNumber(amount);
    }

    public static string FormatNumber(decimal amount)
    {
        var pattern = amount == decimal.Truncate(amount) ? "#,##0" : "#,##0.##";
        return ToPersianDigits(amount.ToString(pattern, Invariant));
    }

    public static string ToApiValue(decimal amount) => amount.ToString("0.##", Invariant);

    public static bool TryParseAmount(string? text, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = NormalizeForParse(text);
        return decimal.TryParse(normalized, NumberStyles.Number, Invariant, out amount)
            || decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.CurrentCulture, out amount);
    }

    public static string NormalizeForParse(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Trim())
        {
            if (character is ',' or '٬' or '،' or ' ' or '\u00A0')
            {
                continue;
            }

            if (character is >= '۰' and <= '۹')
            {
                builder.Append((char)('0' + (character - '۰')));
                continue;
            }

            if (character is >= '٠' and <= '٩')
            {
                builder.Append((char)('0' + (character - '٠')));
                continue;
            }

            builder.Append(character is '٫' ? '.' : character);
        }

        return builder.ToString();
    }

    public static string ToPersianDigits(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character is >= '0' and <= '9' ? (char)('۰' + (character - '0')) : character);
        }

        return builder.ToString();
    }
}
