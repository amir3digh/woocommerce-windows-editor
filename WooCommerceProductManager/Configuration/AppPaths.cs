using System.IO;

namespace WooCommerceProductManager.Configuration;

/// <summary>
/// Local application data locations. User credentials are stored under AppData, not in Git.
/// </summary>
public static class AppPaths
{
    public static string RootDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WooCommerceProductManager");

    public static string LogsDirectory => Path.Combine(RootDirectory, "logs");

    public static string LocalSettingsFile => Path.Combine(RootDirectory, "settings.json");

    public static string DatabaseFile => Path.Combine(RootDirectory, "products.db");

    public static string ImageCacheDirectory => Path.Combine(RootDirectory, "image-cache");
}
