using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CopilotPulse;

public partial class MainWindow
{
    private SkinWindow? skinWindow;
    private readonly DispatcherTimer skinSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private void InitializeSkin()
    {
        SkinTheme.Configure(preferences);
        ApplyPhotoBackground();
        PinButton.Foreground = Brush(Topmost ? SkinTheme.Current.Accent : SkinTheme.Current.Faint);
        skinSaveTimer.Tick += (_, _) => { skinSaveTimer.Stop(); SavePreferences(); };
    }

    private void ApplyPhotoBackground()
    {
        ContentStack.Margin = SkinTheme.Current.Id == "matrix" ? new Thickness(28,22,28,22) : new Thickness(23,19,23,18);
        SkinTheme.ApplySurface(Surface, PhotoShade, false);
    }

    private void SkinEdited()
    {
        bool paletteChanged = SkinTheme.Current.Id != preferences.SkinId;
        SkinTheme.Configure(preferences);
        ApplyPhotoBackground();
        PinButton.Foreground = Brush(Topmost ? SkinTheme.Current.Accent : SkinTheme.Current.Faint);
        StatusDot.Fill = Brush(paused ? "#C7A370" : status == "live" ? "#66D9B8" : status == "syncing" ? "#79B8F7" : "#C7A370");
        MiniHistory.Accent = SkinTheme.Current.Accent;
        MiniHistory.InvalidateVisual();
        if (paletteChanged && data != null) RenderData(data);
        ApplyDisplayMode();
        RefreshDisplayEffects();
        historyWindow?.ApplySkin(paletteChanged);
        recordingWindow?.ApplySkin(paletteChanged);
        if (tray?.ContextMenuStrip is { } menu)
        {
            menu.BackColor = DarkTrayRenderer.Surface;
            menu.ForeColor = DarkTrayRenderer.Text;
            menu.Invalidate();
        }
        if (!App.Demo && !App.Smoke) { skinSaveTimer.Stop(); skinSaveTimer.Start(); }
    }

    private void SelectSkin(string id)
    {
        if (id == "custom" && !File.Exists(preferences.CustomBackgroundPath)) { OpenSkins(this, new RoutedEventArgs()); return; }
        preferences.SkinId = id;
        SkinEdited();
        skinWindow?.RefreshSelection();
    }

    private void OpenSkins(object sender, RoutedEventArgs e)
    {
        if (skinWindow != null) { skinWindow.Show(); skinWindow.Activate(); return; }
        skinWindow = new SkinWindow(preferences, SkinEdited) { Owner = this };
        skinWindow.Closed += (_, _) => { skinWindow = null; if (!App.Demo) SavePreferences(); };
        skinWindow.Show();
    }

    private MenuItem BuildSkinMenu()
    {
        var root = new MenuItem { Header = "换肤" };
        foreach (var skin in SkinTheme.Catalog)
        {
            var item = new MenuItem { Header = skin.Name, IsCheckable = true, IsChecked = preferences.SkinId == skin.Id };
            item.Click += (_, _) => SelectSkin(skin.Id);
            root.Items.Add(item);
        }
        root.Items.Add(new Separator());
        var advanced = new MenuItem { Header = "皮肤预览与图片设置…" }; advanced.Click += OpenSkins; root.Items.Add(advanced);
        return root;
    }

    // Compatibility for the existing isolated visual-review workflow.
    private void TogglePhotoBackground(object sender, RoutedEventArgs e)
    {
        preferences.SkinId = SkinTheme.Current.Photo ? "midnight" : "bayi";
        SkinEdited();
    }
}
