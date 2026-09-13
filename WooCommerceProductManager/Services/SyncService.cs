using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;
using WooCommerceProductManager.Repositories;

namespace WooCommerceProductManager.Services;

public sealed class SyncService : ISyncService
{
    private readonly IProductService _productService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IProductService productService,
        IProductRepository productRepository,
        ILogger<SyncService> logger)
    {
        _productService = productService;
        _productRepository = productRepository;
        _logger = logger;
    }

    public async Task<SyncFromWebsiteResult> SyncFromWebsiteAsync(
        WooCommerceSettings settings,
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var downloaded = 0;
        var updated = 0;
        var added = 0;
        var conflicts = 0;
        var errors = 0;
        var conflictSummaries = new List<string>();

        try
        {
            var page = 1;
            int? totalPages = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new SyncProgress
                {
                    Downloaded = downloaded,
                    Updated = updated,
                    Added = added,
                    Conflicts = conflicts,
                    Errors = errors,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    StatusMessage = $"Syncing page {page}..."
                });

                var remotePage = await _productService
                    .GetRemoteProductsAsync(settings, page, ProductService.DefaultPageSize, cancellationToken)
                    .ConfigureAwait(false);

                totalPages = remotePage.TotalPages;
                var syncedAt = DateTimeOffset.Now;

                foreach (var remote in remotePage.Products)
                {
                    downloaded++;
                    try
                    {
                        var outcome = await _productRepository
                            .UpsertFromRemoteAsync(remote, syncedAt, cancellationToken)
                            .ConfigureAwait(false);

                        switch (outcome)
                        {
                            case RemoteUpsertResult.Added:
                                added++;
                                break;
                            case RemoteUpsertResult.Updated:
                                updated++;
                                break;
                            case RemoteUpsertResult.Conflict:
                                conflicts++;
                                conflictSummaries.Add($"{remote.Name} (WooCommerce ID {remote.Id})");
                                _logger.LogWarning(
                                    "Skipped overwriting dirty local product. WooCommerceId: {WooCommerceId}.",
                                    remote.Id);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors++;
                        _logger.LogError(ex, "Failed to save remote product. WooCommerceId: {WooCommerceId}.", remote.Id);
                    }
                }

                progress?.Report(new SyncProgress
                {
                    Downloaded = downloaded,
                    Updated = updated,
                    Added = added,
                    Conflicts = conflicts,
                    Errors = errors,
                    CurrentPage = page,
                    TotalPages = totalPages,
                    StatusMessage = $"Finished page {page}."
                });

                var isLastPage = totalPages is int known && known > 0
                    ? page >= known
                    : remotePage.Products.Count < ProductService.DefaultPageSize;

                if (isLastPage || remotePage.Products.Count == 0)
                {
                    break;
                }

                page++;
            }

            _logger.LogInformation(
                "Sync from website completed. Downloaded: {Downloaded}. Added: {Added}. Updated: {Updated}. Conflicts: {Conflicts}. Errors: {Errors}.",
                downloaded,
                added,
                updated,
                conflicts,
                errors);

            var result = new SyncFromWebsiteResult
            {
                Downloaded = downloaded,
                Updated = updated,
                Added = added,
                Conflicts = conflicts,
                Errors = errors,
                Succeeded = errors == 0,
                ConflictSummaries = conflictSummaries
            };

            progress?.Report(new SyncProgress
            {
                Downloaded = downloaded,
                Updated = updated,
                Added = added,
                Conflicts = conflicts,
                Errors = errors,
                TotalPages = totalPages,
                IsComplete = true,
                StatusMessage = "Sync complete."
            });

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Sync from website was cancelled.");
            throw;
        }
        catch (WooCommerceApiException ex)
        {
            _logger.LogError(ex, "Sync from website failed. Status: {StatusCode}", ex.StatusCode);
            return new SyncFromWebsiteResult
            {
                Downloaded = downloaded,
                Updated = updated,
                Added = added,
                Conflicts = conflicts,
                Errors = errors + 1,
                Succeeded = false,
                ErrorMessage = ex.UserMessage,
                ConflictSummaries = conflictSummaries
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync from website failed unexpectedly.");
            return new SyncFromWebsiteResult
            {
                Downloaded = downloaded,
                Updated = updated,
                Added = added,
                Conflicts = conflicts,
                Errors = errors + 1,
                Succeeded = false,
                ErrorMessage = "Synchronization failed. Local products were not deleted.",
                ConflictSummaries = conflictSummaries
            };
        }
    }
}
