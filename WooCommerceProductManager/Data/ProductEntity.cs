namespace WooCommerceProductManager.Data;

public sealed class ProductEntity
{
    public long LocalId { get; set; }

    public long WooCommerceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Sku { get; set; }

    public string? RegularPrice { get; set; }

    public string? SalePrice { get; set; }

    public string? Price { get; set; }

    public bool ManageStock { get; set; }

    public int? StockQuantity { get; set; }

    public string? StockStatus { get; set; }

    public string? ImageUrl { get; set; }

    public string? DateModified { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public bool IsDirty { get; set; }
}
