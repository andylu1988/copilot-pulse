using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CopilotPulse;

// Shares the same cached backdrop and glass controls as the main panel.
internal sealed class UtilitySkin
{
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    private readonly Border surface = new(), shade = new();
    private readonly Atmosphere atmosphere = new() { IsHitTestVisible = false };
    internal UtilitySkin(Window window, UIElement content)
    {
        window.SetResourceReference(Window.BackgroundProperty, "SkinPanel");
        window.SetResourceReference(Control.ForegroundProperty, "SkinText");
        window.SetResourceReference(Control.FontFamilyProperty, "SkinFont");
        window.Resources.Add(typeof(Button), new Style(typeof(Button), (Style)window.FindResource("GlassButtonStyle")));
        var layers = new Grid { ClipToBounds = true };
        surface.Child = shade; shade.Child = atmosphere; layers.Children.Add(surface);
        layers.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(95, 5, 12, 18)), IsHitTestVisible = false });
        layers.Children.Add(content); window.Content = layers;
        GlassChrome.SetSource(window, surface);
        window.SizeChanged += (_, _) => Apply();
        window.Activated += (_, _) => Apply();
        ApplyTitleBar(window);
        Apply();
    }
    internal void Apply() { SkinTheme.ApplySurface(surface, shade, true); atmosphere.RefreshAppearance(); }
    internal static void ApplyTitleBar(Window window)
    {
        window.SourceInitialized += (_, _) => { int dark = 1; DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20, ref dark, sizeof(int)); };
    }
}
