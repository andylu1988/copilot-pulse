using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace CopilotPulse;
public partial class MainWindow
{
    private InsightsWindow? insightsWindow;
    private System.Windows.Controls.Button? diagnosticEntry;
    private ReminderState reminderState = new();
    private DateTimeOffset remindersDue, samplingWatchStarted = DateTimeOffset.UtcNow;
    private string notificationState = "", hotkeyStatus = "";
    private HwndSource? hotkeySource;
    private readonly List<int> registeredHotkeys = new();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private void InitializeOperations()
    {
        reminderState = App.Demo || App.Smoke ? new() : Store.Read<ReminderState>("reminders.json") ?? new();
        reminderState.Sent ??= new();
        ConfigureHotkeys();
        var actions = new System.Windows.Controls.WrapPanel();
        diagnosticEntry = InsightUi.Button(UiText.T("连接诊断", "Connection diagnostics"), () => OpenInsights(2)); actions.Children.Add(diagnosticEntry);
        if (EmptyState.Child is System.Windows.Controls.Panel panel) panel.Children.Add(actions);
    }
    internal void OpenDiagnosticLogin() => OpenSource(this, new RoutedEventArgs());
    internal void RefreshDiagnosticSource() => RefreshNow(this, new RoutedEventArgs());
    private void OpenUsageForecast(object sender, RoutedEventArgs e) => OpenInsights(1);
    private void OpenInsights(int tab = 0)
    {
        if (insightsWindow != null) { insightsWindow.SelectTab(tab); insightsWindow.Show(); insightsWindow.Activate(); _ = insightsWindow.ReloadAsync(); return; }
        insightsWindow = new InsightsWindow(measurementTasks,
            () => Task.Run<IReadOnlyList<HistoryFrame>>(() => demoHistory ?? history?.ReadAll() ?? Array.Empty<HistoryFrame>()),
            () => data, BuildDiagnosticReport, tab) { Owner = this };
        insightsWindow.Closed += (_, _) => insightsWindow = null;
        insightsWindow.Show();
    }
    private void ToggleSampling()
    {
        paused = !paused; samplingWatchStarted = DateTimeOffset.UtcNow;
        RecordSamplingEvent(paused ? "paused" : "resumed"); SetStatus(status);
        if (paused) StatusLabel.Text = UiText.T("自动刷新已暂停", "Auto refresh paused");
        else nextRefresh = DateTimeOffset.Now;
        if (tray != null) { var old = tray.ContextMenuStrip; tray.ContextMenuStrip = BuildTrayMenu(); old?.Dispose(); }
        UpdateTimestamp();
    }
    private void ConfigureHotkeys()
    {
        ReleaseHotkeys();
        if (!preferences.GlobalHotkeys || App.Demo || App.Smoke || !IsLoaded) { hotkeyStatus = ""; return; }
        var handle = new WindowInteropHelper(this).Handle;
        hotkeySource = HwndSource.FromHwnd(handle); hotkeySource?.AddHook(HotkeyMessage);
        var failed = new List<string>();
        foreach (var pair in new[] { (4101, 0x54, "Ctrl+Alt+T"), (4102, 0x4D, "Ctrl+Alt+M"), (4103, 0x52, "Ctrl+Alt+R"), (4104, 0x48, "Ctrl+Alt+H") })
            if (RegisterHotKey(handle, pair.Item1, 0x4003, (uint)pair.Item2)) registeredHotkeys.Add(pair.Item1); else failed.Add(pair.Item3);
        hotkeyStatus = failed.Count == 0 ? "OK" : string.Join(", ", failed);
        if (failed.Count > 0) MessageBox.Show(this, UiText.T("以下快捷键已被其他程序占用：", "These shortcuts are already registered by another app: ") + hotkeyStatus, "Copilot Pulse");
    }
    private void ReleaseHotkeys()
    {
        var handle = new WindowInteropHelper(this).Handle;
        foreach (int id in registeredHotkeys) UnregisterHotKey(handle, id);
        registeredHotkeys.Clear(); hotkeySource?.RemoveHook(HotkeyMessage); hotkeySource = null;
    }
    private IntPtr HotkeyMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != 0x312 || closing) return IntPtr.Zero;
        int id = wParam.ToInt32(); if (!registeredHotkeys.Contains(id)) return IntPtr.Zero;
        handled = true;
        Dispatcher.BeginInvoke(new Action(() => {
            if (id == 4101) { Show(); Activate(); ToggleRecordPoint(this, new RoutedEventArgs()); }
            else if (id == 4102) SetDisplayMode(preferences.DisplayMode == "mini" ? preferences.MiniReturnMode : "mini");
            else if (id == 4103) RefreshNow(this, new RoutedEventArgs());
            else if (id == 4104) { Show(); Activate(); OpenTasks(this, new RoutedEventArgs()); }
        })); return IntPtr.Zero;
    }
    private void EvaluateReminders(bool force = false)
    {
        var now = DateTimeOffset.UtcNow;
        if (App.Demo || App.Smoke || tray == null || closing || (!force && now < remindersDue)) return;
        remindersDue = now.AddSeconds(30);
        string before = JsonSerializer.Serialize(reminderState);
        void Send(string key, string title, string text, bool once = false, bool warning = true)
        {
            if (ReminderPolicy.Due(reminderState, key, preferences, now, once)) tray.ShowBalloonTip(7000, "Copilot Pulse · " + title, text, warning ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
        }
        bool active = data != null && usageWarnings.Active(data, preferences).Count > 0;
        string month = HistoryRanges.Period(now);
        string thresholdKey = $"threshold:{month}:{preferences.WarningMode}:{preferences.WarningPercent}:{preferences.WarningAmount}";
        bool fresh = data?.Ok == true && HistoryRanges.Period(data.FetchedAt) == month && now - data.FetchedAt < TimeSpan.FromSeconds(Math.Max(180, preferences.RefreshSeconds * 3 + 30));
        if (fresh && active) Send(thresholdKey, UiText.T("用量警告", "Usage warning"), WarningText.Text);
        if (fresh && preferences.WarningEnabled && !active && reminderState.ThresholdActive && reminderState.ThresholdKey == thresholdKey && preferences.RecoveryReminder)
            Send("recovery:" + thresholdKey + ":" + reminderState.Sent.GetValueOrDefault(thresholdKey).ToUnixTimeSeconds(), UiText.T("警告恢复", "Warning cleared"), UiText.T("最新读数已低于当前阈值。", "The latest reading is below the configured threshold."), true, false);
        if (fresh && (active || !ReminderPolicy.Muted(preferences, now))) { reminderState.ThresholdActive = active; reminderState.ThresholdKey = thresholdKey; }
        var last = data?.FetchedAt ?? samplingWatchStarted;
        if (last < samplingWatchStarted) last = samplingWatchStarted;
        bool interrupted = !paused && now - last >= TimeSpan.FromMinutes(preferences.SamplingReminderMinutes);
        if (interrupted && preferences.SamplingReminder) Send("sampling:" + last.ToUnixTimeSeconds(), UiText.T("采样中断", "Sampling interrupted"), UiText.T("长时间未获得成功读数。请检查网络、登录或连接诊断。", "No successful reading recently. Check the network, sign-in or connection diagnostics."));
        if (!interrupted && !paused && reminderState.SamplingInterrupted && fresh && preferences.SamplingReminder && preferences.RecoveryReminder)
            Send("sampling-recovered:" + now.ToString("yyyyMMddHHmm"), UiText.T("采样恢复", "Sampling resumed"), UiText.T("已重新获得成功读数。", "A successful reading has been received."), true, false);
        if (interrupted || !ReminderPolicy.Muted(preferences, now)) reminderState.SamplingInterrupted = interrupted;
        var reset = HistoryRanges.Start("month", now).AddMonths(1);
        if (preferences.ResetReminder && reset - now <= TimeSpan.FromHours(preferences.ResetReminderHours))
            Send("reset:" + reset.ToString("yyyyMM"), UiText.T("配额即将重置", "Quota resets soon"), UiText.T($"预计在本机时间 {reset.ToLocalTime():MM/dd HH:mm} 重置（UTC 0点）。", $"Expected reset: {reset.ToLocalTime():MM/dd HH:mm} local (00:00 UTC)."), true, false);
        notificationState = ReminderPolicy.Muted(preferences, now) ? UiText.T("通知静音", "Notifications muted") : active ? UiText.T("警告中", "Warning active") : UiText.T("通知正常", "Notifications ready");
        if (before != JsonSerializer.Serialize(reminderState)) Store.Write("reminders.json", reminderState);
    }
    private async Task<DiagnosticReport> BuildDiagnosticReport()
    {
        var checks = new List<DiagnosticCheck>();
        void Add(string zh, string en, string state, string zDetail, string eDetail) => checks.Add(new(UiText.T(zh, en), state, UiText.T(zDetail, eDetail)));
        Add("系统时区", "System timezone", "OK", "按 Windows 系统时区显示，配额按 UTC 月重置。", "Display follows Windows timezone; quota resets by UTC month.");
        bool initialized = App.Demo, source = App.Demo, signedIn = App.Demo;
        try
        {
            if (monitor != null)
            {
                var diagnosticsTask = monitor.GetDiagnosticsAsync();
                var raw = await diagnosticsTask.WaitAsync(TimeSpan.FromSeconds(5));
                var json = JsonSerializer.SerializeToElement(raw);
                bool Read(string key) => json.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.True;
                initialized = Read("initialized"); source = Read("sourcePage");
                signedIn = source && status == "live";
            }
        }
        catch (Exception e) when (e is TimeoutException or InvalidOperationException or COMException or TaskCanceledException) { }
        Add("浏览器会话", "Browser session", initialized ? "OK" : "ERROR", initialized ? "内嵌浏览器已初始化。" : "浏览器未就绪。请检查 WebView2 Runtime，或重启后重试。", initialized ? "Embedded browser initialized." : "Browser not ready. Check WebView2 Runtime or restart.");
        Add("登录与数据页面", "Sign-in and source page", signedIn ? "OK" : "WARN", signedIn ? "已成功读取数据；登录无需导出账号信息。" : "请点击下方登录，完成公司账号验证，并确认 Consumption 标签。", signedIn ? "A source reading succeeded; account details are not exported." : "Use Sign in below, complete company authentication and check Consumption.");
        bool fresh = App.Demo || data != null && DateTimeOffset.UtcNow - data.FetchedAt < TimeSpan.FromSeconds(Math.Max(180, preferences.RefreshSeconds * 3 + 30));
        Add("采样与页面刷新", "Sampling and page refresh", !paused && fresh ? "OK" : "WARN", paused ? "自动采样已暂停。可从托盘恢复。" : fresh ? "已收到近期成功采样。源页面仍可能滞后。" : "最近采样延迟。请立即刷新并检查网络或登录状态。", paused ? "Automatic sampling is paused. Resume from the tray." : fresh ? "Recent successful sample received. The source page may still lag." : "Samples delayed. Refresh and check network or sign-in.");
        bool writable = true;
        if (!App.Demo) try { string probe = Path.Combine(App.DataDirectory, ".diagnostic-" + Guid.NewGuid().ToString("N")); File.WriteAllText(probe, "probe"); File.Delete(probe); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { writable = false; }
        Add("本机数据存储", "Local data storage", writable && taskStorageError.Length == 0 && history?.LastError == null ? "OK" : "ERROR", writable && taskStorageError.Length == 0 && history?.LastError == null ? "数据目录可写，现有历史与任务未报告错误。" : "存储不可写或历史/任务读取异常。检查磁盘和权限，保留原始文件。", writable && taskStorageError.Length == 0 && history?.LastError == null ? "Data folder writable; no history or task error reported." : "Storage unavailable or history/tasks unreadable. Check disk and permissions; retain original files.");
        Add("全局快捷键", "Global shortcuts", hotkeyStatus is "" or "OK" ? "OK" : "WARN", !preferences.GlobalHotkeys ? "未启用，可在设置中选择启用。" : hotkeyStatus == "OK" ? "四个快捷键已注册。" : "部分快捷键冲突，请关闭占用它们的程序后重新启用。", !preferences.GlobalHotkeys ? "Disabled. Enable from Settings." : hotkeyStatus == "OK" ? "Four shortcuts registered." : "Some shortcuts conflict. Disable the conflicting app then re-enable shortcuts.");
        return new(DateTimeOffset.UtcNow, UpdatePolicy.Current.ToString(), TimeZoneInfo.Local.Id, App.Demo, checks);
    }
}
