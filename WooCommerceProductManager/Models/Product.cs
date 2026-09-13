using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using WooCommerceProductManager.Helpers;

namespace WooCommerceProductManager.Models;

/// <summary>
/// Product fields from the official WooCommerce REST API <c>GET /products</c> response.
/// Prices are strings, matching the API. Stock quantity may be null when stock is not managed.
/// </summary>
public sealed class Product : INotifyPropertyChanged
{
    private string? _cachedImagePath;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("regular_price")]
    public string? RegularPrice { get; set; }

    [JsonPropertyName("sale_price")]
    public string? SalePrice { get; set; }

    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("manage_stock")]
    public bool ManageStock { get; set; }

    [JsonPropertyName("stock_quantity")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? StockQuantity { get; set; }

    [JsonPropertyName("stock_status")]
    public string? StockStatus { get; set; }

    [JsonPropertyName("images")]
    public List<ProductImage>? Images { get; set; }

    [JsonPropertyName("date_modified")]
    public string? DateModified { get; set; }

    [JsonPropertyName("permalink")]
    public string? Permalink { get; set; }

    [JsonIgnore]
    public long LocalId { get; set; }

    [JsonIgnore]
    public bool IsDirty { get; set; }

    [JsonIgnore]
    public string? CachedImagePath
    {
        get => _cachedImagePath;
        set
        {
            if (string.Equals(_cachedImagePath, value, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _cachedImagePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayImageUrl));
            OnPropertyChanged(nameof(HasImage));
        }
    }

    [JsonIgnore]
    public string? FirstImageUrl =>
        Images?.FirstOrDefault(image => !string.IsNullOrWhiteSpace(image.Src))?.Src;

    [JsonIgnore]
    public string? DisplayImageUrl
    {
        get
        {
            if (LocalImagePath.TryGetFilePath(FirstImageUrl, out var localFile))
            {
                return localFile;
            }

            if (!string.IsNullOrWhiteSpace(CachedImagePath) && File.Exists(CachedImagePath))
            {
                return CachedImagePath;
            }

            return null;
        }
    }

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrWhiteSpace(DisplayImageUrl);

    [JsonIgnore]
    public string DisplaySku => string.IsNullOrWhiteSpace(Sku) ? "—" : Sku;

    [JsonIgnore]
    public string DisplayPrice =>
        PersianPriceFormatter.FormatForDisplay(
            !string.IsNullOrWhiteSpace(Price) ? Price : RegularPrice);

    [JsonIgnore]
    public string DisplayStockQuantity
    {
        get
        {
            if (!ManageStock)
            {
                return UiStrings.NotManaged;
            }

            return StockQuantity is null ? "—" : StockQuantity.Value.ToString();
        }
    }

    [JsonIgnore]
    public string DisplayStockStatus => StockStatus switch
    {
        "instock" => UiStrings.InStock,
        "outofstock" => UiStrings.OutOfStock,
        "onbackorder" => UiStrings.OnBackorder,
        _ => string.IsNullOrWhiteSpace(StockStatus) ? "—" : StockStatus
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Product CreateDraft() => new()
    {
        Id = 0,
        LocalId = 0,
        Name = string.Empty,
        StockStatus = "instock",
        ManageStock = false,
        Images = []
    };

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
