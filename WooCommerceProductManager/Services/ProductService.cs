using System.Text.Json;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;
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
                "WooCommerce returned product data that could not be read.",
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
        string jsonBody,
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

        return await PushJsonToWebsiteAsync(settings, local, jsonBody, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SaveProductResult> RetryWebsiteSyncAsync(
        WooCommerceSettings settings,
        Product localProduct,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(localProduct);

        var jsonBody = ProductUpdatePayload.BuildFullJson(localProduct);
        return await PushJsonToWebsiteAsync(settings, localProduct, jsonBody, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SaveProductResult> PushJsonToWebsiteAsync(
        WooCommerceSettings settings,
        Product local,
        string jsonBody,
        CancellationToken cancellationToken)
    {
        try
        {
            _apiClient.ApplySettings(settings);
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
                    "WooCommerce saved the product but returned data that could not be read.",
                    diagnosticMessage: "Invalid product JSON after update.",
                    innerException: ex);
            }

            if (remote is null)
            {
                throw new WooCommerceApiException("WooCommerce did not return the updated product.");
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
                RemoteError = "Unable to update WooCommerce. Local changes were kept."
            };
        }
    }
}
