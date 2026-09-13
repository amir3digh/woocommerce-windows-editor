namespace WooCommerceProductManager.Services;

public interface IProductImageCache
{
    string? GetExistingPath(long wooCommerceId, string? imageUrl);

    Task<string?> GetOrDownloadAsync(
        long wooCommerceId,
        string? imageUrl,
        bool forceRefresh,
        CancellationToken cancellationToken = default);

    string? StoreFromFile(long wooCommerceId, string? imageUrl, string localFilePath);

    void RemoveAll(long wooCommerceId);
}
