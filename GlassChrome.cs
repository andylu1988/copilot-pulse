using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;

namespace CopilotPulse;

// Sample only a separate decorative layer: never the button's own visual ancestor.
public static class GlassChrome
{
    internal static event Action<FrameworkElement>? SourceChanged;
    private static readonly ConditionalWeakTable<FrameworkElement, FrostedSurface> surfaces = new();
    internal static void Refresh(FrameworkElement source) { surfaces.Remove(source); if (source.ActualWidth > 0 && source.ActualHeight > 0) Frost(source); SourceChanged?.Invoke(source); }
    // One small, blurred background per window. A live VisualBrush/BlurEffect on
    // every button duplicates the entire animated surface during composition.
    internal static BitmapSource Frost(FrameworkElement source)
    {
        var cached = surfaces.GetValue(source, _ => new FrostedSurface());
        var size = source.RenderSize;
        if (cached.Image != null && cached.Size == size) return cached.Image;
        int w = Math.Max(1, (int)Math.Ceiling(size.Width / 8));
        int h = Math.Max(1, (int)Math.Ceiling(size.Height / 8));
        // Never call RenderTargetBitmap.Render during layout or OnRender: it
        // synchronously waits for WPF composition and can stall opening a window.
        // These surfaces contain only an image/gradient plus a tint, so sample
        // those brushes directly into a small CPU bitmap instead.
        var pixels = new byte[w * h * 4];
        if (source is System.Windows.Controls.Border border)
        {
            Paint(border.Background, pixels, w, h);
            if (border.Child is System.Windows.Controls.Border shade) Paint(shade.Background, pixels, w, h);
        }
        var horizontal = new byte[pixels.Length]; var blurred = new byte[pixels.Length];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int c = 0; c < 4; c++)
        {
            int sum = 0; for (int n = -2; n <= 2; n++) sum += pixels[(y * w + Math.Clamp(x + n, 0, w - 1)) * 4 + c];
            horizontal[(y * w + x) * 4 + c] = (byte)(sum / 5);
        }
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) for (int c = 0; c < 4; c++)
        {
            int sum = 0; for (int n = -2; n <= 2; n++) sum += horizontal[(Math.Clamp(y + n, 0, h - 1) * w + x) * 4 + c];
            blurred[(y * w + x) * 4 + c] = (byte)(sum / 5);
        }
        cached.Image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, blurred, w * 4);
        cached.Image.Freeze(); cached.Size = size; return cached.Image;
    }
    private sealed class ImagePixels
    {
        internal readonly byte[] Bytes;
        internal readonly int Width, Height;
        internal ImagePixels(BitmapSource image)
        {
            var converted = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
            Width = converted.PixelWidth; Height = converted.PixelHeight;
            Bytes = new byte[Width * Height * 4]; converted.CopyPixels(Bytes, Width * 4, 0);
        }
    }
    private static readonly ConditionalWeakTable<BitmapSource, ImagePixels> imagePixels = new();
    private static void Paint(Brush? brush, byte[] pixels, int width, int height)
    {
        if (brush == null || brush.Opacity <= 0) return;
        var picture = brush as ImageBrush;
        var image = picture?.ImageSource is BitmapSource bitmap ? imagePixels.GetValue(bitmap, b => new ImagePixels(b)) : null;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            double u = (x + .5) / width, v = (y + .5) / height;
            double a = 0, r = 0, g = 0, b = 0;
            if (image != null && picture != null)
            {
                var box = picture.Viewbox;
                double sx = box.X + u * box.Width, sy = box.Y + v * box.Height;
                if (picture.ViewboxUnits == BrushMappingMode.RelativeToBoundingBox) { sx *= image.Width; sy *= image.Height; }
                int index = (Math.Clamp((int)sy, 0, image.Height - 1) * image.Width + Math.Clamp((int)sx, 0, image.Width - 1)) * 4;
                b = image.Bytes[index]; g = image.Bytes[index + 1]; r = image.Bytes[index + 2]; a = image.Bytes[index + 3];
            }
            else
            {
                Color color = Colors.Transparent;
                if (brush is SolidColorBrush solid) color = solid.Color;
                else if (brush is LinearGradientBrush gradient && gradient.GradientStops.Count > 0)
                {
                    var start = gradient.StartPoint; var end = gradient.EndPoint;
                    double dx = end.X - start.X, dy = end.Y - start.Y;
                    double t = Math.Clamp(((u - start.X) * dx + (v - start.Y) * dy) / Math.Max(.00001, dx * dx + dy * dy), 0, 1);
                    var left = gradient.GradientStops[0]; var right = left;
                    foreach (var stop in gradient.GradientStops) { right = stop; if (stop.Offset >= t) break; left = stop; }
                    double blend = right.Offset > left.Offset ? Math.Clamp((t - left.Offset) / (right.Offset - left.Offset), 0, 1) : 0;
                    byte Mix(byte first, byte second) => (byte)Math.Round(first + (second - first) * blend);
                    color = Color.FromArgb(Mix(left.Color.A, right.Color.A), Mix(left.Color.R, right.Color.R), Mix(left.Color.G, right.Color.G), Mix(left.Color.B, right.Color.B));
                }
                a = color.A; r = color.R * a / 255; g = color.G * a / 255; b = color.B * a / 255;
            }
            double opacity = brush.Opacity, remaining = 1 - a * opacity / 255;
            int at = (y * width + x) * 4;
            pixels[at] = (byte)Math.Clamp(Math.Round(b * opacity + pixels[at] * remaining), 0, 255);
            pixels[at + 1] = (byte)Math.Clamp(Math.Round(g * opacity + pixels[at + 1] * remaining), 0, 255);
            pixels[at + 2] = (byte)Math.Clamp(Math.Round(r * opacity + pixels[at + 2] * remaining), 0, 255);
            pixels[at + 3] = (byte)Math.Clamp(Math.Round(a * opacity + pixels[at + 3] * remaining), 0, 255);
        }
    }
    private sealed class FrostedSurface { internal Size Size; internal BitmapSource? Image; }
    public static readonly DependencyProperty SourceProperty = DependencyProperty.RegisterAttached("Source", typeof(FrameworkElement), typeof(GlassChrome));
    public static void SetSource(DependencyObject target, FrameworkElement value) => target.SetValue(SourceProperty, value);
    public static FrameworkElement? GetSource(DependencyObject target) => target.GetValue(SourceProperty) as FrameworkElement;
}

public sealed class GlassBackdrop : FrameworkElement
{
    private Point? lastPosition;
    private Brush? lastBackground, lastShade;
    public GlassBackdrop()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => { LayoutUpdated += TrackPosition; GlassChrome.SourceChanged += SourceChanged; InvalidateVisual(); };
        Unloaded += (_, _) => { LayoutUpdated -= TrackPosition; GlassChrome.SourceChanged -= SourceChanged; lastPosition = null; };
        SizeChanged += (_, _) => InvalidateVisual();
    }
    private void SourceChanged(FrameworkElement source)
    {
        var window = Window.GetWindow(this);
        if (window != null && ReferenceEquals(GlassChrome.GetSource(window), source)) InvalidateVisual();
    }
    private void TrackPosition(object? sender, EventArgs args)
    {
        var window = Window.GetWindow(this);
        var source = window == null ? null : GlassChrome.GetSource(window);
        if (source == null || !source.IsVisible) return;
        var position = TranslatePoint(new Point(), source);
        var background = (source as System.Windows.Controls.Border)?.Background;
        var shade = ((source as System.Windows.Controls.Border)?.Child as System.Windows.Controls.Border)?.Background;
        if (lastPosition != position || !ReferenceEquals(lastBackground, background) || !ReferenceEquals(lastShade, shade))
        { lastPosition = position; lastBackground = background; lastShade = shade; InvalidateVisual(); }
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var window = Window.GetWindow(this);
        var source = window == null ? null : GlassChrome.GetSource(window);
        if (source == null || !source.IsVisible) return;
        var origin = TranslatePoint(new Point(), source);
        // Frost the decorative image, not a live copy of the entire animated visual tree
        // for every button. Matrix rain continues to animate once on the window itself.
        if (source.ActualWidth <= 0 || source.ActualHeight <= 0) return;
        var brush = new ImageBrush(GlassChrome.Frost(source)) { ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = new Rect(origin.X / source.ActualWidth, origin.Y / source.ActualHeight, ActualWidth / source.ActualWidth, ActualHeight / source.ActualHeight), Stretch = Stretch.Fill,
            // Blend the cached frost with the original background so animated rain
            // remains visible without capturing or blurring it a second time.
            Opacity = 0.30 };
        brush.Freeze();
        dc.DrawRectangle(brush, null, new Rect(RenderSize));
    }
}
