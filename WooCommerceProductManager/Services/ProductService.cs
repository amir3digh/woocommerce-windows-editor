using System.Text.Json;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;
using WooCommerceProductManager.Helpers;
using WooCommerceProductManager.Models;
using WooCommerceProductManager.Repositories;

namespace WooCommerceProductManager.Services;

public sealed class ProductService : IProductService
{
    public const int DefaultPageSize = 50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly IProductRepository _productRepository;
    private readonly IWooCommerceApiClient _apiClient;
    private readonly ILogger<ProductService> _logger;

    public ProductService(
        IProductRepository productRepository,
        IWooCommerceApiClient apiClient,
        ILogger<ProductService> logger)
    {
        _productRepository = productRepository;
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<ProductListPage> GetProductsAsync(
        int page,
        int perPage,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be 1 or greater.");
        }

        if (perPage is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(perPage), "Page size must be between 1 and 100.");
        }

        var result = await _productRepository
            .GetPageAsync(page, perPage, search, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Loaded {Count} products from the local database. Page {Page} of {TotalPages}. Search: {HasSearch}.",
            result.Products.Count,
            result.Page,
            result.TotalPages,
            !string.IsNullOrWhiteSpace(search));

        return result;
    }

    public async Task<ProductListPage> GetRemoteProductsAsync(
        WooCommerceSettings settings,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be 1 or greater.");
        }

        if (perPage is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(perPage), "per_page must be between 1 and 100.");
        }

        _apiClient.ApplySettings(settings);

        var relativePath = $"products?page={page}&per_page={perPage}";
        var response = await _apiClient.GetAsync(relativePath, cancellationToken).ConfigureAwait(false);

        List<Product>? products;
        try
        {
            products = JsonSerializer.Deserialize<List<Product>>(response.Body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize WooCommerce products JSON.");
            throw new WooCommerceApiException(
                UiStrings.InvalidProductsJson,
                diagnosticMessage: "Invalid products JSON.",
                innerException: ex);
        }

        products ??= [];
        foreach (var product in products)
        {
            product.Images ??= [];
        }

        _logger.LogInformation(
            "Retrieved {Count} products from WooCommerce. Page {Page}. Total pages: {TotalPages}.",
            products.Count,
            page,
            response.TotalPages);

        return new ProductListPage
        {
            Products = products,
            Page = page,
            PerPage = perPage,
            TotalItems = response.TotalItems,
            TotalPages = response.TotalPages
        };
    }

    public async Task<SaveProductResult> SaveProductAsync(
        WooCommerceSettings settings,
        Product original,
        Product edited,
        string? jsonBody,
        string? localImagePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(edited);

        var local = await _productRepository
            .SaveLocalEditsAsync(edited, isDirty: true, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The product was not found in the local database.");

        _logger.LogInformation("Saved local edits for WooCommerceId {WooCommerceId}.", local.Id);

        return await PushJsonToWebsiteAsync(settings, local, jsonBody, localImagePath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SaveProductResult> CreateProductAsync(
        WooCommerceSettings settings,
        string jsonBody,
        string? localImagePath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonBody);

        try
        {
            _apiClient.ApplySettings(settings);

            if (!string.IsNullOrWhiteSpace(localImagePath))
            {
                var media = await _apiClient.UploadMediaAsync(localImagePath, cancellationToken).ConfigureAwait(false);
                jsonBody = ProductUpdatePayload.WithFeaturedImage(jsonBody, currentImages: null, media.Id);
            }

            var response = await _apiClient
                .PostAsync("products", jsonBody, cancellationToken)
                .ConfigureAwait(false);

            Product? remote;
            try
            {
                remote = JsonSerializer.Deserialize<Product>(response.Body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "WooCommerce returned invalid product JSON after POST /products.");
                throw new WooCommerceApiException(
                    UiStrings.SavedButUnreadableResponse,
                    diagnosticMessage: "Invalid product JSON after create.",
                    innerException: ex);
            }

            if (remote is null || remote.Id <= 0)
            {
                throw new WooCommerceApiException(UiStrings.UnableToCreateProduct);
            }

            remote.Images ??= [];
            var saved = await _productRepository
                .InsertFromRemoteAsync(remote, DateTimeOffset.Now, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Created WooCommerce product {WooCommerceId} and stored it locally.", saved.Id);

            return new SaveProductResult
            {
                Product = saved,
                RemoteSaved = true
            };
        }
        catch (WooCommerceApiException ex)
        {
            _logger.LogWarning(ex, "Creating a WooCommerce product failed.");
            return new SaveProductResult
            {
                Product = new Product(),
                RemoteSaved = false,
                RemoteError = ex.UserMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Creating a WooCommerce product failed unexpectedly.");
            return new SaveProductResult
            {
                Product = new Product(),
                RemoteSaved = false,
                RemoteError = UiStrings.UnableToCreateProduct
            };
        }
    }

    public async Task<SaveProductResult> RetryWebsiteSyncAsync(
        WooCommerceSettings settings,
        Product localProduct,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(localProduct);

        LocalImagePath.TryGetFilePath(localProduct.FirstImageUrl, out var localImagePath);
        var jsonBody = ProductUpdatePayload.BuildFullJson(localProduct);
        return await PushJsonToWebsiteAsync(
                settings,
                localProduct,
                jsonBody,
                string.IsNullOrWhiteSpace(localImagePath) ? null : localImagePath,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SaveProductResult> PushJsonToWebsiteAsync(
        WooCommerceSettings settings,
        Product local,
        string? jsonBody,
        string? localImagePath,
        CancellationToken cancellationToken)
    {
        try
        {
            _apiClient.ApplySettings(settings);

            if (!string.IsNullOrWhiteSpace(localImagePath))
            {
                var media = await _apiClient.UploadMediaAsync(localImagePath, cancellationToken).ConfigureAwait(false);
                var currentImages = await TryGetRemoteImagesAsync(local.Id, cancellationToken).ConfigureAwait(false);
                jsonBody = ProductUpdatePayload.WithFeaturedImage(jsonBody, currentImages, media.Id);
            }

            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                return new SaveProductResult
                {
                    Product = local,
                    RemoteSaved = true
                };
            }

            var response = await _apiClient
                .PutAsync($"products/{local.Id}", jsonBody, cancellationToken)
                .ConfigureAwait(false);

            Product? remote;
            try
            {
                remote = JsonSerializer.Deserialize<Product>(response.Body, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "WooCommerce returned invalid product JSON after PUT /products/{Id}.", local.Id);
                throw new WooCommerceApiException(
                    UiStrings.SavedButUnreadableResponse,
                    diagnosticMessage: "Invalid product JSON after update.",
                    innerException: ex);
            }

            if (remote is null)
            {
                throw new WooCommerceApiException(UiStrings.DidNotReturnUpdatedProduct);
            }

            remote.LocalId = local.LocalId;
            remote.Images ??= [];

            var saved = await _productRepository
                .ReplaceWithRemoteAsync(remote, DateTimeOffset.Now, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException("The product was not found in the local database after the website update.");

            _logger.LogInformation("Updated WooCommerce product {WooCommerceId} and refreshed the local row.", local.Id);

            return new SaveProductResult
            {
                Product = saved,
                RemoteSaved = true
            };
        }
        catch (WooCommerceApiException ex)
        {
            _logger.LogWarning(ex, "Website update failed for WooCommerceId {WooCommerceId}. Local changes were kept.", local.Id);
            return new SaveProductResult
            {
                Product = local,
                RemoteSaved = false,
                RemoteError = ex.UserMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Website update failed for WooCommerceId {WooCommerceId}. Local changes were kept.", local.Id);
            return new SaveProductResult
            {
                Product = local,
                RemoteSaved = false,
                RemoteError = UiStrings.UnableToUpdateKeptLocal
            };
        }
    }

    public async Task<DeleteProductResult> DeleteProductAsync(
        WooCommerceSettings settings,
        Product product,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(product);

        if (product.Id <= 0)
        {
            return new DeleteProductResult
            {
                Succeeded = false,
                Error = UiStrings.UnableToDeleteProduct
            };
        }

        try
        {
            _apiClient.ApplySettings(settings);
            try
            {
                await _apiClient
                    .DeleteAsync($"products/{product.Id}?force=true", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (WooCommerceApiException ex) when (ex.StatusCode == 404)
            {
                _logger.LogInformation(
                    "WooCommerce product {WooCommerceId} was already missing. Removing the local row.",
                    product.Id);
            }

            await _productRepository
                .DeleteAsync(product.LocalId, product.Id, cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Deleted WooCommerce product {WooCommerceId}.", product.Id);
            return new DeleteProductResult { Succeeded = true };
        }
        catch (WooCommerceApiException ex)
        {
            _logger.LogWarning(ex, "Deleting WooCommerce product {WooCommerceId} failed.", product.Id);
            return new DeleteProductResult
            {
                Succeeded = false,
                Error = ex.UserMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deleting WooCommerce product {WooCommerceId} failed unexpectedly.", product.Id);
            return new DeleteProductResult
            {
                Succeeded = false,
                Error = UiStrings.UnableToDeleteProduct
            };
        }
    }

    private async Task<List<ProductImage>?> TryGetRemoteImagesAsync(long productId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _apiClient
                .GetAsync($"products/{productId}", cancellationToken)
                .ConfigureAwait(false);
            var remote = JsonSerializer.Deserialize<Product>(response.Body, JsonOptions);
            return remote?.Images;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load existing images for WooCommerceId {WooCommerceId}. The featured image will still be updated.", productId);
            return null;
        }
    }
}
