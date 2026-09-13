namespace WooCommerceProductManager.Helpers;

public static class WooCommerceUrlValidator
{
    public static bool TryValidateHttpsStoreUrl(string? storeUrl, out string normalizedUrl, out string? error)
    {
        normalizedUrl = string.Empty;
        error = null;

        if (string.IsNullOrWhiteSpace(storeUrl))
        {
            error = UiStrings.StoreUrlRequired;
            return false;
        }

        var trimmed = storeUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            error = UiStrings.StoreUrlInvalid;
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            error = UiStrings.StoreUrlMustBeHttps;
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
