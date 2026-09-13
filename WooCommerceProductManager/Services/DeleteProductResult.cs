namespace WooCommerceProductManager.Services;

public sealed class DeleteProductResult
{
    public bool Succeeded { get; init; }

    public string? Error { get; init; }
}
