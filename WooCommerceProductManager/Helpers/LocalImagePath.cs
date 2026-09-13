using System.IO;

namespace WooCommerceProductManager.Helpers;

public static class LocalImagePath
{
    public const long MaxFileBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".gif",
            ".webp"
        };

    public static bool TryGetFilePath(string? value, out string filePath)
    {
        filePath = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (File.Exists(value))
        {
            filePath = value;
            return true;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.IsFile
            && File.Exists(uri.LocalPath))
        {
            filePath = uri.LocalPath;
            return true;
        }

        return false;
    }

    public static string ToDisplayUrl(string filePath) => new Uri(filePath).AbsoluteUri;

    public static bool IsAllowedExtension(string filePath)
        => AllowedExtensions.Contains(Path.GetExtension(filePath));

    public static string? GetMimeType(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => null
    };
}
