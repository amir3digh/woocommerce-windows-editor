using WooCommerceProductManager.Configuration;
using WooCommerceProductManager.Models;

namespace WooCommerceProductManager.Services;

public interface IProductService
{
    Task<ProductListPage> GetProductsAsync(
        int page,
        int perPage,
        string? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Official WooCommerce REST API: GET /products. Used by synchronization, not by the product list.
    /// </summary>
    Task<ProductListPage> GetRemoteProductsAsync(
        WooCommerceSettings settings,
        int page,
        int perPage,
        CancellationToken cancellationToken = default);

    Task<SaveProductResult> SaveProductAsync(
        WooCommerceSettings settings,
        Product original,
        Product edited,
        string? jsonBody,
        string? localImagePath = null,
        CancellationToken cancellationToken = default);

    Task<SaveProductResult> CreateProductAsync(
        WooCommerceSettings settings,
        string jsonBody,
        string? localImagePath = null,
        CancellationToken cancellationToken = default);

    Task<SaveProductResult> RetryWebsiteSyncAsync(
        WooCommerceSettings settings,
        Product localProduct,
        CancellationToken cancellationToken = default);

    Task<DeleteProductResult> DeleteProductAsync(
        WooCommerceSettings settings,
        Product product,
        CancellationToken cancellationToken = default);
}
