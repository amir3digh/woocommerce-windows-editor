using System.Windows;
using System.Windows.Controls;

namespace WooCommerceProductManager.Helpers;

public static class ImageLoadBehavior
{
    public static readonly DependencyProperty HideOnFailureProperty =
        DependencyProperty.RegisterAttached(
            "HideOnFailure",
            typeof(bool),
            typeof(ImageLoadBehavior),
            new PropertyMetadata(false, OnHideOnFailureChanged));

    public static bool GetHideOnFailure(DependencyObject obj) => (bool)obj.GetValue(HideOnFailureProperty);

    public static void SetHideOnFailure(DependencyObject obj, bool value) => obj.SetValue(HideOnFailureProperty, value);

    private static void OnHideOnFailureChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image)
        {
            return;
        }

        image.ImageFailed -= OnImageFailed;

        if ((bool)e.NewValue)
        {
            image.ImageFailed += OnImageFailed;
        }
    }

    private static void OnImageFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        if (sender is Image image)
        {
            image.Source = null;
            image.Visibility = Visibility.Collapsed;
        }
    }
}
