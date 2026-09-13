using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;

namespace WooCommerceProductManager.Services;

/// <summary>
/// Loads settings from committed defaults, optional local development JSON, and
/// an encrypted per-user file under AppData. Never writes secrets into the project folder.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(IConfiguration configuration, ILogger<SettingsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public WooCommerceSettings Load()
    {
        var settings = new WooCommerceSettings();
        _configuration.GetSection(WooCommerceSettings.SectionName).Bind(settings);

        var local = TryReadLocalSettings();
        if (local is not null)
        {
            if (!string.IsNullOrWhiteSpace(local.StoreUrl))
            {
                settings.StoreUrl = local.StoreUrl;
            }

            if (!string.IsNullOrWhiteSpace(local.ConsumerKey))
            {
                settings.ConsumerKey = Unprotect(local.ConsumerKey, local.SecretsProtected);
            }

            if (!string.IsNullOrWhiteSpace(local.ConsumerSecret))
            {
                settings.ConsumerSecret = Unprotect(local.ConsumerSecret, local.SecretsProtected);
            }

            if (!string.IsNullOrWhiteSpace(local.WordPressUsername))
            {
                settings.WordPressUsername = Unprotect(local.WordPressUsername, local.SecretsProtected);
            }

            if (!string.IsNullOrWhiteSpace(local.ApplicationPassword))
            {
                settings.ApplicationPassword = Unprotect(local.ApplicationPassword, local.SecretsProtected);
            }
        }

        _logger.LogInformation(
            "Loaded WooCommerce settings. Store URL configured: {HasStoreUrl}. Credentials present: {HasCredentials}.",
            !string.IsNullOrWhiteSpace(settings.StoreUrl),
            settings.HasCredentials);

        return settings;
    }

    public void Save(WooCommerceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Directory.CreateDirectory(AppPaths.RootDirectory);

        var local = new LocalSettingsFile
        {
            StoreUrl = settings.StoreUrl.Trim(),
            ConsumerKey = Protect(settings.ConsumerKey),
            ConsumerSecret = Protect(settings.ConsumerSecret),
            WordPressUsername = Protect(settings.WordPressUsername),
            ApplicationPassword = Protect(settings.ApplicationPassword),
            SecretsProtected = true
        };

        var json = JsonSerializer.Serialize(local, JsonOptions);
        File.WriteAllText(AppPaths.LocalSettingsFile, json);

        _logger.LogInformation(
            "Saved WooCommerce settings to the local application data folder. Store URL configured: {HasStoreUrl}.",
            !string.IsNullOrWhiteSpace(settings.StoreUrl));
    }

    private LocalSettingsFile? TryReadLocalSettings()
    {
        if (!File.Exists(AppPaths.LocalSettingsFile))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(AppPaths.LocalSettingsFile);
            return JsonSerializer.Deserialize<LocalSettingsFile>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to read local settings file. Application defaults will be used.");
            return null;
        }
    }

    private static string Protect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        var protectedBytes = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    private string Unprotect(string value, bool secretsProtected)
    {
        if (!secretsProtected)
        {
            return value;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(value);
            var bytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to decrypt a stored credential. The value will be treated as empty.");
            return string.Empty;
        }
    }

    private sealed class LocalSettingsFile
    {
        public string StoreUrl { get; set; } = string.Empty;

        public string ConsumerKey { get; set; } = string.Empty;

        public string ConsumerSecret { get; set; } = string.Empty;

        public string WordPressUsername { get; set; } = string.Empty;

        public string ApplicationPassword { get; set; } = string.Empty;

        public bool SecretsProtected { get; set; }
    }
}
