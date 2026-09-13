using System.Windows;

namespace WooCommerceProductManager.Services;

public sealed class ConfirmDialog : IConfirmDialog
{
    public bool Confirm(string message, string caption)
    {
        var result = MessageBox.Show(
            message,
            caption,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        return result == MessageBoxResult.Yes;
    }
}
