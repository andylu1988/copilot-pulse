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
    private HistoryWindow? recordingWindow;
    private IReadOnlyList<HistoryFrame>? demoHistory;
    private int miniRevision;
    private DateTimeOffset miniHistoryUpdated;
    private decimal? miniBaselineDelta;
    private bool HasActiveMeasurement => DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out _);
    private bool ShowMiniTaskDelta => preferences.MiniShowRecordDelta && HasActiveMeasurement;

    private void InitializeHistory()
    {
        if (App.Demo) { demoHistory = CreateDemoHistory(); InitializeTasks(); _ = RefreshMiniHistoryAsync(); return; }
        if (App.Smoke) return;
        history = new HistoryRepository(Path.Combine(App.DataDirectory, "history"));
        history.Changed += HistoryChanged;
        InitializeTasks();
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
            DateTimeOffset now = DateTimeOffset.Now;
            DateTimeOffset start = HistoryRanges.Start(preferences.HistoryRange, now);
            DateTimeOffset end = HistoryRanges.End(preferences.HistoryRange, now);
            var frames = demoHistory != null ? demoHistory.Where(f => f.Time >= start && f.Time <= end).ToList() : (history == null ? Array.Empty<HistoryFrame>() : await Task.Run(() => history.ReadRange(start, end)));
            bool hasBaseline = DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out var baseline);
            var baselineFrames = !hasBaseline ? Array.Empty<HistoryFrame>() : demoHistory != null ? demoHistory.Where(f => f.Time >= baseline && f.Time <= now).ToArray() : history == null ? Array.Empty<HistoryFrame>() : (await Task.Run(() => history.ReadRange(baseline, now))).ToArray();
            if (closing || request != miniRevision) return;
            BaselinePanel.Visibility = hasBaseline ? Visibility.Visible : Visibility.Collapsed;
            RecordPointButton.Content = hasBaseline ? UiText.T("结束并保存计量", "End & save measurement") : UiText.T("开始任务计量", "Start measurement");
            RefreshMiniRecordDeltaButton();
            var activeTask = measurementTasks?.Tasks.SingleOrDefault(t => t.EndUtc == null);
            BaselineTitle.Text = activeTask != null ? UiText.TaskName(activeTask.Name) : UiText.T("任务计量增量  ↗", "Task measurement delta  ↗");
            BaselineTitle.ToolTip = BaselineTitle.Text;
            var baselinePoints = HistoryMath.Series(baselineFrames, null);
            miniBaselineDelta = HistoryMath.BaselineNetChange(baselineFrames, null);
            BaselineAmount.Text = miniBaselineDelta.HasValue ? HistoryChart.Signed(miniBaselineDelta.Value) : "—";
            BaselineStart.Text = UiText.T($"基准 {SystemClock.LocalMinute(baseline)} · 本机时间", $"Baseline {SystemClock.LocalMinute(baseline)} · Local time");
            BaselineSamples.Text = baselinePoints.Count == 0 ? UiText.T("等待首次成功采样", "Waiting for first successful sample") : UiText.T($"{baselinePoints.Count:N0} 次采样 · 实际 {SystemClock.Local(baselinePoints[0].Time):MM/dd HH:mm} → {SystemClock.Local(baselinePoints[^1].Time):MM/dd HH:mm}", $"{baselinePoints.Count:N0} samples · Observed {SystemClock.Local(baselinePoints[0].Time):MM/dd HH:mm} → {SystemClock.Local(baselinePoints[^1].Time):MM/dd HH:mm}");
            BaselinePanel.ToolTip = UiText.T("持续保存成功刷新后的采样，重启后保留。首次采样为零点，按 UTC 月合计已观测增量；首次采样前和月界缺失的用量不补算。账户口径改变或整月无记录时不可计算。点击查看采样历史。", "Successful refreshes are recorded and retained after restart. The first sample starts at zero; observed changes are summed within UTC months. Usage before the first sample or missing at month boundaries is not estimated. Requires a consistent account basis with no missing months. Click to view sample history.");
            BaselineHistory.SetData(miniBaselineDelta.HasValue ? HistoryMath.RecordedSeries(baselineFrames, null) : Array.Empty<HistoryPoint>(), from: hasBaseline ? baseline : null, until: hasBaseline ? now : null);
            var points = HistoryMath.Series(frames, null);
            decimal? net = HistoryMath.NetChange(points);
            HistoryNetGrowth.Text = $"{HistoryRanges.Label(preferences.HistoryRange)}净增长 {(net.HasValue ? HistoryChart.Signed(net.Value) : "—")}";
            HistoryNetGrowth.ToolTip = net.HasValue ? "所选区间总用量的末次读数减去首次读数；未采集时段不补算。负变化可能包含源页面累计值归零或金额修正，不代表负消费。" : HistoryMath.NetChangeUnavailableReason(points);
            HistorySummary.Text = UiText.Translate(HistorySummary.Text);
            HistoryNetGrowth.Text = UiText.Translate(HistoryNetGrowth.Text);
            HistoryNetGrowth.ToolTip = UiText.Translate(HistoryNetGrowth.ToolTip?.ToString() ?? "");
            miniHistoryUpdated = now;
            MiniHistory.SetData(points, from: preferences.HistoryRange == "today" ? start : null, until: preferences.HistoryRange == "today" ? now : null);
            HistorySummary.Text = UiText.Translate(App.Demo ? "示例趋势 · 点击展开" : history?.LastError ?? $"{HistoryRanges.Label(preferences.HistoryRange)} · {frames.Count:N0} 次记录 · 点击展开");
            RefreshUsageProgress();
            if (tasksWindow != null) _ = tasksWindow.ReloadAsync();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { if (closing || request != miniRevision) return; HistorySummary.Text = "历史暂不可读 · 点击查看"; HistoryNetGrowth.Text = $"{HistoryRanges.Label(preferences.HistoryRange)}净增长 —"; BaselineAmount.Text = "—"; BaselineSamples.Text = UiText.T("历史暂不可读", "History temporarily unavailable"); miniBaselineDelta = null; RefreshUsageProgress(); }
    }

    private void HistoryViewChanged()
    {
        SavePreferences();
        _ = RefreshMiniHistoryAsync();
        if (recordingWindow != null) _ = recordingWindow.ReloadAsync();
    }

    private void ToggleRecordPoint(object sender, RoutedEventArgs e)
    {
        bool active = DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out _);
        if (!PrepareTaskToggle(active)) return;
        if (active)
        {
            preferences.MeasurementBaselineUtc = "";
            recordingWindow?.Close();
            RecordPointButton.Content = UiText.T("开始任务计量", "Start measurement");
            miniBaselineDelta = null;
        }
        else
        {
            // Start at the click, never backdate the session to an old cached reading.
            DateTimeOffset point = measurementTasks!.Tasks.Single(t => t.EndUtc == null).StartUtc;
            preferences.MeasurementBaselineUtc = point.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            RecordPointButton.Content = UiText.T("结束并保存计量", "End & save measurement");
        }
        RefreshMiniRecordDeltaButton();
        RefreshUsageProgress();
        SavePreferences();
        _ = RefreshMiniHistoryAsync();
        if (!active && monitor != null) _ = monitor.RefreshAsync();
        if (historyWindow != null) _ = historyWindow.ReloadAsync();
    }

    private void ToggleMiniRecordDelta(object sender, RoutedEventArgs e)
    {
        if (!HasActiveMeasurement) { RefreshMiniRecordDeltaButton(); RefreshUsageProgress(); return; }
        preferences.MiniShowRecordDelta = !preferences.MiniShowRecordDelta;
        RefreshMiniRecordDeltaButton();
        SavePreferences();
        RefreshUsageProgress();
    }

    private void RefreshMiniRecordDeltaButton()
    {
        if (!IsInitialized) return;
        bool active = HasActiveMeasurement;
        if (!active) preferences.MiniShowRecordDelta = false;
        MiniRecordDeltaButton.IsEnabled = active;
        System.Windows.Controls.ToolTipService.SetShowOnDisabled(MiniRecordDeltaButton, true);
        MiniRecordDeltaButton.Content = preferences.MiniShowRecordDelta
            ? UiText.T("迷你显示任务增量：开", "Mini task delta: On")
            : UiText.T("迷你显示任务增量：关", "Mini task delta: Off");
        MiniRecordDeltaButton.ToolTip = !active
            ? UiText.T("请先开始任务计量，再选择迷你显示任务增量。", "Start a task before choosing to show its delta in mini mode.")
            : UiText.T("开启后迷你模式显示当前任务的计量增量；关闭后显示本月总用量。结束任务后自动恢复总用量。", "Show the active task's measured delta in mini mode. When off or the task ends, show the monthly total.");
    }

    private void OpenRecordingWindow(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(preferences.MeasurementBaselineUtc)) return;
        if (recordingWindow != null) { recordingWindow.Show(); recordingWindow.Activate(); return; }
        if (history == null && !App.Demo) return;
        recordingWindow = new HistoryWindow(history, demoHistory, preferences, HistoryViewChanged, true) { Owner = this };
        recordingWindow.Closed += (_, _) => recordingWindow = null;
        recordingWindow.Show();
    }

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
        DateTimeOffset monthStart = HistoryRanges.Start("month", now);
        for (int i = 0; i < 100; i++)
        {
            // In-memory synthetic month: zero at UTC month start, ending at the demo card total.
            decimal fraction = i / 99m;
            decimal first = decimal.Round(1200m * fraction, 2);
            decimal second = decimal.Round(34.56m * fraction, 2);
            DateTimeOffset time = monthStart.AddTicks((now - monthStart).Ticks * i / 99);
            result.Add(new HistoryFrame { Time = time, Period = HistoryRanges.Period(time), Cadence = Math.Max(60, (int)((now - monthStart).TotalSeconds / 99)), Total = first + second, Accounts = new() {
                new HistoryAccount { Name = "Example Enterprise", Login = "demo-emu", Cost = first },
                new HistoryAccount { Name = "Example Organization", Login = "demo-user", Cost = second }
            } });
        }
        return result;
    }

    private async Task ReviewHistoryAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (string mode in new[] { "compact", "full" })
        {
            SetDisplayMode(mode); SetDisplayMode("mini");
            var saved = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(preferences))!;
            SettingsPolicy.Normalize(saved);
            if (saved.MiniReturnMode != mode) throw new InvalidOperationException("Mini return persistence failed: " + mode);
            preferences.MiniReturnMode = saved.MiniReturnMode;
            ExpandMini(this, new RoutedEventArgs());
            if (preferences.DisplayMode != mode || (DetailsPanel.Visibility == Visibility.Collapsed) != (mode == "compact")) throw new InvalidOperationException("Mini restore failed: " + mode);
        }
        preferences.MeasurementBaselineUtc = demoHistory![demoHistory.Count / 2].Time.ToUniversalTime().ToString("O");
        SetDisplayMode("compact");
        await RefreshMiniHistoryAsync();
        await Task.Delay(180);
        if (BaselinePanel.Visibility != Visibility.Visible || BaselineAmount.Text == "—") throw new InvalidOperationException("Baseline card did not render");
        preferences.MiniShowRecordDelta = false;
        RefreshUsageProgress();
        string miniWithoutRecord = MiniUsageProgress.Text;
        ToggleMiniRecordDelta(this, new RoutedEventArgs());
        if (!preferences.MiniShowRecordDelta || MiniDisplay.Value != miniBaselineDelta || !MiniDisplay.RecordedPeriod || FullAmount.Value != data!.Total) throw new InvalidOperationException("Mini recorded delta opt-in failed");
        SetDisplayMode("mini");
        await Task.Delay(100);
        Capture(Path.Combine(directory, "mini-recorded-period.png"));
        ToggleMiniRecordDelta(this, new RoutedEventArgs());
        if (preferences.MiniShowRecordDelta || MiniUsageProgress.Text != miniWithoutRecord || MiniDisplay.Value != data!.Total || MiniDisplay.RecordedPeriod) throw new InvalidOperationException("Mini recorded delta opt-out failed");
        SetDisplayMode("compact");
        Capture(Path.Combine(directory, "baseline-compact.png"));
        string beforeRangeChange = BaselineAmount.Text;
        preferences.HistoryRange = "today"; await RefreshMiniHistoryAsync();
        if (BaselineAmount.Text != beforeRangeChange) throw new InvalidOperationException("Baseline incorrectly depends on history range");
        OpenRecordingWindow(this, new RoutedEventArgs());
        OpenHistory(this, new RoutedEventArgs());
        if (recordingWindow == null || historyWindow == null || ReferenceEquals(recordingWindow, historyWindow)) throw new InvalidOperationException("Independent recording window failed");
        await recordingWindow.ReloadAsync(); await Task.Delay(200);
        CaptureControl(recordingWindow, Path.Combine(directory, "recording-history.png"));
        recordingWindow.Width = 730; await Task.Delay(100);
        CaptureControl(recordingWindow, Path.Combine(directory, "recording-history-narrow.png"));
        historyWindow.Close();
        int originalSamples = demoHistory.Count;
        ToggleRecordPoint(this, new RoutedEventArgs()); await RefreshMiniHistoryAsync();
        if (recordingWindow != null || BaselinePanel.Visibility != Visibility.Collapsed || demoHistory.Count != originalSamples) throw new InvalidOperationException("Record point removal failed");
        var clickTime = DateTimeOffset.UtcNow;
        ToggleRecordPoint(this, new RoutedEventArgs()); await RefreshMiniHistoryAsync();
        if (!DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out var freshPoint) || freshPoint < clickTime || BaselineAmount.Text != "—") throw new InvalidOperationException("New record point reused cached data");
        preferences.MeasurementBaselineUtc = demoHistory[demoHistory.Count / 2].Time.ToUniversalTime().ToString("O");
        await RefreshMiniHistoryAsync();
        File.WriteAllText(Path.Combine(directory, "feature-checks.txt"), "PASS: compact/full mini return; serialized return mode; independent baseline card and window; removal retains samples; new point never backdates cached data; baseline delta=" + BaselineAmount.Text);
        preferences.HistoryRange = "month"; SetDisplayMode("full"); await RefreshMiniHistoryAsync();
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
        var today = new HistoryWindow(null, demoHistory, new Preferences { HistoryRange = "today" }) { Width = 960, Height = 680 };
        today.Show(); await today.ReloadAsync(); await Task.Delay(150);
        CaptureControl(today, Path.Combine(directory, "history-today.png"));
        today.Width = 730; await Task.Delay(150);
        CaptureControl(today, Path.Combine(directory, "history-today-narrow.png"));
        today.Close();
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
