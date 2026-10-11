using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CopilotPulse;

public partial class MainWindow
{
    private async Task ReviewSettingsAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        void Check(string name, bool pass) { if (!pass) throw new InvalidOperationException(name); checks.Add(name); }
        OpenPreferences(); await Task.Delay(180);
        CaptureControl(settingsWindow!, Path.Combine(directory, "settings-zh.png"));
        Check("drag amount and background", WindowDrag.CanDrag(FullAmount) && WindowDrag.CanDrag(Surface));
        Check("buttons retain interaction", !WindowDrag.CanDrag(RefreshButton));
        Check("slider and text input retain interaction", !WindowDrag.CanDrag(new Slider()) && !WindowDrag.CanDrag(new TextBox()));
        Check("chart retains interaction", !WindowDrag.CanDrag(new HistoryChart()));
        OpenHistory(this, new RoutedEventArgs()); await historyWindow!.ReloadAsync();
        OpenSkins(this, new RoutedEventArgs());
        foreach (int fps in SettingsPolicy.FpsOptions)
        {
            preferences.AnimationFps = fps; ApplySettings();
            Check("full and mini FPS " + fps, Math.Abs(FullAmount.AnimationIntervalMs - 1000.0 / fps) < .01 && Math.Abs(MiniDisplay.AnimationIntervalMs - 1000.0 / fps) < .01);
        }
        Check("skin preview FPS", Math.Abs(skinWindow!.PreviewAnimationIntervalMs - 1000.0 / 60) < .01);
        foreach (int seconds in SettingsPolicy.RefreshOptions)
        {
            Nodes<ComboBox>(settingsWindow!).ToArray()[1].SelectedIndex = Array.IndexOf(SettingsPolicy.RefreshOptions, seconds);
            Check("refresh control " + seconds, preferences.RefreshSeconds == seconds && Math.Abs((nextRefresh - DateTimeOffset.Now).TotalSeconds - seconds) < 2);
        }
        Nodes<ComboBox>(settingsWindow!).ToArray()[0].SelectedIndex = 1;
        Check("language control applies English", preferences.Language == "en" && UiText.English);
        settingsWindow!.Close(); OpenPreferences(); await Task.Delay(180);
        Check("checkbox dark theme contrast", Nodes<CheckBox>(settingsWindow!).All(c => c.Foreground.ToString() == ((Brush)FindResource("SkinText")).ToString()));
        Nodes<ComboBox>(settingsWindow!).ToArray()[4].SelectedIndex = 1;
        var thresholdInput = Nodes<TextBox>(settingsWindow!).Single();
        var applyButton = Nodes<Button>(settingsWindow!).Single(b => (string?)b.Content == "Apply");
        thresholdInput.Text = "-5"; applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("invalid threshold rejected", preferences.WarningAmount == 1000);
        thresholdInput.Text = "1250.50"; applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("threshold control applies decimal amount", preferences.WarningAmount == 1250.50m);
        Nodes<ComboBox>(settingsWindow!).ToArray()[4].SelectedIndex = 0;
        CaptureControl(settingsWindow!, Path.Combine(directory, "settings-en.png"));
        Capture(Path.Combine(directory, "widget-en.png"));
        Check("english live status", StatusLabel.Text.Contains("Demo"));
        Check("english static label", Texts(this).Contains("Monthly internal usage"));
        OpenHistory(this, new RoutedEventArgs()); await historyWindow!.ReloadAsync(); await Task.Delay(140);
        CaptureControl(historyWindow, Path.Combine(directory, "history-en.png"));
        Check("history uses UTC and system zone", historyWindow.PeriodLabel.Text.Contains("UTC") && historyWindow.PeriodLabel.Text.Contains(TimeZoneInfo.Local.StandardName));
        preferences.Language = "zh-CN"; ApplySettings(); await Task.Delay(140);
        Check("language round trip", Texts(this).Contains("本月内部用量"));
        Check("history language round trip", historyWindow.TodayButton.Content.ToString() == "今天");
        preferences.WarningEnabled = true; preferences.WarningMode = "amount"; preferences.WarningAmount = 1000; ApplySettings();
        Check("warning visible", WarningBox.Visibility == Visibility.Visible);
        Capture(Path.Combine(directory, "widget-warning.png"));
        Check("warning title Chinese", WarningTitle.Text == "用量警告 · 已达到阈值");
        Check("mini warning badge active", MiniUsageProgress.Text.StartsWith("⚠ ") && MiniUsageProgress.FontWeight == FontWeights.Bold);
        Check("warning above history", WarningBox.TranslatePoint(new Point(0, 0), ContentStack).Y < HistoryNetGrowth.TranslatePoint(new Point(0, 0), ContentStack).Y);
        preferences.Language = "en"; ApplySettings();
        Check("warning title English", WarningTitle.Text == "Usage warning · Threshold reached");
        preferences.SkinId = "matrix"; ApplySettings(); SetDisplayMode("compact"); Capture(Path.Combine(directory, "warning-compact-matrix-en.png"));
        SetDisplayMode("full");
        preferences.Language = "zh-CN"; preferences.HistoryRange = "month"; ApplySettings();
        await RefreshMiniHistoryAsync();
        Check("demo month total and growth agree", demoHistory!.First().Total == 0 && demoHistory!.Last().Total == data!.Total && HistoryMath.NetChange(HistoryMath.Series(demoHistory!, null)) == data.Total);
        foreach (string skin in new[] { "matrix", "eink" })
        {
            preferences.SkinId = skin; ApplySettings(); await Task.Delay(160);
            Check("warning fixed contrast " + skin, WarningBox.Background.ToString() == "#FF382A12" && WarningText.Foreground.ToString() == "#FFFFF0CF");
            SetDisplayMode("full"); Capture(Path.Combine(directory, "warning-full-" + skin + ".png"));
            SetDisplayMode("compact"); Capture(Path.Combine(directory, "warning-compact-" + skin + ".png"));
            SetDisplayMode("mini"); Capture(Path.Combine(directory, "warning-mini-" + skin + ".png"));
        }
        SetDisplayMode("full"); preferences.SkinId = "midnight"; ApplySettings();

        preferences.WarningEnabled = false; ApplySettings(); Check("warning disabled hidden", WarningBox.Visibility == Visibility.Collapsed);
        Check("mini warning clears when disabled", !MiniUsageProgress.Text.StartsWith("⚠ ") && MiniUsageProgress.FontWeight == FontWeights.Normal);
        preferences.WarningEnabled = true; preferences.WarningMode = "amount"; preferences.WarningAmount = 2000; ApplySettings();
        Check("below-threshold progress remains visible", UsageProgress.Visibility == Visibility.Visible && UsageProgress.Text.Contains("61.73%") && WarningBox.Visibility == Visibility.Collapsed);
        Check("mini warning clears below threshold", !MiniUsageProgress.Text.StartsWith("⚠ "));
        Capture(Path.Combine(directory, "widget-threshold-progress.png"));
        preferences.SkinId = "matrix"; ApplySettings(); SetDisplayMode("compact"); Capture(Path.Combine(directory, "threshold-compact-matrix.png"));
        preferences.Language = "en"; ApplySettings(); Capture(Path.Combine(directory, "threshold-compact-matrix-en.png"));
        preferences.Language = "zh-CN"; preferences.SkinId = "midnight"; ApplySettings(); SetDisplayMode("full");
        foreach (string font in SettingsPolicy.NumberFonts)
        {
            preferences.NumberFont = font; ApplySettings(); await Task.Delay(40);
            Check("font option " + font, SkinTheme.Settings.NumberFont == font);
            SetDisplayMode("mini"); await Task.Delay(40);
            Capture(Path.Combine(directory, "font-" + font.Replace(" ", "-") + ".png"));
        }
        SetDisplayMode("full"); preferences.NumberFont = "skin";
        preferences.WarningMode = "percent"; ApplySettings();
        Check("quota percentage uses numeric limit", UsageProgress.Text.Contains("1.20%"));
        Check("unknown quota has no invented percentage", SettingsPolicy.QuotaPercent(new Account { Cost = 50, Percent = 75 }) == null);
        updateState = new AutoUpdateState { Repository = preferences.UpdateRepository, Version = "1.12.0", FirstSeenUtc = DateTimeOffset.UtcNow };
        RefreshUpdateBadge(); await Task.Delay(80);
        Check("new version button visible", NewVersionButton.Visibility == Visibility.Visible);
        var headerButtons = (StackPanel)NewVersionButton.Parent;
        Check("update notice directly before mini", headerButtons.Children.IndexOf(NewVersionButton) + 1 == headerButtons.Children.Cast<UIElement>().ToList().FindIndex(b => b is Button mini && mini.Content?.ToString() == "迷你"));
        Check("notice header fits without overlap", headerButtons.ActualWidth <= ContentStack.ActualWidth);
        Check("notice explains local restart deadline", NewVersionButton.ToolTip.ToString()!.Contains("自动安装并重启"));
        Capture(Path.Combine(directory, "widget-new-version.png"));
        preferences.Language = "en"; ApplySettings(); RefreshUpdateBadge();
        Check("notice English", NewVersionButton.Content.ToString() == "Update");
        Capture(Path.Combine(directory, "widget-new-version-en.png"));
        preferences.Language = "zh-CN"; ApplySettings(); RefreshUpdateBadge();
        OpenUpdates();
        Check("opening notice cancels this version auto install", updateState.Acknowledged);
        updateWindow!.Close();
        var updateReview = new UpdateWindow(preferences) { Owner = this }; updateReview.Show(); await Task.Delay(120);
        CaptureControl(updateReview, Path.Combine(directory, "online-update.png")); updateReview.Close();
        Check("About exists in three-dot menu", BuildSettingsMenu().Items.OfType<MenuItem>().Any(i => i.Header?.ToString() == "关于…"));
        bool acknowledgedBeforeAbout = updateState.Acknowledged;
        OpenAbout(); await aboutWindow!.CheckLatestAsync(); await Task.Delay(100);
        Check("About shows local and cloud versions", Texts(aboutWindow).Any(t => t.Contains(UpdatePolicy.Current.ToString())) && Texts(aboutWindow).Any(t => t.Contains("1.12.0")));
        Check("About preserves automatic update acknowledgement", updateState.Acknowledged == acknowledgedBeforeAbout);
        CaptureControl(aboutWindow, Path.Combine(directory, "about-zh.png")); aboutWindow.Close();
        preferences.Language = "en"; ApplySettings(); OpenAbout(); await aboutWindow!.CheckLatestAsync(); await Task.Delay(100);
        Check("About English", Texts(aboutWindow).Contains("Local version"));
        CaptureControl(aboutWindow, Path.Combine(directory, "about-en.png")); aboutWindow.Close();
        preferences.Language = "zh-CN"; ApplySettings();
        File.WriteAllText(Path.Combine(directory, "settings-review.json"), JsonSerializer.Serialize(new { passed = checks.Count, checks, zone = SystemClock.ZoneLabel(DateTimeOffset.Now) }, Store.Json));
        historyWindow.Close(); settingsWindow.Close(); skinWindow!.Close();
    }
    private static IEnumerable<T> Nodes<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T match) yield return match;
        if (node is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (T child in Nodes<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static IEnumerable<string> Texts(DependencyObject node)
    {
        if (node is TextBlock block) yield return block.Text;
        if (node is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (string text in Texts(VisualTreeHelper.GetChild(node, i))) yield return text;
    }
}
