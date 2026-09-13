namespace WooCommerceProductManager.Helpers;

public static class WooCommerceUrlValidator
{
    public static bool TryValidateHttpsStoreUrl(string? storeUrl, out string normalizedUrl, out string? error)
    {
        normalizedUrl = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(storeUrl))
        {
            error = "Store URL is required.";
            return false;
        }

        var trimmed = storeUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = "Store URL is not a valid absolute URL.";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = "Store URL must use HTTPS.";
            return false;
        }

        normalizedUrl = uri.ToString();
        if (!normalizedUrl.EndsWith('/'))
        {
            normalizedUrl += "/";
        }

        return true;
    }
}
