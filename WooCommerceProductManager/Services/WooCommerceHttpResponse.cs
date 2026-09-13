namespace WooCommerceProductManager.Services;

public sealed class WooCommerceHttpResponse
{
    public required string Body { get; init; }

    public int? TotalItems { get; init; }

    public int? TotalPages { get; init; }
}
