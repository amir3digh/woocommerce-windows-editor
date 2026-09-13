using WooCommerceProductManager.Configuration;

namespace WooCommerceProductManager.Services;

public interface IWooCommerceApiClient
{
    void ApplySettings(WooCommerceSettings settings);

    Task<WooCommerceHttpResponse> GetAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an authenticated GET request to the official WooCommerce REST API
    /// <c>products</c> collection with <c>per_page=1</c> to verify credentials.
    /// </summary>
    Task TestConnectionAsync(WooCommerceSettings settings, CancellationToken cancellationToken = default);

    Task<WooCommerceHttpResponse> PutAsync(string relativePath, string jsonBody, CancellationToken cancellationToken = default);
}
