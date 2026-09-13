namespace WooCommerceProductManager.Configuration;

/// <summary>
/// WooCommerce REST API connection settings. Credentials must never be hard-coded
/// or committed to source control.
/// </summary>
public sealed class WooCommerceSettings
{
    public const string SectionName = "WooCommerce";

    /// <summary>
    /// WooCommerce REST API base URL, for example https://your-store.com/wp-json/wc/v3/
    /// </summary>
    public string StoreUrl { get; set; } = string.Empty;

    public string ConsumerKey { get; set; } = string.Empty;

    public string ConsumerSecret { get; set; } = string.Empty;

    public string WordPressUsername { get; set; } = string.Empty;

    public string ApplicationPassword { get; set; } = string.Empty;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(StoreUrl)
        && !string.IsNullOrWhiteSpace(ConsumerKey)
        && !string.IsNullOrWhiteSpace(ConsumerSecret);

    public bool HasMediaCredentials =>
        !string.IsNullOrWhiteSpace(WordPressUsername)
        && !string.IsNullOrWhiteSpace(ApplicationPassword);

    public WooCommerceSettings Clone() => new()
    {
        StoreUrl = StoreUrl,
        ConsumerKey = ConsumerKey,
        ConsumerSecret = ConsumerSecret,
        WordPressUsername = WordPressUsername,
        ApplicationPassword = ApplicationPassword
    };
}
