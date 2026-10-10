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
        Nodes<ComboBox>(settingsWindow!).ToArray()[3].SelectedIndex = 1;
        var thresholdInput = Nodes<TextBox>(settingsWindow!).Single();
        var applyButton = Nodes<Button>(settingsWindow!).Single(b => (string?)b.Content == "Apply");
        thresholdInput.Text = "-5"; applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("invalid threshold rejected", preferences.WarningAmount == 1000);
        thresholdInput.Text = "1250.50"; applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("threshold control applies decimal amount", preferences.WarningAmount == 1250.50m);
        Nodes<ComboBox>(settingsWindow!).ToArray()[3].SelectedIndex = 0;
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
        preferences.WarningEnabled = false; ApplySettings(); Check("warning disabled hidden", WarningBox.Visibility == Visibility.Collapsed);
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
