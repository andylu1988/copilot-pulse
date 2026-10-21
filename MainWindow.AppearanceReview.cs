using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CopilotPulse;
public partial class MainWindow
{
    private async Task ReviewAppearanceAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        if (App.PerformanceReview) { await ReviewPerformanceAsync(directory); return; }
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); }
        Check(BrandLogo.Source is BitmapSource icon && icon.PixelWidth >= 256, "Brand icon uses high resolution frame");
        Check(BrandLogoSurface.Clip is RectangleGeometry rounded && rounded.RadiusX == 9 && rounded.RadiusY == 9 && rounded.Rect == new Rect(0, 0, 32, 32), "Brand glass must clip to its rounded inner edge");
        var opening = System.Diagnostics.Stopwatch.StartNew();
        preferences.SkinId = "matrix"; SkinEdited(); SetDisplayMode("compact");
        await Task.Delay(100); Capture(Path.Combine(directory, "widget-sharp-icon.png"));
        OpenHistory(this, new RoutedEventArgs()); await historyWindow!.ReloadAsync();
        await Task.Delay(100);
        File.AppendAllText(Path.Combine(directory, "window-performance.txt"), $"History first open and reload: {opening.Elapsed.TotalMilliseconds:0.0} ms\n");
        Check(GlassChrome.GetSource(historyWindow)?.Opacity == 1, "History backdrop must remain opaque");
        var bitmap = new RenderTargetBitmap((int)historyWindow.ActualWidth, (int)historyWindow.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(historyWindow);
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        int translucent = 0; for (int i = 3; i < pixels.Length; i += 4) if (pixels[i] > 20 && pixels[i] < 245) translucent++;
        Check(translucent < bitmap.PixelWidth * bitmap.PixelHeight / 20, "History rendered background must not expose desktop content");
        CaptureControl(historyWindow, Path.Combine(directory, "history-opaque.png"));
        opening.Restart(); OpenPreferences(); var original = settingsWindow!;
        OpenSkins(this, new RoutedEventArgs());
        Check(ReferenceEquals(original, settingsWindow) && Window.GetWindow(skinWindow) == original, "Skin shortcut must open existing Settings page");
        await Task.Delay(100); original.VerifyFixedChromeForReview();
        File.AppendAllText(Path.Combine(directory, "window-performance.txt"), $"Settings first open: {opening.Elapsed.TotalMilliseconds:0.0} ms\n");
        CaptureControl(original, Path.Combine(directory, "settings-skins-zh.png"));
        await ReviewSwitchPerformance(original, directory);
        skinWindow!.SetReviewEffects(.75, 1.25);
        Check(preferences.EffectIntensity == .75 && preferences.EffectSpeed == 1.25, "Embedded effect sliders did not apply");
        skinWindow.SelectForReview("midnight");
        Check(preferences.SkinId == "midnight" && SkinTheme.Current.Id == "midnight", "Skin selection not propagated");
        Check(GlassChrome.GetSource(historyWindow)?.Opacity == 1, "Skin change removed history opacity");
        skinWindow.SelectForReview("matrix");
        original.SelectPageForReview("general"); await Task.Delay(60);
        Check(!skinWindow.PreviewAnimationRunning, "Detached skin page kept animation running");
        OpenSkins(this, new RoutedEventArgs()); await Task.Delay(80);
        Check(skinWindow.PreviewAnimationRunning == (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast), "Skin preview failed to resume");
        original.Close();
        preferences.Language = "en"; UiText.Language = "en";
        OpenSkins(this, new RoutedEventArgs()); await Task.Delay(100);
        Check(skinWindow!.MiniModeButton.Content?.ToString() == "Mini number", "Embedded skin controls were not translated");
        settingsWindow!.VerifyFixedChromeForReview();
        CaptureControl(settingsWindow, Path.Combine(directory, "settings-skins-en.png"));
        settingsWindow.Close(); historyWindow.Close();
        File.WriteAllText(Path.Combine(directory,"appearance-checks.txt"), "PASS: high-resolution icon, rendered history alpha, shared Settings shortcut, bilingual skin gallery, live effects, skin changes, preview pause/resume, fixed close/footer.\n");
    }

    private async Task ReviewPerformanceAsync(string directory)
    {
        string log = Path.Combine(directory, "responsiveness.txt");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        double last = 0, maximumGap = 0; int ticks = 0;
        var heartbeat = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(20) };
        heartbeat.Tick += (_, _) => { double now = clock.Elapsed.TotalMilliseconds; maximumGap = Math.Max(maximumGap, now - last); last = now; ticks++; };
        heartbeat.Start();
        async Task Measure(string label, Action open, Func<Task>? reload = null)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            File.AppendAllText(log, $"Starting {label}\n");
            open();
            File.AppendAllText(log, $"{label} synchronous action: {watch.Elapsed.TotalMilliseconds:0.0} ms\n");
            if (reload != null) await reload();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            double openMs = watch.Elapsed.TotalMilliseconds;
            await Task.Delay(120);
            File.AppendAllText(log, $"{label}: ready {openMs:0.0} ms; including first frames {watch.Elapsed.TotalMilliseconds:0.0} ms\n");
        }
        try
        {
            for (int pass = 0; pass < 3; pass++)
            {
                await Measure($"History {pass + 1}", () => OpenHistory(this, new RoutedEventArgs()), () => historyWindow!.ReloadAsync());
                await Measure($"Close history {pass + 1}", () => historyWindow!.Close());
                await Measure($"Tasks {pass + 1}", () => OpenTasks(this, new RoutedEventArgs()), () => tasksWindow!.ReloadAsync());
                await Measure($"Close tasks {pass + 1}", () => tasksWindow!.Close());
                await Measure($"Forecast {pass + 1}", () => OpenInsights(1), () => insightsWindow!.ReloadAsync());
                await Measure($"Close forecast {pass + 1}", () => insightsWindow!.Close());
                await Measure($"Settings {pass + 1}", () => OpenPreferences());
                foreach (string page in new[] { "skins", "appearance", "warnings", "notifications", "shortcuts", "measurement", "updates", "general" })
                    await Measure($"Page {page} / {pass + 1}", () => settingsWindow!.SelectPageForReview(page));
                await Measure($"Close settings {pass + 1}", () => settingsWindow!.Close());
            }
            File.AppendAllText(log, $"Completed 48 open/page/close operations; UI heartbeat ticks {ticks}; maximum dispatch gap {maximumGap:0.0} ms. No screenshot capture during this test.\n");
        }
        finally { heartbeat.Stop(); }
    }

    private async Task ReviewSwitchPerformance(SettingsWindow settings, string directory)
    {
        var results = new System.Collections.Generic.List<string>();
        for (int round = 0; round < 2; round++)
        {
            var timings = new System.Collections.Generic.List<double>();
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            var cpu = process.TotalProcessorTime;
            for (int pass = 0; pass < 3; pass++)
                foreach (string page in new[] { "general", "skins", "appearance", "warnings", "notifications", "shortcuts", "measurement", "updates" })
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    settings.SelectPageForReview(page);
                    await settings.Dispatcher.InvokeAsync(() => settings.UpdateLayout(), System.Windows.Threading.DispatcherPriority.Render);
                    timings.Add(watch.Elapsed.TotalMilliseconds);
                }
            timings.Sort();
            results.Add($"Shared frosted background round {round + 1}: {timings.Count} page switches; median {timings[timings.Count / 2]:0.0} ms; p95 {timings[(int)(timings.Count * .95)]:0.0} ms; max {timings[^1]:0.0} ms; CPU {(process.TotalProcessorTime - cpu).TotalMilliseconds:0} ms");
        }
        settings.SelectPageForReview("skins");
        results.Add("Environment-specific UI-thread measurements, not a guarantee of compositor frame time.");
        File.WriteAllLines(Path.Combine(directory, "switch-performance.txt"), results);
    }
}
