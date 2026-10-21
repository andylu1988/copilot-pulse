using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace CopilotPulse;

public partial class MainWindow
{
    // Development-only rendering of the actual controls, not a screenshot mockup.
    private async Task ReviewMenusAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var menu = BuildSettingsMenu();
        menu.Items.Add(new MenuItem { Header = "不可用状态 · 检查", IsEnabled = false });
        menu.PlacementTarget = PinButton;
        menu.IsOpen = true;
        await Task.Delay(250);
        CaptureControl(menu, Path.Combine(directory, "menu-normal.png"));
        var refresh = menu.Items.OfType<MenuItem>().First(i => (string)i.Header == "自动刷新间隔");
        refresh.IsSubmenuOpen = true;
        await Task.Delay(250);
        CaptureControl(menu, Path.Combine(directory, "menu-highlight.png"));
        var popup = refresh.Template.FindName("PART_Popup", refresh) as Popup;
        if (popup?.Child is FrameworkElement submenu) CaptureControl(submenu, Path.Combine(directory, "submenu.png"));
        refresh.IsSubmenuOpen = false;
        menu.IsOpen = false;

        using var trayMenu = BuildTrayMenu();
        trayMenu.Items.Add(new Forms.ToolStripMenuItem("不可用状态 · 检查") { Enabled = false });
        trayMenu.Show(100, 100);
        await Task.Delay(150);
        using var bitmap = new System.Drawing.Bitmap(trayMenu.Width, trayMenu.Height);
        trayMenu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(directory, "tray-normal.png"), System.Drawing.Imaging.ImageFormat.Png);
        trayMenu.Items[0].Select();
        trayMenu.Refresh();
        trayMenu.DrawToBitmap(bitmap, new System.Drawing.Rectangle(System.Drawing.Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(directory, "tray-highlight.png"), System.Drawing.Imaging.ImageFormat.Png);
        trayMenu.Hide();
    }

    private static void CaptureControl(FrameworkElement control, string file)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        control.UpdateLayout();
        var image = new RenderTargetBitmap((int)Math.Ceiling(control.ActualWidth * 2), (int)Math.Ceiling(control.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        image.Render(control);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(file);
        encoder.Save(output);
        File.AppendAllText(Path.Combine(Path.GetDirectoryName(file)!, "capture-performance.txt"), $"{Path.GetFileName(file)}: {watch.Elapsed.TotalMilliseconds:0.0} ms\n");
    }
}
