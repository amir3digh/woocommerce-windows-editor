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

            error = UiStrings.RegularPriceRequired;
            return false;
        }

        var trimmed = PersianPriceFormatter.NormalizeForParse(text);
        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            && !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
        {
            error = UiStrings.InvalidPrice;
            return false;
        }

        if (amount < 0)
        {
            error = UiStrings.PriceCannotBeNegative;
            return false;
        }

        apiValue = amount.ToString("0.##", CultureInfo.InvariantCulture);
        return true;
    }
}
