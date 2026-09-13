using System.Text.Json.Serialization;

namespace WooCommerceProductManager.Services;

internal sealed class WooCommerceErrorResponse
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
