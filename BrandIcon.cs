using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CopilotPulse;

internal static class BrandIcon
{
    private static readonly Uri ResourceUri = new("pack://application:,,,/CopilotPulse;component/Assets/pulse-usage-v4-c.ico");
    private static readonly Lazy<ImageSource> WindowImage = new(() =>
    {
        var image = BitmapFrame.Create(ResourceUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        image.Freeze(); return image;
    });
    internal static ImageSource Window => WindowImage.Value;
    internal static System.Drawing.Icon CreateTray()
    {
        using var stream = Application.GetResourceStream(ResourceUri)!.Stream;
        using var icon = new System.Drawing.Icon(stream, new System.Drawing.Size(32, 32));
        return (System.Drawing.Icon)icon.Clone();
    }
}
