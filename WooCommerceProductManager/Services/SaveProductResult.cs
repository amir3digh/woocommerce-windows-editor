using WooCommerceProductManager.Models;

namespace WooCommerceProductManager.Services;

public sealed class SaveProductResult
{
    public required Product Product { get; init; }

    public bool RemoteSaved { get; init; }

    public string? RemoteError { get; init; }
}
