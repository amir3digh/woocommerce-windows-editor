using System.Globalization;
using System.Windows.Data;

namespace WooCommerceProductManager.Helpers;

public sealed class PersianPriceInputConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return PersianPriceFormatter.TryParseAmount(text, out var amount)
            ? PersianPriceFormatter.FormatNumber(amount)
            : text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return PersianPriceFormatter.TryParseAmount(text, out var amount)
            ? PersianPriceFormatter.ToApiValue(amount)
            : text;
    }
}
