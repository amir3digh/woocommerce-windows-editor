using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WooCommerceProductManager.Helpers;
using WooCommerceProductManager.Models;
using WooCommerceProductManager.Services;

namespace WooCommerceProductManager.ViewModels;

public sealed record StockStatusChoice(string Value, string Display);

public partial class ProductEditViewModel : ObservableObject
{
    private readonly IImageFilePicker? _imageFilePicker;
    private readonly IProductImageCache? _imageCache;
    private string? _pendingImagePath;

    public ProductEditViewModel()
    {
    }

    public ProductEditViewModel(IImageFilePicker imageFilePicker, IProductImageCache imageCache)
    {
        _imageFilePicker = imageFilePicker;
        _imageCache = imageCache;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProduct))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(IsStockQuantityEnabled))]
    [NotifyPropertyChangedFor(nameof(PlaceholderText))]
    [NotifyPropertyChangedFor(nameof(EditorTitle))]
    private Product? _product;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _sku = string.Empty;

    [ObservableProperty]
    private string _regularPrice = string.Empty;

    [ObservableProperty]
    private string _salePrice = string.Empty;

    [ObservableProperty]
    private string _stockQuantity = string.Empty;

    [ObservableProperty]
    private string _stockStatus = "instock";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStockQuantityEnabled))]
    private bool _manageStock;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaceholderText))]
    private string? _imageUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    [NotifyPropertyChangedFor(nameof(IsStockQuantityEnabled))]
    private bool _isSaving;

    [ObservableProperty]
    private string? _validationMessage;

    [ObservableProperty]
    private string? _saveMessage;

    [ObservableProperty]
    private bool _isSaveSuccessful;

    public IReadOnlyList<StockStatusChoice> StockStatusChoices { get; } =
    [
        new("instock", UiStrings.InStock),
        new("outofstock", UiStrings.OutOfStock),
        new("onbackorder", UiStrings.OnBackorder)
    ];

    public bool HasProduct => Product is not null;

    public bool CanEdit => Product is not null && !IsSaving;

    public bool CanDelete => Product is { Id: > 0 } && !IsSaving;

    public bool IsStockQuantityEnabled => CanEdit && ManageStock;

    public string PlaceholderText => HasProduct ? UiStrings.NoImage : UiStrings.NoProductSelected;

    public string EditorTitle => Product is { Id: <= 0 } ? UiStrings.NewProduct : UiStrings.Product;

    public bool HasPendingImage => !string.IsNullOrWhiteSpace(_pendingImagePath);

    public Func<Task>? SaveAction { get; set; }

    public Func<Task>? DeleteAction { get; set; }

    public Action? CancelAction { get; set; }

    partial void OnProductChanged(Product? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ChooseImageCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsSavingChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        ChooseImageCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private bool CanSaveOrCancel() => CanEdit;

    [RelayCommand(CanExecute = nameof(CanSaveOrCancel))]
    private async Task SaveAsync()
    {
        if (SaveAction is not null)
        {
            await SaveAction().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveOrCancel))]
    private void Cancel()
    {
        CancelAction?.Invoke();
    }

    private bool CanExecuteDelete() => CanDelete;

    [RelayCommand(CanExecute = nameof(CanExecuteDelete))]
    private async Task DeleteAsync()
    {
        if (DeleteAction is not null)
        {
            await DeleteAction().ConfigureAwait(true);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveOrCancel))]
    private void ChooseImage()
    {
        if (_imageFilePicker is null)
        {
            return;
        }

        var path = _imageFilePicker.PickImage();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!LocalImagePath.IsAllowedExtension(path))
        {
            ValidationMessage = UiStrings.ImageTypeInvalid;
            return;
        }

        var length = new FileInfo(path).Length;
        if (length is 0 or > LocalImagePath.MaxFileBytes)
        {
            ValidationMessage = UiStrings.ImageTooLarge;
            return;
        }

        ValidationMessage = null;
        SaveMessage = null;
        _pendingImagePath = path;
        var cached = Product is not null
            ? _imageCache?.StoreFromFile(Product.Id, Product.FirstImageUrl, path)
            : null;
        if (Product is not null && !string.IsNullOrWhiteSpace(cached))
        {
            Product.CachedImagePath = cached;
        }

        ImageUrl = cached ?? LocalImagePath.ToDisplayUrl(path);
    }

    public void Load(Product? product)
    {
        Product = product;
        ValidationMessage = null;
        SaveMessage = null;
        IsSaveSuccessful = false;

        if (product is null)
        {
            Name = string.Empty;
            Sku = string.Empty;
            RegularPrice = string.Empty;
            SalePrice = string.Empty;
            StockQuantity = string.Empty;
            StockStatus = "instock";
            ManageStock = false;
            ImageUrl = null;
            _pendingImagePath = null;
            return;
        }

        Name = product.Name;
        Sku = product.Sku ?? string.Empty;
        RegularPrice = product.RegularPrice ?? string.Empty;
        SalePrice = product.SalePrice ?? string.Empty;
        StockQuantity = product.StockQuantity?.ToString() ?? string.Empty;
        StockStatus = string.IsNullOrWhiteSpace(product.StockStatus) ? "instock" : product.StockStatus;
        ManageStock = product.ManageStock;
        ImageUrl = product.DisplayImageUrl;
        _pendingImagePath = LocalImagePath.TryGetFilePath(product.FirstImageUrl, out var pendingPath)
            ? pendingPath
            : null;
    }

    public bool TryGetValidatedValues(out ProductEditValues? values)
    {
        values = null;
        ValidationMessage = null;

        if (Product is null)
        {
            ValidationMessage = UiStrings.SelectProductToEdit;
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationMessage = UiStrings.ProductNameRequired;
            return false;
        }

        if (!WooCommercePriceParser.TryParseNonNegative(RegularPrice, allowEmpty: false, out var regularPrice, out var priceError))
        {
            ValidationMessage = priceError;
            return false;
        }

        if (!WooCommercePriceParser.TryParseNonNegative(SalePrice, allowEmpty: true, out var salePrice, out var saleError))
        {
            ValidationMessage = saleError;
            return false;
        }

        int? stockQuantity = Product.StockQuantity;
        if (ManageStock)
        {
            if (string.IsNullOrWhiteSpace(StockQuantity))
            {
                ValidationMessage = UiStrings.StockQuantityRequired;
                return false;
            }

            if (!int.TryParse(StockQuantity.Trim(), out var parsedQuantity) || parsedQuantity < 0)
            {
                ValidationMessage = UiStrings.StockQuantityInvalid;
                return false;
            }

            stockQuantity = parsedQuantity;
        }

        if (string.IsNullOrWhiteSpace(StockStatus)
            || StockStatusChoices.All(choice => choice.Value != StockStatus))
        {
            ValidationMessage = UiStrings.SelectValidStockStatus;
            return false;
        }

        values = new ProductEditValues
        {
            Name = Name.Trim(),
            Sku = Sku.Trim(),
            RegularPrice = regularPrice,
            SalePrice = salePrice,
            StockQuantity = stockQuantity,
            StockStatus = StockStatus,
            ManageStock = ManageStock,
            LocalImagePath = _pendingImagePath
        };
        return true;
    }
}
