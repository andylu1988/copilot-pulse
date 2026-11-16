using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CopilotPulse;

public partial class MainWindow
{
    private async Task ReviewDesktopAppearanceAsync(string directory, Action<bool, string> check)
    {
        preferences.HideAmounts = false; preferences.ReduceMotion = false; preferences.GrowthPulse = true;
        preferences.FreshnessAppearance = true; paused = false;
        string? originalSkin = preferences.SkinId;
        foreach (string skin in new[] { "matrix", "aurora", "terminal", "cyber" })
        {
            preferences.SkinId = skin; SkinTheme.Configure(preferences); ApplyPhotoBackground();
            SetDisplayMode("mini"); SetDisplayMode("compact"); UpdateLayout();
            check(FreshnessFrame.CornerRadius == Surface.CornerRadius && GrowthFrame.CornerRadius == Surface.CornerRadius,
                skin + " compact outline matches its skin immediately after leaving mini");
            check(FullBackdrop.CornerRadius == PhotoShade.CornerRadius && FullAtmosphere.Clip is RectangleGeometry clip
                && clip.RadiusX == PhotoShade.CornerRadius.TopLeft && clip.Rect.Size == FullAtmosphere.RenderSize,
                skin + " background and animation are clipped to the inner contour");
            SetDisplayMode("full");
            check(FreshnessFrame.CornerRadius == Surface.CornerRadius && GrowthFrame.CornerRadius == Surface.CornerRadius,
                skin + " full outline matches its surface without waiting for sampling");
        }
        preferences.SkinId = originalSkin; SkinTheme.Configure(preferences); ApplyPhotoBackground();
        SetDisplayMode("compact"); UpdateLayout();
        Capture(Path.Combine(directory, "compact-unified-outline-matrix.png"));
        var original = data!;
        data = new Consumption { Ok = true, Total = original.Total, FetchedAt = DateTimeOffset.UtcNow, Accounts = original.Accounts };
        pulseTime = null; pulseTotal = null;
        RefreshDesktopAppearance("live");
        check(freshnessState == "live" && FreshnessFrame.Opacity == .40, "Fresh reading uses a subtle skin-accent frame");
        int pulses = growthPulseCount;
        data.Total += 2; data.FetchedAt = data.FetchedAt.AddSeconds(1); RefreshDesktopAppearance("live");
        bool animations = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
        check(growthPulseCount == pulses + (animations ? 1 : 0), "A newer increasing source sample triggers one growth pulse when animations are allowed");
        RefreshDesktopAppearance("live");
        check(growthPulseCount == pulses + (animations ? 1 : 0), "Repeated rendering of the same source sample cannot pulse again");
        SetDisplayMode("mini"); SelectMiniMetric("today", false); await RefreshMiniHistoryAsync();
        check(growthPulseCount == pulses + (animations ? 1 : 0), "Switching mini metrics does not trigger a growth pulse");
        preferences.GrowthPulse = false; RefreshDesktopAppearance("live");
        check(!GrowthFrame.HasAnimatedProperties && GrowthFrame.Opacity == 0, "Disabling growth pulse cancels any active border animation");
        data.Total += 2; data.FetchedAt = data.FetchedAt.AddSeconds(1); RefreshDesktopAppearance("live");
        check(growthPulseCount == pulses + (animations ? 1 : 0), "Opted-out source growth stays still");
        preferences.GrowthPulse = true; preferences.HideAmounts = true;
        data.Total += 2; data.FetchedAt = data.FetchedAt.AddSeconds(1); RefreshDesktopAppearance("live");
        check(growthPulseCount == pulses + (animations ? 1 : 0), "Privacy mode suppresses growth pulse");
        preferences.HideAmounts = false;
        data.FetchedAt = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero); data.Total += 2;
        RefreshDesktopAppearance("live");
        check(growthPulseCount == pulses + (animations ? 1 : 0), "UTC quota month rollover does not trigger a growth pulse");
        data = original; pulseTime = null; pulseTotal = null;
        SelectMiniMetric("total", false); SelectMiniSecondaryMetric("today"); await RefreshMiniHistoryAsync();
        RefreshDesktopAppearance("stale"); await Task.Delay(80);
        check(FreshnessFrame.BorderThickness.Left == 2 && freshnessState == "stale" && MiniHost.ToolTip.ToString()!.Contains("最近成功"), "Delayed data is visible in the frame and explains last success on hover");
        Capture(Path.Combine(directory, "mini-dual-freshness-delayed.png"));
        preferences.FreshnessAppearance = false; RefreshDesktopAppearance("stale");
        check(FreshnessFrame.Opacity == 0, "Freshness frame can be switched off");
        preferences.FreshnessAppearance = true;
        paused = true; RefreshDesktopAppearance("live"); check(freshnessState == "paused", "Paused sampling has its own appearance"); paused = false;
        RefreshDesktopAppearance("network"); check(freshnessState == "network" && FreshnessFrame.BorderThickness.Left == 2, "Connection errors have a visible warning frame");
        RefreshDesktopAppearance("syncing"); check(freshnessState == "syncing", "Sampling has its own appearance");
        data = null;
        RefreshDesktopAppearance("login"); check(freshnessState == "login", "First-use sign-in errors remain visible without a previous reading");
        RefreshDesktopAppearance("network"); check(freshnessState == "network", "First-use network errors remain visible without a previous reading");
        RefreshDesktopAppearance("connecting"); check(freshnessState == "empty", "First-use connection waits for the initial reading");
        data = original;
        ApplyDesktopPreset("focus"); await RefreshMiniHistoryAsync();
        check(preferences.DisplayMode == "mini" && preferences.EdgeTuck && preferences.EdgeSnap && preferences.HideForFullscreen && preferences.MiniDualMetrics, "Focus preset enables mini dual metrics and desktop tuck");
        check(!HasActiveMeasurement && preferences.MiniHistoryRange == "today" && !ShowMiniTaskDelta, "Focus preset without a task uses today's delta rather than an unavailable task");
        ApplyDesktopPreset("quiet");
        check(preferences.ReduceMotion && !Topmost && !preferences.EdgeTuck && preferences.DisplayMode == "mini", "Quiet preset stops motion and topmost");
        ApplyDesktopPreset("sharing");
        check(preferences.HideAmounts && preferences.DisplayMode == "compact", "Sharing preset hides amounts on compact panel");
        ApplyDesktopPreset("glance"); await RefreshMiniHistoryAsync();
        check(!preferences.HideAmounts && preferences.MiniDualMetrics && preferences.MiniSecondaryMetric == "today" && preferences.DisplayMode == "compact", "Daily preset restores compact total plus today");
        SetDisplayMode("mini"); await Task.Delay(180);
        RefreshDesktopAppearance("live"); Capture(Path.Combine(directory, "mini-dual-total-today-matrix.png"));
        decimal totalBeforePreview = data.Total;
        PulseUsageChange(.12m, DateTimeOffset.Now, preview: true);
        await Task.Delay(300); Capture(Path.Combine(directory, "mini-growth-pulse-matrix.png"));
        check(data.Total == totalBeforePreview, "Effect preview does not modify usage data");
        check(!animations || (GrowthBadge.Visibility == Visibility.Visible && GrowthMeta.Text.Contains("不计入用量")), "Preview is visible and clearly labeled as unrecorded");
        for (int frame = 0; frame < 10; frame++)
        {
            Capture(Path.Combine(directory, $"growth-frame-{frame:00}.png"));
            await Task.Delay(240);
        }
        check(GrowthBadge.Visibility == Visibility.Collapsed, "Growth pulse finishes and releases its visible badge");
        var desktopMenu = BuildSettingsMenu().Items.OfType<MenuItem>().Single(i => i.Header?.ToString() == "桌面与收纳");
        check(desktopMenu.Items.OfType<MenuItem>().Count() == 5, "Grouped desktop menu exposes both tuck directions, center recovery, automatic tuck and settings");
        var command = StartupRegistration.Command(@"C:\Program Files\Copilot Pulse\CopilotPulse.exe");
        check(command == "\"C:\\Program Files\\Copilot Pulse\\CopilotPulse.exe\" --startup", "Startup command quotes paths with spaces");
        bool protectedRegistry = false; try { StartupRegistration.Set(true); } catch (InvalidOperationException) { protectedRegistry = true; }
        check(protectedRegistry, "Isolated screenshot/test process cannot change Windows startup registration");
        OpenPreferences(); settingsWindow!.SelectPageForReview("general"); await Task.Delay(100);
        CaptureControl(settingsWindow, Path.Combine(directory, "startup-setting-matrix.png"));
        settingsWindow.SelectPageForReview("appearance"); await Task.Delay(100);
        CaptureControl(settingsWindow, Path.Combine(directory, "appearance-settings-matrix.png"));
        settingsWindow.SelectPageForReview("measurement"); await Task.Delay(100);
        CaptureControl(settingsWindow, Path.Combine(directory, "measurement-settings-matrix.png"));
        settingsWindow.SelectPageForReview("desktop"); await Task.Delay(120);
        settingsWindow.VerifyFixedChromeForReview(); CaptureControl(settingsWindow, Path.Combine(directory, "desktop-presets-matrix.png"));
        settingsWindow.Close();
        OpenAbout(); await aboutWindow!.CheckLatestAsync(); await Task.Delay(100);
        check(Texts(aboutWindow).Any(t => t.Contains(PlatformRuntime.RuntimeBadge(false))), "About window reports runtime architecture badge");
        CaptureControl(aboutWindow, Path.Combine(directory, "about-arm64-runtime-matrix.png"));
        aboutWindow.Close();
        var updateMatrix = new UpdateWindow(preferences) { Owner = this }; updateMatrix.Show(); await Task.Delay(100);
        check(Texts(updateMatrix).Any(t => t.Contains(PlatformRuntime.RuntimeBadge(false))) && Texts(updateMatrix).Any(t => t.Contains("ARM64")), "Update window displays architecture matching guidance");
        CaptureControl(updateMatrix, Path.Combine(directory, "update-arm64-runtime-matrix.png"));
        updateMatrix.Close();
        OpenDiagnostics(); await Task.Delay(220);
        check(Texts(diagnosticsWindow!).Any(t => t.Contains("TieredPGO")), "Diagnostics window includes TieredPGO and power optimization profile");
        CaptureControl(diagnosticsWindow!, Path.Combine(directory, "diagnostics-arm64-runtime-matrix.png"));
        diagnosticsWindow!.Close();
        preferences.DesktopLocked = false; preferences.EdgeSnap = true; preferences.EdgeTuck = true; preferences.HideForFullscreen = false;
        Top = 180; SetClickThrough(true); TuckDesktopNow(false); await Task.Delay(120);
        check(!clickThrough && tucked && TuckedStrip.Visibility == Visibility.Visible, "Immediate tuck restores mouse interaction and exposes the strip");
        Capture(Path.Combine(directory, "edge-tuck-left-matrix.png"));
        RevealTucked(); DockDesktop(false); await Task.Delay(120);
        var area = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        check(Math.Abs(PointToScreen(new Point()).X - area.Left) < 2, "Dock-left action places window on native screen working area");
        double dockedLeft = Left; Left += 10; SnapDesktop();
        check(Math.Abs(Left - dockedLeft) < 2, "Dragging within snap distance aligns window to screen edge");
        desktopIdle = DateTimeOffset.UtcNow.AddSeconds(-4); DesktopTick(); await Task.Delay(80);
        check(tucked && Math.Abs(Left - (dockedLeft - ActualWidth + 24)) < 2, "Idle edge tuck leaves a 24 DIP strip");
        check(InputHitTest(new Point(ActualWidth - 16, 48)) != null, "Left tuck strip contains a hit-testable visible surface beyond transparent margin");
        RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = MouseEnterEvent });
        check(!tucked && Math.Abs(Left - dockedLeft) < 2, "Entering exposed strip restores full widget through hover event");
        DockDesktop(true); await Task.Delay(60); double dockedRight = Left;
        check(Math.Abs(PointToScreen(new Point()).X + ActualWidth * VisualTreeHelper.GetDpi(this).DpiScaleX - area.Right) < 2, "Dock-right action respects screen DPI");
        desktopIdle = DateTimeOffset.UtcNow.AddSeconds(-4); DesktopTick();
        check(tucked && InputHitTest(new Point(16, 48)) != null, "Right tuck also retains a hit-testable strip");
        RevealTucked(); preferences.DesktopLocked = true; desktopIdle = DateTimeOffset.UtcNow.AddSeconds(-4); DesktopTick();
        check(!tucked, "Locked desktop position prevents automatic tuck");
        preferences.DesktopLocked = false;
        DockDesktop(false); OpenPreferences(); desktopIdle = DateTimeOffset.UtcNow.AddSeconds(-4); DesktopTick();
        check(!tucked, "Open settings prevent automatic tuck"); settingsWindow!.Close();
        BeginDesktopDrag(); desktopIdle = DateTimeOffset.UtcNow.AddSeconds(-10); DesktopTick();
        check(!tucked && desktopDragging, "Tuck timer cannot hide the panel during a drag");
        CenterDesktop(); EndDesktopDrag(); double centerLeft = Left;
        preferences.EdgeTuck = true; desktopIdle = DateTimeOffset.UtcNow.AddSeconds(-10); DesktopTick();
        check(!tucked && Math.Abs(Left - centerLeft) < 1, "Dragging back to center never reuses a stale edge position");
        TuckDesktopNow(false);
        check(DesktopRoot.Clip is RectangleGeometry stripClip && stripClip.Rect.Width == 24, "Tucked content is clipped to the exposed strip on adjacent displays");
        Left = centerLeft; DesktopLocationChanged(); RevealTucked();
        check(!tucked && DesktopRoot.Clip == null && Math.Abs(Left - centerLeft) < 1, "External reposition clears tuck state, clipping and old restore position");
        TuckDesktopNow(true); CenterDesktop();
        check(!tucked && !preferences.EdgeTuck && !preferences.HideForFullscreen && IsVisible, "Center recovery disables automatic hiding and reveals the panel");
        preferences.EdgeTuck = false; Left = 350;
        check(Math.Abs(dockedLeft - dockedRight) > 100, "Left and right docking are distinct native positions");
    }
}
