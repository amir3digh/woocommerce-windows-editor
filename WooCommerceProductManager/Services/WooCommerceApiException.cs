namespace WooCommerceProductManager.Services;

/// <summary>
/// API failure with a user-facing message. Diagnostic details stay in logs, not in the UI.
/// </summary>
public sealed class WooCommerceApiException : Exception
{
    public WooCommerceApiException(string userMessage, int? statusCode = null, string? diagnosticMessage = null, Exception? innerException = null)
        : base(diagnosticMessage ?? userMessage, innerException)
    {
        UserMessage = userMessage;
        StatusCode = statusCode;
    }

    public string UserMessage { get; }

    public int? StatusCode { get; }
}
