using Microsoft.Win32;
using WooCommerceProductManager.Helpers;

namespace WooCommerceProductManager.Services;

public sealed class ImageFilePicker : IImageFilePicker
{
    public string? PickImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = UiStrings.ChooseProductImage,
            Filter = UiStrings.ImageFileFilter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
