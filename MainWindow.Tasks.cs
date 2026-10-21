using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CopilotPulse;

public partial class MainWindow
{
    private TaskRepository? measurementTasks;
    private SamplingRepository? samplingLog;
    private TasksWindow? tasksWindow;
    private string taskStorageError = "";
    private DateTimeOffset? lastAmountChange;

    private void InitializeTasks()
    {
        try
        {
            measurementTasks = new TaskRepository(App.Demo ? null : Path.Combine(App.DataDirectory, "tasks.json"));
            samplingLog = new SamplingRepository(App.Demo ? null : Path.Combine(App.DataDirectory, "sampling"));
            if (App.Demo)
            {
                var frames = demoHistory!;
                foreach (var range in new[] { (70, 85, UiText.T("修复登录 Bug", "Fix login bug")), (87, 99, UiText.T("准备客户方案", "Prepare customer proposal")) })
                {
                    var t = measurementTasks.Start(range.Item3, frames[range.Item1].Time, frames[range.Item1].Cadence);
                    measurementTasks.End(t.Id, frames[range.Item2].Time);
                }
                foreach (var f in frames) samplingLog.Append(new SamplingEvent(f.Time, "live", f.Cadence));
                samplingLog.Append(new SamplingEvent(frames[80].Time.AddSeconds(-20), "network", 60));
                samplingLog.Append(new SamplingEvent(frames[80].Time.AddSeconds(-10), "syncing", 60));
            }
            else
            {
                var active = measurementTasks.Tasks.SingleOrDefault(t => t.EndUtc == null);
                if (active != null) preferences.MeasurementBaselineUtc = active.StartUtc.ToString("O");
                else if (DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out var old) && measurementTasks.Tasks.Any(t => t.StartUtc == old)) preferences.MeasurementBaselineUtc = "";
                else measurementTasks.SyncBaseline(preferences.MeasurementBaselineUtc, preferences.RefreshSeconds, DateTimeOffset.UtcNow);
                SavePreferences();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            measurementTasks = null;
            taskStorageError = UiText.T("任务记录暂不可读写，原文件已保留。请检查磁盘和权限。", "Task storage unavailable; original file retained. Check disk and permissions.");
        }
        RefreshSamplingHealth();
    }

    private bool TryTaskWrite(Action write)
    {
        try { write(); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            taskStorageError = UiText.T("任务保存失败，原始记录已保留。请检查磁盘、权限和系统时间后重试。", "Task could not be saved. Original records retained; check disk, permissions and system clock.");
            MessageBox.Show(this, taskStorageError, "Copilot Pulse"); RefreshSamplingHealth(); return false;
        }
    }

    private void SynchronizeTaskBaseline()
    {
        if (measurementTasks == null) return;
        if (!TryTaskWrite(() => measurementTasks.SyncBaseline(preferences.MeasurementBaselineUtc, preferences.RefreshSeconds, DateTimeOffset.UtcNow)))
            preferences.MeasurementBaselineUtc = measurementTasks.Tasks.SingleOrDefault(t => t.EndUtc == null)?.StartUtc.ToString("O") ?? "";
    }

    private bool PrepareTaskToggle(bool active)
    {
        if (measurementTasks == null) { MessageBox.Show(this, taskStorageError, "Copilot Pulse"); return false; }
        // Reviews may change the legacy baseline directly; keep the demo's metadata consistent.
        if (App.Demo) SynchronizeTaskBaseline();
        if (active)
        {
            var task = measurementTasks.Tasks.SingleOrDefault(t => t.EndUtc == null);
            return task == null || TryTaskWrite(() => measurementTasks.End(task.Id, DateTimeOffset.UtcNow));
        }
        string name = UiText.T("未命名任务", "Untitled task");
        if (!App.Demo || App.TasksReviewPath == null && App.HistoryReviewPath == null)
        {
            var dialog = new TaskNameDialog(this);
            if (dialog.ShowDialog() != true) return false;
            name = dialog.TaskName;
        }
        return TryTaskWrite(() => measurementTasks.Start(name, DateTimeOffset.UtcNow, preferences.RefreshSeconds));
    }

    private void RecordSamplingEvent(string value)
    {
        if (samplingLog == null || !SamplingHealth.KnownStates.Contains(value)) return;
        try { samplingLog.Append(new SamplingEvent(DateTimeOffset.UtcNow, value, preferences.RefreshSeconds)); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            taskStorageError = UiText.T("采样状态日志保存失败，请检查磁盘或权限。", "Sampling journal could not be saved; check disk and permissions.");
        }
    }

    private (string Label, string Hint) SamplingHealthText()
    {
        string state = SamplingHealth.State(status, paused, data?.FetchedAt, DateTimeOffset.UtcNow, preferences.RefreshSeconds);
        string label = App.Demo ? UiText.T("● 只读演示 · 示例采样", "● Read-only demo · Synthetic samples") : state switch
        {
            "paused" => UiText.T("Ⅱ 采样已暂停", "Ⅱ Sampling paused"),
            "sampling" => UiText.T("◌ 正在采样", "◌ Sampling"),
            "fresh" => UiText.T("● 刚更新", "● Fresh"),
            "page" => UiText.T("! 页面未刷新 / 需要处理", "! Page not refreshed / Action needed"),
            _ => UiText.T("! 数据延迟", "! Data delayed")
        };
        string last = data?.FetchedAt.ToLocalTime().ToString("MM/dd HH:mm:ss") ?? "—";
        string hint = UiText.T($"最近成功 {last} · 目标间隔 {preferences.RefreshSeconds} 秒。", $"Last success {last} · Target interval {preferences.RefreshSeconds}s.");
        if (state == "page") hint += UiText.T(" 请打开原始页面检查登录和 Consumption 标签。", " Open the source page to check sign-in and the Consumption tab.");
        if (lastAmountChange.HasValue && (DateTimeOffset.UtcNow - lastAmountChange.Value).TotalSeconds > Math.Max(300, preferences.RefreshSeconds * 5))
            hint += UiText.T(" 读数持续未变化：可能没有消耗，也可能源页面更新滞后。", " Readings unchanged: there may be no usage, or the source page may be lagging.");
        return (label, hint + (taskStorageError.Length > 0 ? " " + taskStorageError : ""));
    }

    private void RefreshSamplingHealth()
    {
        if (!IsInitialized || preferences == null) return;
        var h = SamplingHealthText();
        SamplingHealthLabel.Text = h.Label + (notificationState.Length == 0 ? "" : " · " + notificationState);
        SamplingHealthLabel.ToolTip = h.Hint + (notificationState.Length == 0 ? "" : " · " + notificationState);
        SamplingHealthLabel.Foreground = Brush(taskStorageError.Length > 0 || (!App.Demo && h.Label.StartsWith('!')) ? "#FFB86C" : "#82CDB0");
    }

    private void OpenTasks(object sender, RoutedEventArgs e)
    {
        if (measurementTasks == null) { MessageBox.Show(this, taskStorageError, "Copilot Pulse"); return; }
        if (tasksWindow != null) { tasksWindow.Show(); tasksWindow.Activate(); return; }
        tasksWindow = new TasksWindow(measurementTasks,
            () => Task.Run<(IReadOnlyList<HistoryFrame>, IReadOnlyList<SamplingEvent>)>(() => (demoHistory ?? history?.ReadAll() ?? Array.Empty<HistoryFrame>(), samplingLog?.ReadAll() ?? Array.Empty<SamplingEvent>())),
            SamplingHealthText,
            () => { if (!DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out _)) ToggleRecordPoint(this, new RoutedEventArgs()); },
            () => { if (DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out _)) ToggleRecordPoint(this, new RoutedEventArgs()); },
            (id, name) => { TryTaskWrite(() => measurementTasks.Rename(id, name)); _ = RefreshMiniHistoryAsync(); }) { Owner = this };
        tasksWindow.Closed += (_, _) => tasksWindow = null;
        tasksWindow.Show();
    }

    private void OpenDemo()
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { WorkingDirectory = AppContext.BaseDirectory };
        info.ArgumentList.Add("--demo"); info.ArgumentList.Add("--skin"); info.ArgumentList.Add("matrix");
        Process.Start(info);
    }

    private async Task ReviewTasksAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        OpenTasks(this, new RoutedEventArgs());
        await tasksWindow!.ReloadAsync(); await Task.Delay(500);
        tasksWindow.SelectForReview(1); await Task.Delay(150);
        CaptureControl(tasksWindow, Path.Combine(directory, "tasks-matrix.png"));
        tasksWindow.Width = 740; await Task.Delay(200);
        CaptureControl(tasksWindow, Path.Combine(directory, "tasks-narrow.png"));
        var task = measurementTasks!.Tasks[0];
        var metrics = SamplingHealth.Measure(task, demoHistory!, samplingLog!.ReadAll(), DateTimeOffset.UtcNow);
        if (metrics.Samples != 16 || metrics.FailedAttempts != 1 || metrics.RetryAttempts != 1) throw new InvalidOperationException("Task review sample/attempt count mismatch");
        File.WriteAllText(Path.Combine(directory, "anonymous.json"), TaskExport.Json(measurementTasks.Tasks, demoHistory!, samplingLog.ReadAll(), DateTimeOffset.UtcNow, true));
        File.WriteAllText(Path.Combine(directory, "tasks.csv"), TaskExport.Csv(measurementTasks.Tasks, demoHistory!, samplingLog.ReadAll(), DateTimeOffset.UtcNow, true));
        tasksWindow.Close();
        InsightUi.SavePng(InsightUi.TaskReport(task,demoHistory!,DateTimeOffset.UtcNow,true),Path.Combine(directory,"task-report.png"));
        OpenInsights(1); await Task.Delay(500); await insightsWindow!.ReloadAsync(); await Task.Delay(300);
        if (GlassChrome.GetSource(insightsWindow) == null || GlassChrome.GetSource(this) != FullBackdrop)
            throw new InvalidOperationException("Glass background source missing");
        string skin = preferences.SkinId ?? "midnight";
        preferences.SkinId = "midnight"; SkinEdited(); await Task.Delay(120);
        CaptureControl(insightsWindow, Path.Combine(directory, "insights-midnight.png"));
        preferences.SkinId = skin; SkinEdited(); await Task.Delay(120);
        CaptureControl(insightsWindow,Path.Combine(directory,"insights-forecast-zh.png"));
        if (insightsWindow.Width > 600) throw new InvalidOperationException("Forecast window must remain compact");
        insightsWindow.Close(); OpenDiagnostics(); await diagnosticsWindow!.ReloadAsync(); await Task.Delay(150);
        CaptureControl(diagnosticsWindow,Path.Combine(directory,"insights-diagnostics-zh.png"));
        diagnosticsWindow.ExportReview(directory);
        string diagnostic = File.ReadAllText(Path.Combine(directory,"diagnostics.json"));
        if (diagnostic.Contains("demo-user") || diagnostic.Contains("1234.56")) throw new InvalidOperationException("Diagnostic privacy failure");
        diagnosticsWindow!.Close();
        string language = preferences.Language; preferences.Language = "en"; UiText.Language = "en";
        OpenInsights(0); await Task.Delay(400); await insightsWindow!.ReloadAsync(); await Task.Delay(200);
        CaptureControl(insightsWindow,Path.Combine(directory,"insights-forecast-en.png")); insightsWindow.Close();
        preferences.Language = language; UiText.Language = language;
        foreach (string lang in new[] { "zh-CN", "en" })
        {
            UiText.Language = lang;
            var settingsReview = new SettingsWindow(preferences, () => { }) { Owner = this };
            settingsReview.Show(); await Task.Delay(200);
            foreach (string page in new[] { "general", "appearance", "warnings", "notifications", "shortcuts", "measurement", "updates" })
            {
                settingsReview.SelectPageForReview(page); await Task.Delay(90); settingsReview.VerifyFixedChromeForReview();
                CaptureControl(settingsReview, Path.Combine(directory, "settings-" + page + "-" + lang + ".png"));
            }
            settingsReview.Close();
        }
        UiText.Language = language;
        await ReviewQuotaScenarios(directory);
        var chart = new HistoryChart { Width=900,Height=300 };
        var series = HistoryMath.Series(demoHistory!,null);
        chart.SetData(series); InsightUi.SavePng(chart,Path.Combine(directory,"chart-full.png"),900);
        chart.ZoomForReview(series[0].Time,series[0].Time.AddMinutes(3));
        if (!chart.IsZoomed) throw new InvalidOperationException("Zoom did not apply");
        chart.SetData(series); if (!chart.IsZoomed) throw new InvalidOperationException("Refresh lost zoom");
        chart.InspectPointForReview(1); InsightUi.SavePng(chart,Path.Combine(directory,"chart-zoom.png"),900);
        chart.ResetZoom(); if(chart.IsZoomed) throw new InvalidOperationException("Reset zoom failed");
        await RefreshMiniHistoryAsync(); UpdateLayout();
        var taskButtons = new[] { RecordPointButton, TaskHistoryButton, MiniRecordDeltaButton, UsageForecastButton };
        if (taskButtons.Any(b => b.ActualHeight != 34 || Math.Abs(b.ActualWidth - RecordPointButton.ActualWidth) > 1 || b.Style != FindResource("GlassButtonStyle")))
            throw new InvalidOperationException("Task button size or glass style mismatch");
        var topLeft = RecordPointButton.TranslatePoint(new Point(), this);
        var topRight = TaskHistoryButton.TranslatePoint(new Point(), this);
        var bottomLeft = MiniRecordDeltaButton.TranslatePoint(new Point(), this);
        var bottomRight = UsageForecastButton.TranslatePoint(new Point(), this);
        if (topLeft.Y != topRight.Y || bottomLeft.Y != bottomRight.Y || topLeft.X != bottomLeft.X || topRight.X != bottomRight.X || bottomLeft.Y <= topLeft.Y)
            throw new InvalidOperationException("Task actions are not a 2 by 2 grid");
        OpenUsageForecast(this, new RoutedEventArgs()); await Task.Delay(200);
        if (!insightsWindow!.Title.Contains(UiText.T("用量预估", "Usage forecast"))) throw new InvalidOperationException("Usage forecast naming mismatch");
        insightsWindow.Close();
        string? oldBaseline = preferences.MeasurementBaselineUtc;
        bool oldMiniDelta = preferences.MiniShowRecordDelta;
        preferences.MeasurementBaselineUtc = "";
        preferences.MiniShowRecordDelta = true;
        RefreshMiniRecordDeltaButton(); RefreshUsageProgress();
        if (MiniRecordDeltaButton.IsEnabled || preferences.MiniShowRecordDelta || ShowMiniTaskDelta)
            throw new InvalidOperationException("Inactive measurement must disable and clear mini task delta");
        ToggleMiniRecordDelta(this, new RoutedEventArgs());
        if (preferences.MiniShowRecordDelta) throw new InvalidOperationException("Inactive task delta was enabled");
        preferences.MeasurementBaselineUtc = demoHistory![87].Time.ToString("O"); RefreshMiniRecordDeltaButton();
        if (!MiniRecordDeltaButton.IsEnabled || preferences.MiniShowRecordDelta)
            throw new InvalidOperationException("Active task must enable opt-in without selecting it");
        preferences.MeasurementBaselineUtc = oldBaseline;
        preferences.MiniShowRecordDelta = oldMiniDelta; RefreshMiniRecordDeltaButton(); RefreshUsageProgress();
        OpenHistory(this, new RoutedEventArgs()); await Task.Delay(200); await historyWindow!.ReloadAsync();
        if (GlassChrome.GetSource(historyWindow) == null) throw new InvalidOperationException("History glass source missing");
        CaptureControl(historyWindow, Path.Combine(directory, "history-glass.png")); historyWindow.Close();
        foreach (string lang in new[] { "zh-CN", "en" })
        {
            UiText.Language = lang; string tooltip = TrayUsageText();
            if (!tooltip.Contains("1,234.56") || tooltip.Length > 127 || tooltip.Contains("demo-user"))
                throw new InvalidOperationException("Tray usage tooltip invalid");
            File.WriteAllText(Path.Combine(directory, "tray-tooltip-" + lang + ".txt"), tooltip);
        }
        UiText.Language = language;
        Capture(Path.Combine(directory, "widget-tasks.png"));
        preferences.Language = "en"; ApplySettings(); await Task.Delay(150);
        Capture(Path.Combine(directory, "widget-tasks-en.png"));
        preferences.Language = language; ApplySettings();
        await CaptureReleaseGallery(directory);
        File.WriteAllText(Path.Combine(directory, "feature-checks.txt"), "PASS: 16 samples, 1 failed refresh, 1 retry; anonymous exports; isolated demo; bilingual compact forecast, standalone diagnostics, themed About/Update/task naming, PNG exports, chart zoom/preserve/reset, diagnostic privacy.\n");
    }

    private async Task CaptureReleaseGallery(string directory)
    {
        preferences.SkinId = "matrix";
        preferences.HistoryRange = "month";
        preferences.MeasurementBaselineUtc = demoHistory![87].Time.ToUniversalTime().ToString("O");
        SynchronizeTaskBaseline();
        foreach (string lang in new[] { "zh-CN", "en" })
        {
            preferences.Language = lang; preferences.WarningEnabled = true;
            preferences.WarningMode = "amount"; preferences.WarningAmount = 2000;
            preferences.MiniShowRecordDelta = false; ApplySettings();
            await RefreshMiniHistoryAsync();
            SetDisplayMode("full"); await Task.Delay(200);
            Capture(Path.Combine(directory, $"release-full-{lang}.png"));
            SetDisplayMode("compact"); await Task.Delay(100);
            Capture(Path.Combine(directory, $"release-compact-{lang}.png"));
            preferences.WarningAmount = 1000; ApplySettings(); await Task.Delay(100);
            Capture(Path.Combine(directory, $"release-warning-{lang}.png"));
            preferences.WarningAmount = 2000; ApplySettings();
            preferences.MiniShowRecordDelta = true; await RefreshMiniHistoryAsync();
            SetDisplayMode("mini"); await Task.Delay(100);
            Capture(Path.Combine(directory, $"release-mini-task-{lang}.png"));
            preferences.MiniShowRecordDelta = false; RefreshNumericState();
            Capture(Path.Combine(directory, $"release-mini-month-{lang}.png"));
            SetDisplayMode("compact");
            OpenRecordingWindow(this, new RoutedEventArgs()); await recordingWindow!.ReloadAsync(); await Task.Delay(100);
            CaptureControl(recordingWindow, Path.Combine(directory, $"release-recording-{lang}.png")); recordingWindow.Close();
            OpenHistory(this, new RoutedEventArgs()); await historyWindow!.ReloadAsync(); await Task.Delay(100);
            CaptureControl(historyWindow, Path.Combine(directory, $"release-history-{lang}.png"));
            historyWindow.HoverForReview();
            CaptureControl(historyWindow, Path.Combine(directory, $"release-hover-{lang}.png"));
            historyWindow.ShowChangesForReview(); await Task.Delay(80);
            CaptureControl(historyWindow, Path.Combine(directory, $"release-changes-{lang}.png")); historyWindow.Close();
            OpenTasks(this, new RoutedEventArgs()); await tasksWindow!.ReloadAsync(); tasksWindow.SelectForReview(1); await Task.Delay(100);
            CaptureControl(tasksWindow, Path.Combine(directory, $"release-tasks-{lang}.png")); tasksWindow.Close();
            OpenInsights(1); await insightsWindow!.ReloadAsync(); await Task.Delay(100);
            CaptureControl(insightsWindow, Path.Combine(directory, $"release-forecast-{lang}.png")); insightsWindow.Close();
            OpenDiagnostics(); await diagnosticsWindow!.ReloadAsync(); await Task.Delay(100);
            CaptureControl(diagnosticsWindow, Path.Combine(directory, $"release-diagnostics-{lang}.png")); diagnosticsWindow.Close();
            var about = new AboutWindow(preferences, () => { }) { Owner = this };
            about.Show(); await about.CheckLatestAsync(); await Task.Delay(120);
            CaptureControl(about, Path.Combine(directory, $"release-about-{lang}.png")); about.Close();
            var update = new UpdateWindow(preferences) { Owner = this };
            update.Show(); await Task.Delay(120);
            CaptureControl(update, Path.Combine(directory, $"release-updates-{lang}.png")); update.Close();
            var naming = new TaskNameDialog(this); naming.Show(); await Task.Delay(120);
            CaptureControl(naming, Path.Combine(directory, $"release-task-name-{lang}.png")); naming.Close();

            OpenSkins(this, new RoutedEventArgs()); await Task.Delay(150);
            CaptureControl(settingsWindow!, Path.Combine(directory, $"release-skins-{lang}.png")); settingsWindow!.Close();
        }
    }
}
