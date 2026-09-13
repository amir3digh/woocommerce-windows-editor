using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;
using WooCommerceProductManager.Helpers;

namespace WooCommerceProductManager.Services;

public sealed class ProductImageCache : IProductImageCache, IDisposable
{
    private const int HashLength = 16;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _downloadGate = new(4);
    private readonly ILogger<ProductImageCache> _logger;
    private bool _disposed;

    public ProductImageCache(ILogger<ProductImageCache> logger)
    {
        _logger = logger;
        Directory.CreateDirectory(AppPaths.ImageCacheDirectory);

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WooCommerceProductManager/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("image/avif,image/webp,image/apng,image/*,*/*;q=0.8");
    }

    public string? GetExistingPath(long wooCommerceId, string? imageUrl)
    {
        if (LocalImagePath.TryGetFilePath(imageUrl, out var localFile))
        {
            return localFile;
        }

        if (!TryCreateCachePath(wooCommerceId, imageUrl, out var cachePath))
        {
            return null;
        }

        return File.Exists(cachePath) ? cachePath : null;
    }

    public async Task<string?> GetOrDownloadAsync(
        long wooCommerceId,
        string? imageUrl,
        bool forceRefresh,
        CancellationToken cancellationToken = default)
    {
        if (LocalImagePath.TryGetFilePath(imageUrl, out var localFile))
        {
            return localFile;
        }

        if (!TryCreateCachePath(wooCommerceId, imageUrl, out var cachePath)
            || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri)
            || (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!forceRefresh && File.Exists(cachePath))
        {
            return cachePath;
        }

        await _downloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && File.Exists(cachePath))
            {
                return cachePath;
            }

            using var response = await _httpClient
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            cachePath = WithExtension(cachePath, mediaType, uri);

            var tempPath = cachePath + ".tmp";
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            var length = new FileInfo(tempPath).Length;
            if (length is 0 or > LocalImagePath.MaxFileBytes)
            {
                File.Delete(tempPath);
                _logger.LogWarning("Skipped caching image for WooCommerceId {WooCommerceId} because the file was empty or too large.", wooCommerceId);
                return File.Exists(cachePath) ? cachePath : null;
            }

            RemoveOtherCacheFiles(wooCommerceId, cachePath);
            File.Copy(tempPath, cachePath, overwrite: true);
            File.Delete(tempPath);
            return cachePath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache product image. WooCommerceId: {WooCommerceId}.", wooCommerceId);
            return File.Exists(cachePath) ? cachePath : null;
        }
        finally
        {
            _downloadGate.Release();
        }
    }

    public string? StoreFromFile(long wooCommerceId, string? imageUrl, string localFilePath)
    {
        if (!File.Exists(localFilePath))
        {
            return null;
        }

        var key = string.IsNullOrWhiteSpace(imageUrl) ? localFilePath : imageUrl;
        if (!TryCreateCachePath(wooCommerceId, key, out var cachePath))
        {
            cachePath = Path.Combine(
                AppPaths.ImageCacheDirectory,
                $"{wooCommerceId}_local{Path.GetExtension(localFilePath)}");
        }

        cachePath = Path.ChangeExtension(cachePath, Path.GetExtension(localFilePath));
        Directory.CreateDirectory(AppPaths.ImageCacheDirectory);
        RemoveOtherCacheFiles(wooCommerceId, cachePath);
        File.Copy(localFilePath, cachePath, overwrite: true);
        return cachePath;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _downloadGate.Dispose();
        _httpClient.Dispose();
    }

    private static bool TryCreateCachePath(long wooCommerceId, string? imageUrl, out string cachePath)
    {
        cachePath = string.Empty;
        if (wooCommerceId <= 0 || string.IsNullOrWhiteSpace(imageUrl))
        {
            return false;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl.Trim())))[..HashLength];
        var extension = GuessExtension(imageUrl);
        cachePath = Path.Combine(AppPaths.ImageCacheDirectory, $"{wooCommerceId}_{hash}{extension}");
        return true;
    }

    private static string WithExtension(string cachePath, string? mediaType, Uri uri)
    {
        var extension = mediaType?.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => GuessExtension(uri.AbsolutePath)
        };

        return Path.ChangeExtension(cachePath, extension);
    }

    private static string GuessExtension(string value)
    {
        var extension = Path.GetExtension(value.Split('?', 2)[0]).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" ? extension : ".jpg";
    }

    private static void RemoveOtherCacheFiles(long wooCommerceId, string keepPath)
    {
        var prefix = $"{wooCommerceId}_";
        foreach (var file in Directory.EnumerateFiles(AppPaths.ImageCacheDirectory, prefix + "*"))
        {
            if (!string.Equals(file, keepPath, StringComparison.OrdinalIgnoreCase)
                && !file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
