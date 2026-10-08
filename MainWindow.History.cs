using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CopilotPulse;

public partial class MainWindow
{
    private HistoryRepository? history;
    private HistoryWindow? historyWindow;
    private IReadOnlyList<HistoryFrame>? demoHistory;
    private int miniRevision;

    private void InitializeHistory()
    {
        if (App.Demo) { demoHistory = CreateDemoHistory(); _ = RefreshMiniHistoryAsync(); return; }
        if (App.Smoke) return;
        history = new HistoryRepository(Path.Combine(App.DataDirectory, "history"));
        history.Changed += HistoryChanged;
        _ = RefreshMiniHistoryAsync();
    }

    private void RecordHistory(Consumption value)
    {
        if (history == null) return;
        history.Record(value, preferences.RefreshSeconds);
        if (history.LastError != null) HistorySummary.Text = history.LastError;
    }

    private async void HistoryChanged() => await RefreshMiniHistoryAsync();
    private async Task RefreshMiniHistoryAsync()
    {
        int request = ++miniRevision;
        try
        {
            var frames = demoHistory ?? (history == null ? Array.Empty<HistoryFrame>() : await Task.Run(() => history.ReadRange(HistoryRanges.Start(preferences.HistoryRange, DateTimeOffset.Now), DateTimeOffset.Now)));
            if (closing || request != miniRevision) return;
            MiniHistory.SetData(HistoryMath.Series(frames, null));
            HistorySummary.Text = App.Demo ? "示例趋势 · 点击展开" : history?.LastError ?? $"{HistoryRanges.Label(preferences.HistoryRange)} · {frames.Count:N0} 次记录 · 点击展开";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { HistorySummary.Text = "历史暂不可读 · 点击查看"; }
    }

    private void HistoryViewChanged() { SavePreferences(); _ = RefreshMiniHistoryAsync(); }

    private void OpenHistory(object sender, RoutedEventArgs e)
    {
        if (historyWindow != null) { historyWindow.Show(); historyWindow.Activate(); return; }
        if (history == null && !App.Demo) return;
        historyWindow = new HistoryWindow(history, demoHistory, preferences, HistoryViewChanged) { Owner = this };
        historyWindow.Closed += (_, _) => historyWindow = null;
        historyWindow.Show();
    }

    private static IReadOnlyList<HistoryFrame> CreateDemoHistory()
    {
        var result = new List<HistoryFrame>();
        DateTimeOffset now = DateTimeOffset.Now;
        decimal first = 1000m, second = 20m;
        for (int i = 0; i < 100; i++)
        {
            // Synthetic QA data stays in memory and is labelled on every preview.
            if (i == 48 || i == 49) continue;
            first += i % 8 == 0 ? 0.72m : i % 3 == 0 ? 0.14m : 0.03m;
            second += i % 13 == 0 ? 0.06m : 0;
            if (i == 76) first -= 0.21m;
            DateTimeOffset time = now.AddMinutes((i - 99) * 3);
            result.Add(new HistoryFrame { Time = time, Period = time.ToString("yyyy-MM"), Cadence = 60, Total = first + second, Accounts = new() {
                new HistoryAccount { Name = "Example Enterprise", Login = "demo-emu", Cost = first },
                new HistoryAccount { Name = "Example Organization", Login = "demo-user", Cost = second }
            } });
        }
        return result;
    }

    private async Task ReviewHistoryAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var window = new HistoryWindow(null, demoHistory) { Width = 960, Height = 680 };
        window.Show();
        await window.ReloadAsync();
        await Task.Delay(250);
        CaptureControl(window, Path.Combine(directory, "history-cumulative.png"));
        window.HoverForReview();
        await Task.Delay(100);
        CaptureControl(window, Path.Combine(directory, "history-hover.png"));
        window.ShowChangesForReview();
        await Task.Delay(150);
        CaptureControl(window, Path.Combine(directory, "history-changes.png"));
        window.Close();
        Capture(Path.Combine(directory, "widget-history.png"));
        DetailsPanel.Visibility = Visibility.Collapsed;
        await Task.Delay(180);
        Capture(Path.Combine(directory, "widget-compact.png"));
        DetailsPanel.Visibility = Visibility.Visible;
        TogglePhotoBackground(this, new RoutedEventArgs());
        await Task.Delay(180);
        Capture(Path.Combine(directory, "widget-plain.png"));
        TogglePhotoBackground(this, new RoutedEventArgs());
        var empty = new HistoryWindow(null, Array.Empty<HistoryFrame>());
        empty.Show(); await empty.ReloadAsync(); await Task.Delay(150);
        CaptureControl(empty, Path.Combine(directory, "history-empty.png")); empty.Close();
        var single = new HistoryWindow(null, demoHistory!.TakeLast(1).ToList());
        single.Show(); await single.ReloadAsync(); await Task.Delay(150);
        CaptureControl(single, Path.Combine(directory, "history-one-point.png")); single.Close();
    }
}
