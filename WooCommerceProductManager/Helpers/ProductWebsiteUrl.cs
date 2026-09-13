using System.Diagnostics.CodeAnalysis;
using WooCommerceProductManager.Models;

namespace WooCommerceProductManager.Helpers;

public static class ProductWebsiteUrl
{
    public static bool TryCreate(string? storeUrl, Product product, [NotNullWhen(true)] out Uri? url)
    {
        url = null;
        if (product is null || product.Id <= 0)
        {
            return false;
        }

        if (TryCreateHttpsUri(product.Permalink, out url))
        {
            return true;
        }

        if (!WooCommerceUrlValidator.TryValidateHttpsStoreUrl(storeUrl, out var normalizedStoreUrl, out _))
        {
            return false;
        }

        var storeUri = new Uri(normalizedStoreUrl, UriKind.Absolute);
        var path = storeUri.AbsolutePath.TrimEnd('/');
        const string apiMarker = "/wp-json/wc/v3";
        var markerIndex = path.LastIndexOf(apiMarker, StringComparison.OrdinalIgnoreCase);
        var sitePath = markerIndex >= 0 ? path[..markerIndex] : string.Empty;
        return Uri.TryCreate($"{storeUri.Scheme}://{storeUri.Authority}{sitePath}/?p={product.Id}", UriKind.Absolute, out url);
    }

    private static bool TryCreateHttpsUri(string? value, [NotNullWhen(true)] out Uri? url)
    {
        url = null;
        return !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value, UriKind.Absolute, out url)
            && url is not null
            && string.Equals(url.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
