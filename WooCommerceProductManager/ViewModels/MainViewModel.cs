using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;
using WooCommerceProductManager.Helpers;
using WooCommerceProductManager.Models;
using WooCommerceProductManager.Services;

namespace WooCommerceProductManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int SearchDebounceMilliseconds = 400;

    private readonly ISettingsService _settingsService;
    private readonly IWooCommerceApiClient _apiClient;
    private readonly IProductService _productService;
    private readonly ISyncService _syncService;
    private readonly ILogger<MainViewModel> _logger;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _searchDebounceCts;
    private int _loadGeneration;

    [ObservableProperty]
    private string _storeUrl = string.Empty;

    [ObservableProperty]
    private string _consumerKey = string.Empty;

    [ObservableProperty]
    private string _consumerSecret = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Configure your WooCommerce store to get started.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(LoadProductsCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    [NotifyCanExecuteChangedFor(nameof(SyncFromWebsiteCommand))]
    [NotifyCanExecuteChangedFor(nameof(RetrySyncCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSettingsExpanded = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _connectionResultMessage;

    [ObservableProperty]
    private bool _isLastConnectionSuccessful;

    [ObservableProperty]
    private int _syncDownloaded;

    [ObservableProperty]
    private int _syncUpdated;

    [ObservableProperty]
    private int _syncAdded;

    [ObservableProperty]
    private int _syncConflicts;

    [ObservableProperty]
    private int _syncErrors;

    [ObservableProperty]
    private string _syncStatusText = "Not synced yet. Use Sync from Website to download products.";

    public MainViewModel()
    {
        Products = new ProductListViewModel();
        ProductEdit = new ProductEditViewModel();
        Products.PropertyChanged += ProductsOnPropertyChanged;
        _settingsService = null!;
        _apiClient = null!;
        _productService = null!;
        _syncService = null!;
        _logger = null!;
    }

    public MainViewModel(
        ISettingsService settingsService,
        IWooCommerceApiClient apiClient,
        IProductService productService,
        ISyncService syncService,
        ILogger<MainViewModel> logger,
        ProductListViewModel productListViewModel,
        ProductEditViewModel productEditViewModel)
    {
        _settingsService = settingsService;
        _apiClient = apiClient;
        _productService = productService;
        _syncService = syncService;
        _logger = logger;
        Products = productListViewModel;
        ProductEdit = productEditViewModel;
        ProductEdit.SaveAction = ExecuteSaveProductAsync;
        ProductEdit.CancelAction = () => ProductEdit.Load(Products.SelectedProduct);
        Products.PropertyChanged += ProductsOnPropertyChanged;

        LoadSettings();
        _ = FetchProductsAsync(1);
    }

    public ProductListViewModel Products { get; }

    public ProductEditViewModel ProductEdit { get; }

    public bool IsIdle => !IsBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsIdle));

    partial void OnSearchTextChanged(string value)
    {
        if (_productService is null)
        {
            return;
        }

        _searchDebounceCts?.Cancel();
        _searchDebounceCts?.Dispose();
        _searchDebounceCts = new CancellationTokenSource();
        var token = _searchDebounceCts.Token;
        _ = DebouncedSearchAsync(token);
    }

    private bool CanSaveSettings() => IsIdle;

    private bool CanTestConnection() => IsIdle;

    private bool CanLoadProducts() => IsIdle;

    private bool CanSearch() => IsIdle;

    private bool CanGoPrevious() => IsIdle && Products.CanGoPrevious;

    private bool CanGoNext() => IsIdle && Products.CanGoNext;

    private bool CanSyncFromWebsite() => IsIdle;

    private bool CanRetrySync() => IsIdle;

    private void ProductsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ProductListViewModel.SelectedProduct) || ProductEdit.IsSaving)
        {
            return;
        }

        ProductEdit.Load(Products.SelectedProduct);
    }

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private void SaveSettings()
    {
        ErrorMessage = null;
        ConnectionResultMessage = null;
        IsLastConnectionSuccessful = false;

        if (!TryCreateSettings(out var settings, out var error))
        {
            ErrorMessage = error;
            StatusMessage = error ?? "Invalid store settings.";
            return;
        }

        StoreUrl = settings.StoreUrl;

        try
        {
            _settingsService.Save(settings);
            StatusMessage = "Settings saved on this computer.";
            _logger.LogInformation("User saved WooCommerce connection settings.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save WooCommerce settings.");
            ErrorMessage = "Unable to save settings. See the application log for details.";
            StatusMessage = ErrorMessage;
        }
    }

    [RelayCommand(CanExecute = nameof(CanTestConnection))]
    private async Task TestConnectionAsync()
    {
        ErrorMessage = null;
        ConnectionResultMessage = null;
        IsLastConnectionSuccessful = false;

        if (!TryCreateSettings(out var settings, out var error))
        {
            ErrorMessage = error;
            ConnectionResultMessage = error;
            StatusMessage = error ?? "Invalid store settings.";
            return;
        }

        StoreUrl = settings.StoreUrl;
        IsBusy = true;
        StatusMessage = "Testing connection...";

        try
        {
            await _apiClient.TestConnectionAsync(settings).ConfigureAwait(true);
            const string success = "Connected successfully";
            IsLastConnectionSuccessful = true;
            ConnectionResultMessage = success;
            ErrorMessage = null;
            StatusMessage = success;
            _logger.LogInformation("WooCommerce connection test succeeded.");
        }
        catch (WooCommerceApiException ex)
        {
            _logger.LogError(ex, "WooCommerce connection test failed. Status: {StatusCode}", ex.StatusCode);
            IsLastConnectionSuccessful = false;
            ConnectionResultMessage = ex.UserMessage;
            ErrorMessage = ex.UserMessage;
            StatusMessage = ex.UserMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WooCommerce connection test failed with an unexpected error.");
            const string message = "Unable to connect to WooCommerce. Please check the store URL and API credentials.";
            IsLastConnectionSuccessful = false;
            ConnectionResultMessage = message;
            ErrorMessage = message;
            StatusMessage = message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadProducts))]
    private async Task LoadProductsAsync()
    {
        await FetchProductsAsync(1).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task SearchAsync()
    {
        await FetchProductsAsync(1).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanGoPrevious))]
    private async Task PreviousPageAsync()
    {
        if (!Products.CanGoPrevious)
        {
            return;
        }

        await FetchProductsAsync(Products.CurrentPage - 1).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextPageAsync()
    {
        if (!Products.CanGoNext)
        {
            return;
        }

        await FetchProductsAsync(Products.CurrentPage + 1).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanSyncFromWebsite))]
    private async Task SyncFromWebsiteAsync()
    {
        if (!TryCreateSettings(out var settings, out var error))
        {
            ErrorMessage = error;
            StatusMessage = error ?? "Configure the store connection before syncing.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Syncing...";
        SyncStatusText = "Syncing...";
        NotifyPagingCommands();

        var progress = new Progress<SyncProgress>(update =>
        {
            SyncDownloaded = update.Downloaded;
            SyncUpdated = update.Updated;
            SyncAdded = update.Added;
            SyncConflicts = update.Conflicts;
            SyncErrors = update.Errors;
            if (!string.IsNullOrWhiteSpace(update.StatusMessage))
            {
                SyncStatusText = update.StatusMessage;
                StatusMessage = update.StatusMessage;
            }
        });

        try
        {
            var result = await _syncService
                .SyncFromWebsiteAsync(settings, progress)
                .ConfigureAwait(true);

            SyncDownloaded = result.Downloaded;
            SyncUpdated = result.Updated;
            SyncAdded = result.Added;
            SyncConflicts = result.Conflicts;
            SyncErrors = result.Errors;

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage ?? "Synchronization failed. Existing local products were kept.";
                StatusMessage = ErrorMessage;
                SyncStatusText = ErrorMessage;
            }
            else
            {
                StatusMessage =
                    $"Sync complete. Downloaded: {result.Downloaded}. Updated: {result.Updated}. Added: {result.Added}. Conflicts: {result.Conflicts}. Errors: {result.Errors}.";
                SyncStatusText = StatusMessage;
            }

            await FetchProductsAsync(1).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Synchronization was cancelled. Existing local products were kept.";
            SyncStatusText = StatusMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync from website failed.");
            ErrorMessage = "Synchronization failed. Existing local products were kept.";
            StatusMessage = ErrorMessage;
            SyncStatusText = ErrorMessage;
        }
        finally
        {
            IsBusy = false;
            NotifyPagingCommands();
        }
    }

    private async Task DebouncedSearchAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(SearchDebounceMilliseconds, token).ConfigureAwait(true);
            await FetchProductsAsync(1).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task FetchProductsAsync(int page)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        var loadId = Interlocked.Increment(ref _loadGeneration);

        IsBusy = true;
        Products.IsLoading = true;
        ErrorMessage = null;
        StatusMessage = "Loading products...";
        NotifyPagingCommands();

        try
        {
            var result = await _productService
                .GetProductsAsync(page, ProductService.DefaultPageSize, SearchText, token)
                .ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            Products.ApplyPage(result, Products.SelectedProduct?.LocalId);
            StatusMessage = result.TotalItems is 0
                ? "No local products yet. Use Sync from Website to download your catalog."
                : FormatLoadedMessage(result);
            _logger.LogInformation("Displayed {Count} local products on page {Page}.", result.Products.Count, result.Page);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Product load was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load local products. ExceptionType: {ExceptionType}. Message: {ExceptionMessage}.",
                ex.GetType().FullName,
                SensitiveDataRedactor.Redact(ex.Message));
            const string message = "Unable to load products from the local database.";
            ErrorMessage = message;
            StatusMessage = message;
        }
        finally
        {
            if (loadId == _loadGeneration)
            {
                IsBusy = false;
                Products.IsLoading = false;
                NotifyPagingCommands();
            }
        }
    }

    private static string FormatLoadedMessage(ProductListPage result)
    {
        if (result.TotalItems is int total)
        {
            return $"Loaded {result.Products.Count} local products (page {result.Page} of {result.TotalPages ?? result.Page}). {total} total.";
        }

        return $"Loaded {result.Products.Count} local products (page {result.Page}).";
    }

    private void NotifyPagingCommands()
    {
        PreviousPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        LoadProductsCommand.NotifyCanExecuteChanged();
        SearchCommand.NotifyCanExecuteChanged();
        SyncFromWebsiteCommand.NotifyCanExecuteChanged();
        RetrySyncCommand.NotifyCanExecuteChanged();
    }

    private async Task ExecuteSaveProductAsync()
    {
        ProductEdit.SaveMessage = null;
        ProductEdit.IsSaveSuccessful = false;

        if (!ProductEdit.TryGetValidatedValues(out var values) || values is null || ProductEdit.Product is null)
        {
            StatusMessage = ProductEdit.ValidationMessage ?? "The product could not be saved.";
            return;
        }

        var jsonBody = ProductUpdatePayload.TryBuildChangedJson(ProductEdit.Product, values);
        if (jsonBody is null)
        {
            ProductEdit.SaveMessage = "No changes to save.";
            ProductEdit.IsSaveSuccessful = true;
            StatusMessage = ProductEdit.SaveMessage;
            return;
        }

        if (!TryCreateSettings(out var settings, out var error))
        {
            ErrorMessage = error;
            ProductEdit.ValidationMessage = error;
            StatusMessage = error ?? "Configure the store connection before saving.";
            return;
        }

        var original = ProductEdit.Product;
        var edited = ProductUpdatePayload.ToLocalProduct(original, values);

        ProductEdit.IsSaving = true;
        IsBusy = true;
        StatusMessage = "Saving...";
        NotifyPagingCommands();

        try
        {
            var result = await _productService
                .SaveProductAsync(settings, original, edited, jsonBody)
                .ConfigureAwait(true);

            Products.ReplaceProduct(result.Product);
            ProductEdit.Load(result.Product);

            if (result.RemoteSaved)
            {
                ProductEdit.IsSaveSuccessful = true;
                ProductEdit.SaveMessage = "Saved to the local database and WooCommerce.";
                ErrorMessage = null;
                StatusMessage = ProductEdit.SaveMessage;
            }
            else
            {
                ProductEdit.IsSaveSuccessful = false;
                ProductEdit.SaveMessage =
                    "Saved locally. WooCommerce was not updated. Use Unsynced on the product row to retry.";
                ErrorMessage = result.RemoteError;
                StatusMessage = result.RemoteError ?? ProductEdit.SaveMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save product {WooCommerceId}.", original.Id);
            ProductEdit.IsSaveSuccessful = false;
            ProductEdit.SaveMessage = "Unable to save the product.";
            ErrorMessage = ProductEdit.SaveMessage;
            StatusMessage = ProductEdit.SaveMessage;
        }
        finally
        {
            ProductEdit.IsSaving = false;
            IsBusy = false;
            NotifyPagingCommands();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRetrySync))]
    private async Task RetrySyncAsync(Product? product)
    {
        product ??= Products.SelectedProduct;
        if (product is null)
        {
            return;
        }

        if (!TryCreateSettings(out var settings, out var error))
        {
            ErrorMessage = error;
            StatusMessage = error ?? "Configure the store connection before retrying.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Retrying website update...";
        NotifyPagingCommands();

        try
        {
            var result = await _productService
                .RetryWebsiteSyncAsync(settings, product)
                .ConfigureAwait(true);

            Products.ReplaceProduct(result.Product);
            if (ProductEdit.Product?.LocalId == result.Product.LocalId)
            {
                ProductEdit.Load(result.Product);
            }

            if (result.RemoteSaved)
            {
                ErrorMessage = null;
                StatusMessage = "Website update succeeded. The product is synced.";
                ProductEdit.IsSaveSuccessful = true;
                ProductEdit.SaveMessage = StatusMessage;
            }
            else
            {
                ErrorMessage = result.RemoteError;
                StatusMessage = result.RemoteError ?? "The website could not be updated. The local product is still unsynced.";
                ProductEdit.IsSaveSuccessful = false;
                ProductEdit.SaveMessage = StatusMessage;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retry sync failed for WooCommerceId {WooCommerceId}.", product.Id);
            ErrorMessage = "Unable to update the product on WooCommerce.";
            StatusMessage = ErrorMessage;
        }
        finally
        {
            IsBusy = false;
            NotifyPagingCommands();
        }
    }

    private bool TryCreateSettings(out WooCommerceSettings settings, out string? error)
    {
        settings = new WooCommerceSettings();

        if (!WooCommerceUrlValidator.TryValidateHttpsStoreUrl(StoreUrl, out var normalizedUrl, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ConsumerKey) || string.IsNullOrWhiteSpace(ConsumerSecret))
        {
            error = "Consumer Key and Consumer Secret are required.";
            return false;
        }

        settings = new WooCommerceSettings
        {
            StoreUrl = normalizedUrl,
            ConsumerKey = ConsumerKey.Trim(),
            ConsumerSecret = ConsumerSecret
        };
        error = null;
        return true;
    }

    private void LoadSettings()
    {
        try
        {
            var settings = _settingsService.Load();
            StoreUrl = settings.StoreUrl;
            ConsumerKey = settings.ConsumerKey;
            ConsumerSecret = settings.ConsumerSecret;
            IsSettingsExpanded = !settings.HasCredentials;
            StatusMessage = "Loading local products...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load WooCommerce settings.");
            ErrorMessage = "Unable to load saved settings.";
            StatusMessage = ErrorMessage;
        }
    }
}
