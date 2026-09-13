using System.Text.Json.Serialization;

namespace WooCommerceProductManager.Models;

/// <summary>
/// Image object from the WooCommerce REST API product <c>images</c> array.
/// </summary>
public sealed class ProductImage
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("src")]
    public string? Src { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("alt")]
    public string? Alt { get; set; }
}
