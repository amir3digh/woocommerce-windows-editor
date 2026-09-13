using System.Text.Json.Serialization;

namespace WooCommerceProductManager.Models;

/// <summary>
/// Product fields from the official WooCommerce REST API <c>GET /products</c> response.
/// Prices are strings, matching the API. Stock quantity may be null when stock is not managed.
/// </summary>
public sealed class Product
{
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

    [JsonIgnore]
    public long LocalId { get; set; }

    [JsonIgnore]
    public bool IsDirty { get; set; }

    [JsonIgnore]
    public string? FirstImageUrl =>
        Images?.FirstOrDefault(image => !string.IsNullOrWhiteSpace(image.Src))?.Src;

    [JsonIgnore]
    public bool HasImage => !string.IsNullOrWhiteSpace(FirstImageUrl);

    [JsonIgnore]
    public string DisplaySku => string.IsNullOrWhiteSpace(Sku) ? "—" : Sku;

    [JsonIgnore]
    public string DisplayPrice
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Price))
            {
                return Price;
            }

            return string.IsNullOrWhiteSpace(RegularPrice) ? "—" : RegularPrice;
        }
    }

    [JsonIgnore]
    public string DisplayStockQuantity
    {
        get
        {
            if (!ManageStock)
            {
                return "Not managed";
            }

            return StockQuantity is null ? "—" : StockQuantity.Value.ToString();
        }
    }

    [JsonIgnore]
    public string DisplayStockStatus => StockStatus switch
    {
        "instock" => "In stock",
        "outofstock" => "Out of stock",
        "onbackorder" => "On backorder",
        _ => string.IsNullOrWhiteSpace(StockStatus) ? "—" : StockStatus
    };
}
