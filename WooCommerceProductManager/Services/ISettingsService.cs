using WooCommerceProductManager.Configuration;

namespace WooCommerceProductManager.Services;

public interface ISettingsService
{
    WooCommerceSettings Load();

    void Save(WooCommerceSettings settings);
}
