using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;
using WooCommerceProductManager.Helpers;

namespace WooCommerceProductManager.Services;

/// <summary>
/// Reusable WooCommerce REST API client. Credentials are applied per request after
/// the destination URL is confirmed to be the configured HTTPS store.
/// </summary>
public sealed class WooCommerceApiClient : IWooCommerceApiClient, IDisposable
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<WooCommerceApiClient> _logger;
    private readonly object _configurationSync = new();

    private Uri? _baseAddress;
    private string _consumerKey = string.Empty;
    private string _consumerSecret = string.Empty;
    private string _wordPressUsername = string.Empty;
    private string _applicationPassword = string.Empty;
    private bool _disposed;

    public WooCommerceApiClient(ILogger<WooCommerceApiClient> logger)
    {
        _logger = logger;

        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = RequestTimeout
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NarvanYadak/1.0");
    }

    public async Task TestConnectionAsync(WooCommerceSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Configure(settings);

        // Official WooCommerce REST API: GET /wp-json/wc/v3/products
        await GetAsync("products?per_page=1", cancellationToken).ConfigureAwait(false);
    }

    public void ApplySettings(WooCommerceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Configure(settings);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }

    public async Task<WooCommerceHttpResponse> GetAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, CreateAllowedRequestUri(relativePath));
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WooCommerceHttpResponse> PutAsync(string relativePath, string jsonBody, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, CreateAllowedRequestUri(relativePath));
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WooCommerceHttpResponse> PostAsync(string relativePath, string jsonBody, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, CreateAllowedRequestUri(relativePath));
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WooCommerceHttpResponse> DeleteAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, CreateAllowedRequestUri(relativePath));
        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WordPressMediaUpload> UploadMediaAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new WooCommerceApiException(UiStrings.ImageFileMissing);
        }

        if (!LocalImagePath.IsAllowedExtension(filePath))
        {
            throw new WooCommerceApiException(UiStrings.ImageTypeInvalid);
        }

        var mimeType = LocalImagePath.GetMimeType(filePath)
            ?? throw new WooCommerceApiException(UiStrings.ImageTypeInvalid);

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > LocalImagePath.MaxFileBytes)
        {
            throw new WooCommerceApiException(UiStrings.ImageTooLarge);
        }

        if (fileInfo.Length == 0)
        {
            throw new WooCommerceApiException(UiStrings.ImageFileMissing);
        }

        bool hasMediaCredentials;
        lock (_configurationSync)
        {
            hasMediaCredentials = !string.IsNullOrWhiteSpace(_wordPressUsername)
                && !string.IsNullOrWhiteSpace(_applicationPassword);
        }

        if (!hasMediaCredentials)
        {
            throw new WooCommerceApiException(UiStrings.ImageUploadNeedsApplicationPassword);
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var safeFileName = "product-image" + extension;

        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
            FileName = safeFileName
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, CreateMediaUploadUri())
        {
            Content = content
        };

        try
        {
            var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            var media = JsonSerializer.Deserialize<WordPressMediaResponse>(response.Body, JsonOptions);
            if (media is null || media.Id <= 0 || string.IsNullOrWhiteSpace(media.SourceUrl))
            {
                throw new WooCommerceApiException(UiStrings.ImageUploadFailed);
            }

            return new WordPressMediaUpload
            {
                Id = media.Id,
                SourceUrl = media.SourceUrl
            };
        }
        catch (WooCommerceApiException ex) when (ex.StatusCode is 401 or 403)
        {
            throw new WooCommerceApiException(UiStrings.ImageUploadForbidden, ex.StatusCode, ex.Message, ex);
        }
    }

    private void Configure(WooCommerceSettings settings)
    {
        if (!WooCommerceUrlValidator.TryValidateHttpsStoreUrl(settings.StoreUrl, out var normalizedUrl, out var error))
        {
            throw new WooCommerceApiException(error ?? UiStrings.StoreUrlMustBeHttps);
        }

        if (string.IsNullOrWhiteSpace(settings.ConsumerKey) || string.IsNullOrWhiteSpace(settings.ConsumerSecret))
        {
            throw new WooCommerceApiException(UiStrings.CredentialsRequired);
        }

        var baseAddress = new Uri(normalizedUrl, UriKind.Absolute);
        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException(UiStrings.StoreUrlMustBeHttps);
        }

        lock (_configurationSync)
        {
            _baseAddress = baseAddress;
            _consumerKey = settings.ConsumerKey.Trim();
            _consumerSecret = settings.ConsumerSecret;
            _wordPressUsername = settings.WordPressUsername.Trim();
            _applicationPassword = NormalizeApplicationPassword(settings.ApplicationPassword);

            // HttpClient.BaseAddress cannot be assigned after the first request has been sent.
            // Subsequent Configure calls (pagination, reload, test-then-load) must only update
            // the local _baseAddress used to build absolute request URIs.
            if (_httpClient.BaseAddress is null)
            {
                _httpClient.BaseAddress = baseAddress;
            }
        }
    }

    private async Task<WooCommerceHttpResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        ApplyAuthentication(request);

        var requestPath = GetLoggablePath(request.RequestUri);
        _logger.LogInformation("HTTP {Method} {Path}", request.Method.Method, requestPath);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "HTTP {Method} {Path} timed out.", request.Method.Method, requestPath);
            throw new WooCommerceApiException(
                UiStrings.RequestTimedOut,
                diagnosticMessage: "Request timed out.",
                innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP {Method} {Path} failed with a network error.", request.Method.Method, requestPath);
            throw new WooCommerceApiException(
                UiStrings.UnableToConnect,
                diagnosticMessage: "Network failure while calling WooCommerce.",
                innerException: ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "HTTP {Method} {Path} -> {StatusCode}",
                request.Method.Method,
                requestPath,
                (int)response.StatusCode);

            if ((int)response.StatusCode is >= 300 and < 400)
            {
                throw new WooCommerceApiException(
                    UiStrings.StoreUrlRedirected,
                    (int)response.StatusCode,
                    $"Unexpected redirect status {(int)response.StatusCode}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw CreateHttpException(response.StatusCode, body);
            }

            if (!IsJsonContent(response))
            {
                throw new WooCommerceApiException(
                    UiStrings.StoreUrlNotRestApi,
                    (int)response.StatusCode,
                    "Successful HTTP status but non-JSON content type.");
            }

            return new WooCommerceHttpResponse
            {
                Body = body,
                TotalItems = GetPositiveIntHeader(response.Headers, "X-WP-Total"),
                TotalPages = GetPositiveIntHeader(response.Headers, "X-WP-TotalPages")
            };
        }
    }

    private void ApplyAuthentication(HttpRequestMessage request)
    {
        if (request.RequestUri is null)
        {
            throw new WooCommerceApiException(UiStrings.RequestMissingUrl);
        }

        string userName;
        string password;
        lock (_configurationSync)
        {
            var isMediaUpload = request.RequestUri.AbsolutePath.EndsWith(
                "/wp-json/wp/v2/media",
                StringComparison.OrdinalIgnoreCase);
            if (isMediaUpload
                && !string.IsNullOrWhiteSpace(_wordPressUsername)
                && !string.IsNullOrWhiteSpace(_applicationPassword))
            {
                userName = _wordPressUsername;
                password = _applicationPassword;
            }
            else
            {
                userName = _consumerKey;
                password = _consumerSecret;
            }
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userName}:{password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    private Uri CreateAllowedRequestUri(string relativePath)
    {
        Uri baseAddress;
        lock (_configurationSync)
        {
            if (_baseAddress is null)
            {
                throw new WooCommerceApiException(UiStrings.ClientNotConfigured);
            }

            baseAddress = _baseAddress;
        }

        if (!Uri.TryCreate(baseAddress, relativePath, out var requestUri))
        {
            throw new WooCommerceApiException(UiStrings.RequestPathInvalid);
        }

        if (!string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException(UiStrings.StoreUrlMustBeHttps);
        }

        if (!string.Equals(requestUri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase)
            || requestUri.Port != baseAddress.Port)
        {
            throw new WooCommerceApiException(UiStrings.RequestsOnlyToConfiguredStore);
        }

        var configuredPath = baseAddress.AbsolutePath.TrimEnd('/');
        if (!string.IsNullOrEmpty(configuredPath)
            && !requestUri.AbsolutePath.StartsWith(configuredPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException(UiStrings.RequestsOnlyToConfiguredStore);
        }

        return requestUri;
    }

    private Uri CreateMediaUploadUri()
    {
        Uri baseAddress;
        lock (_configurationSync)
        {
            if (_baseAddress is null)
            {
                throw new WooCommerceApiException(UiStrings.ClientNotConfigured);
            }

            baseAddress = _baseAddress;
        }

        if (!Uri.TryCreate(baseAddress, "../../wp/v2/media", out var mediaUri))
        {
            throw new WooCommerceApiException(UiStrings.RequestPathInvalid);
        }

        if (!string.Equals(mediaUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException(UiStrings.StoreUrlMustBeHttps);
        }

        if (!string.Equals(mediaUri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase)
            || mediaUri.Port != baseAddress.Port)
        {
            throw new WooCommerceApiException(UiStrings.RequestsOnlyToConfiguredStore);
        }

        if (!mediaUri.AbsolutePath.EndsWith("/wp-json/wp/v2/media", StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException(UiStrings.RequestPathInvalid);
        }

        return mediaUri;
    }

    private WooCommerceApiException CreateHttpException(HttpStatusCode statusCode, string body)
    {
        var status = (int)statusCode;
        var apiError = TryReadApiError(body);
        if (apiError is not null)
        {
            _logger.LogWarning(
                "WooCommerce API error. Status: {StatusCode}. Code: {ErrorCode}.",
                status,
                apiError.Code);
        }

        var userMessage = statusCode switch
        {
            HttpStatusCode.Unauthorized =>
                UiStrings.UnableToConnect,
            HttpStatusCode.Forbidden =>
                UiStrings.Forbidden,
            HttpStatusCode.NotFound =>
                UiStrings.RestApiNotFound,
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                UiStrings.StoreUnavailable,
            _ when status >= 500 =>
                UiStrings.StoreUnavailable,
            _ => string.IsNullOrWhiteSpace(apiError?.Message)
                ? UiStrings.WooCommerceReturnedError
                : apiError.Message
        };

        return new WooCommerceApiException(userMessage, status, $"HTTP {status}. Code: {apiError?.Code}.");
    }

    private static WooCommerceErrorResponse? TryReadApiError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WooCommerceErrorResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsJsonContent(HttpResponseMessage response)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        return string.IsNullOrEmpty(mediaType)
               || mediaType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLoggablePath(Uri? uri)
    {
        if (uri is null)
        {
            return "(none)";
        }

        var path = string.IsNullOrEmpty(uri.Query)
            ? uri.AbsolutePath
            : uri.AbsolutePath + uri.Query;

        return SensitiveDataRedactor.Redact(path);
    }

    private static string NormalizeApplicationPassword(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : string.Concat(value.Where(character => !char.IsWhiteSpace(character)));

    private static int? GetPositiveIntHeader(System.Net.Http.Headers.HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();
        if (int.TryParse(raw, out var parsed) && parsed >= 0)
        {
            return parsed;
        }

        return null;
    }

    private sealed class WordPressMediaResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public long Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("source_url")]
        public string? SourceUrl { get; set; }
    }
}
