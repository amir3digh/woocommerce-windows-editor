using System.Text.RegularExpressions;

namespace WooCommerceProductManager.Helpers;

/// <summary>
/// Removes credentials and authentication material from log messages.
/// </summary>
public static partial class SensitiveDataRedactor
{
    public static string Redact(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        var redacted = ConsumerKeyPattern().Replace(message, "ck_[REDACTED]");
        redacted = ConsumerSecretPattern().Replace(redacted, "cs_[REDACTED]");
        redacted = QueryParameterPattern().Replace(redacted, "$1=[REDACTED]");
        redacted = AuthorizationHeaderPattern().Replace(redacted, "Authorization: [REDACTED]");
        return redacted;
    }

    [GeneratedRegex(@"ck_[A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex ConsumerKeyPattern();

    [GeneratedRegex(@"cs_[A-Za-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex ConsumerSecretPattern();

    [GeneratedRegex(@"(?i)(consumer_key|consumer_secret|consumerKey|consumerSecret|Authorization)\s*[=:]\s*[^&\s]+")]
    private static partial Regex QueryParameterPattern();

    [GeneratedRegex(@"(?i)Authorization:\s*.+")]
    private static partial Regex AuthorizationHeaderPattern();
}
