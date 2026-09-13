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
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

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
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WooCommerceProductManager/1.0");
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

    private void Configure(WooCommerceSettings settings)
    {
        if (!WooCommerceUrlValidator.TryValidateHttpsStoreUrl(settings.StoreUrl, out var normalizedUrl, out var error))
        {
            throw new WooCommerceApiException(error ?? "Store URL must use HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(settings.ConsumerKey) || string.IsNullOrWhiteSpace(settings.ConsumerSecret))
        {
            throw new WooCommerceApiException("Consumer Key and Consumer Secret are required.");
        }

        var baseAddress = new Uri(normalizedUrl, UriKind.Absolute);
        if (!string.Equals(baseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException("Store URL must use HTTPS.");
        }

        lock (_configurationSync)
        {
            _baseAddress = baseAddress;
            _consumerKey = settings.ConsumerKey.Trim();
            _consumerSecret = settings.ConsumerSecret;

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
                "The request to WooCommerce timed out. Please try again.",
                diagnosticMessage: "Request timed out.",
                innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP {Method} {Path} failed with a network error.", request.Method.Method, requestPath);
            throw new WooCommerceApiException(
                "Unable to connect to WooCommerce. Please check the store URL and API credentials.",
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
                    "The store URL redirected to another location. Confirm the HTTPS WooCommerce REST API URL.",
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
                    "The store URL did not return a WooCommerce REST API response. Confirm it ends with /wp-json/wc/v3/",
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
            throw new WooCommerceApiException("A request was created without a destination URL.");
        }

        string consumerKey;
        string consumerSecret;
        lock (_configurationSync)
        {
            consumerKey = _consumerKey;
            consumerSecret = _consumerSecret;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumerKey}:{consumerSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    private Uri CreateAllowedRequestUri(string relativePath)
    {
        Uri baseAddress;
        lock (_configurationSync)
        {
            if (_baseAddress is null)
            {
                throw new WooCommerceApiException("The WooCommerce client is not configured.");
            }

            baseAddress = _baseAddress;
        }

        if (!Uri.TryCreate(baseAddress, relativePath, out var requestUri))
        {
            throw new WooCommerceApiException("The WooCommerce request path is invalid.");
        }

        if (!string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException("Store URL must use HTTPS.");
        }

        if (!string.Equals(requestUri.Host, baseAddress.Host, StringComparison.OrdinalIgnoreCase)
            || requestUri.Port != baseAddress.Port)
        {
            throw new WooCommerceApiException("Requests can only be sent to the configured WooCommerce store URL.");
        }

        var configuredPath = baseAddress.AbsolutePath.TrimEnd('/');
        if (!string.IsNullOrEmpty(configuredPath)
            && !requestUri.AbsolutePath.StartsWith(configuredPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new WooCommerceApiException("Requests can only be sent to the configured WooCommerce store URL.");
        }

        return requestUri;
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
                "Unable to connect to WooCommerce. Please check the store URL and API credentials.",
            HttpStatusCode.Forbidden =>
                "WooCommerce denied this request. The API key may not have permission to access products.",
            HttpStatusCode.NotFound =>
                "The WooCommerce REST API was not found. Confirm the Store URL ends with /wp-json/wc/v3/",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
                "The WooCommerce store is unavailable. Please try again later.",
            _ when status >= 500 =>
                "The WooCommerce store is unavailable. Please try again later.",
            _ => string.IsNullOrWhiteSpace(apiError?.Message)
                ? "WooCommerce returned an error. Please check the store URL and API credentials."
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
}
