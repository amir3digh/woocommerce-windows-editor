using System.Globalization;

namespace WooCommerceProductManager.Helpers;

public static class WooCommercePriceParser
{
    public static bool TryParseNonNegative(
        string? text,
        bool allowEmpty,
        out string apiValue,
        out string? error)
    {
        apiValue = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            if (allowEmpty)
            {
                return true;
            }

            error = "Regular price is required.";
            return false;
        }

        var trimmed = text.Trim();
        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            && !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            error = "Enter a valid non-negative price.";
            return false;
        }

        if (amount < 0)
        {
            error = "Price cannot be negative.";
            return false;
        }

        apiValue = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return true;
    }
}
