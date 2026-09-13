using WooCommerceProductManager.Models;
using WooCommerceProductManager.Services;

namespace WooCommerceProductManager.Repositories;

public interface IProductRepository
{
    Task<ProductListPage> GetPageAsync(
        int page,
        int perPage,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts or updates from WooCommerce. Dirty local rows are left unchanged.
    /// </summary>
    Task<RemoteUpsertResult> UpsertFromRemoteAsync(Product remote, DateTimeOffset syncedAt, CancellationToken cancellationToken = default);

    Task<Product?> SaveLocalEditsAsync(Product product, bool isDirty, CancellationToken cancellationToken = default);

    Task<Product?> ReplaceWithRemoteAsync(Product remote, DateTimeOffset syncedAt, CancellationToken cancellationToken = default);

    Task<Product> InsertFromRemoteAsync(Product remote, DateTimeOffset syncedAt, CancellationToken cancellationToken = default);

    Task DeleteAsync(long localId, long wooCommerceId, CancellationToken cancellationToken = default);
}

public enum RemoteUpsertResult
{
    Added,
    Updated,
    Conflict
}
