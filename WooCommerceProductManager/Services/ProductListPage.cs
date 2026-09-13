using WooCommerceProductManager.Models;

namespace WooCommerceProductManager.Services;

public sealed class ProductListPage
{
    public required IReadOnlyList<Product> Products { get; init; }

    public int Page { get; init; }

    public int PerPage { get; init; }

    public int? TotalItems { get; init; }

    public int? TotalPages { get; init; }
}
