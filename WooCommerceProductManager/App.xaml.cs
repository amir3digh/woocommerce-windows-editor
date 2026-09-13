using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WooCommerceProductManager.Configuration;
using WooCommerceProductManager.Data;
using WooCommerceProductManager.Helpers;
using WooCommerceProductManager.Repositories;
using WooCommerceProductManager.Services;
using WooCommerceProductManager.ViewModels;
using WooCommerceProductManager.Views;

namespace WooCommerceProductManager;

public partial class App : Application
{
    private IServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(AppPaths.RootDirectory);
        Directory.CreateDirectory(AppPaths.LogsDirectory);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<WooCommerceSettings>(configuration.GetSection(WooCommerceSettings.SectionName));
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(AppPaths.LogsDirectory));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={AppPaths.DatabaseFile}"));
        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<WooCommerceApiClient>();
        services.AddSingleton<IWooCommerceApiClient>(sp => sp.GetRequiredService<WooCommerceApiClient>());
        services.AddSingleton<IProductService, ProductService>();
        services.AddSingleton<ISyncService, SyncService>();
        services.AddSingleton<ProductListViewModel>();
        services.AddTransient<ProductEditViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        _services = services.BuildServiceProvider();

        var logger = _services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("WooCommerce Product Manager starting.");

        try
        {
            var dbFactory = _services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await DatabaseInitializer.InitializeAsync(dbFactory, logger).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize the local SQLite database.");
            MessageBox.Show(
                "The local product database could not be initialized. You can still open the application, but product data may be unavailable.",
                "WooCommerce Product Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        DispatcherUnhandledException += (_, args) =>
        {
            logger.LogError(args.Exception, "Unhandled UI exception.");
            MessageBox.Show(
                "An unexpected error occurred. Details were written to the application log.",
                "WooCommerce Product Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var window = _services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}
