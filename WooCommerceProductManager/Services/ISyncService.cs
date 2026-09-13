using WooCommerceProductManager.Configuration;

namespace WooCommerceProductManager.Services;

public interface ISyncService
{
    Task<SyncFromWebsiteResult> SyncFromWebsiteAsync(
        WooCommerceSettings settings,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
