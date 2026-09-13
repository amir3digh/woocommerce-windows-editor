namespace WooCommerceProductManager.Services;

public interface IConfirmDialog
{
    bool Confirm(string message, string caption);
}
