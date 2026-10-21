using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CopilotPulse;

public partial class MainWindow
{
    private SettingsWindow? settingsWindow;
    private readonly UsageWarnings usageWarnings = new();
    private string currentZone = "";
    private DateTimeOffset nextZoneCheck;
    private void OpenPreferences()
    {
        if (settingsWindow != null) { settingsWindow.Show(); settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(preferences, ApplySettings, SkinEdited) { Owner = this };
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }
    private void ApplySettings()
    {
        SettingsPolicy.Normalize(preferences);
        SynchronizeTaskBaseline();
        UiText.Language = preferences.Language;
        if (diagnosticEntry != null) diagnosticEntry.Content = UiText.T("连接诊断", "Connection diagnostics");
        nextRefresh = DateTimeOffset.Now.AddSeconds(preferences.RefreshSeconds);
        SkinEdited();
        skinWindow?.RefreshSelection();
        if (data != null) RenderData(data);
        SetStatus(status);
        UiText.Apply(this);
        if (tasksWindow != null) { tasksWindow.ApplyLanguage(); _ = tasksWindow.ReloadAsync(); }
        RefreshUpdateBadge();
        if (historyWindow != null) { UiText.Apply(historyWindow); _ = historyWindow.ReloadAsync(); }
        if (recordingWindow != null)
        {
            if (string.IsNullOrWhiteSpace(preferences.MeasurementBaselineUtc)) recordingWindow.Close();
            else { UiText.Apply(recordingWindow); _ = recordingWindow.ReloadAsync(); }
        }
        if (tray != null)
        {
            var old = tray.ContextMenuStrip;
            tray.ContextMenuStrip = BuildTrayMenu();
            old?.Dispose();
            UpdateTrayTooltip();
        }
        _ = RefreshMiniHistoryAsync();
        EvaluateWarning(false);
        ConfigureHotkeys(); EvaluateReminders(true);
        insightsWindow?.Close();
        SavePreferences();
    }
    private void EvaluateWarning(bool notify)
    {
        RefreshUsageProgress();
        if (data == null) return;
        var active = usageWarnings.Active(data, preferences);
        WarningBox.Visibility = active.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        string message = preferences.WarningMode == "amount"
            ? UiText.T($"本月用量已达 {Money(data.Total)}，警告阈值 {Money(preferences.WarningAmount)}。", $"Monthly usage {Money(data.Total)} reached your {Money(preferences.WarningAmount)} warning threshold.")
            : UiText.T($"{active.Count} 个账户已达到 {preferences.WarningPercent:0.##}% 配额警告阈值。", $"{active.Count} account(s) reached the {preferences.WarningPercent:0.##}% quota warning threshold.");
        WarningTitle.Text = UiText.T("用量警告 · 已达到阈值", "Usage warning · Threshold reached");
        WarningText.Text = message;
        if (notify) EvaluateReminders(true);
    }
    private void CheckSystemZone()
    {
        if (DateTimeOffset.UtcNow < nextZoneCheck) return;
        nextZoneCheck = DateTimeOffset.UtcNow.AddMinutes(1);
        settingsWindow?.UpdateClock();
        // Clear .NET's cached Windows timezone so changes in Windows Settings take effect while running.
        TimeZoneInfo.ClearCachedData();
        string zone = TimeZoneInfo.Local.Id + TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.Now);
        if (zone == currentZone) return;
        currentZone = zone;
        _ = RefreshMiniHistoryAsync();
        if (historyWindow != null) _ = historyWindow.ReloadAsync();
        if (recordingWindow != null) _ = recordingWindow.ReloadAsync();
        settingsWindow?.UpdateClock();
    }
}

internal sealed partial class SettingsWindow : Window
{
    private readonly Preferences preferences;
    private readonly Action changed;
    private bool building;
    private readonly Action appearanceChanged;
    internal SkinPicker SkinEditor { get; private set; } = null!;
    private StackPanel body = new();
    private TextBlock? zoneText, resetText;
    private TextBox? threshold;
    private TextBlock? error;
    internal SettingsWindow(Preferences preferences, Action changed, Action? appearanceChanged = null)
    {
        this.preferences = preferences; this.changed = changed; this.appearanceChanged = appearanceChanged ?? changed;
        Title = UiText.T("Copilot Pulse · 设置", "Copilot Pulse · Settings"); Icon = BrandIcon.Window;
        InitializeShell();
        Build();
    }
    private TextBlock Label(string text, double size = 12)
    {
        var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 7) };
        body.Children.Add(label); return label;
    }
    private void Section(string zh, string en) { var label = Label(UiText.T(zh, en), 13); label.Margin = new Thickness(0, 18, 0, 9); label.FontWeight = FontWeights.SemiBold; }
    private ComboBox Choices(string[] labels, int selected, Action<int> update)
    {
        var box = new ComboBox { ItemsSource = labels, SelectedIndex = selected, MinHeight = 32, Margin = new Thickness(0, 0, 0, 6) };
        // Explicit colors also keep native popup/selection readable under light and dark Windows themes.
        box.Background = Brushes.White; box.Foreground = Brushes.Black;
        box.SelectionChanged += (_, _) => { if (!building && box.SelectedIndex >= 0) update(box.SelectedIndex); };
        body.Children.Add(box); return box;
    }
    private void Build()
    {
        building = true; BeginPages();
        Page("general", "常规", "General", "语言和采样频率", "Language and sampling interval");
        Section("语言 / Language", "Language / 语言");
        Choices(new[] { "简体中文", "English" }, preferences.Language == "en" ? 1 : 0, i => { preferences.Language = i == 1 ? "en" : "zh-CN"; changed(); Build(); });
        Section("数据刷新频率", "Data refresh interval");
        Choices(Array.ConvertAll(SettingsPolicy.RefreshOptions, n => UiText.T($"{n} 秒", $"{n} seconds")), Array.IndexOf(SettingsPolicy.RefreshOptions, preferences.RefreshSeconds), i => { preferences.RefreshSeconds = SettingsPolicy.RefreshOptions[i]; changed(); });
        Page("skins", "皮肤", "Skins", "选择皮肤、实时预览与特效调节", "Skin gallery, live preview and effects");
        SkinEditor = new SkinPicker(preferences, () => { this.appearanceChanged(); ApplySkin(); }); body.Children.Add(SkinEditor);
        Page("appearance", "外观", "Appearance", "皮肤动画与数字字体", "Skin animation and number font");
        Section("皮肤动画帧率", "Skin animation frame rate");
        Choices(Array.ConvertAll(SettingsPolicy.FpsOptions, n => $"{n} FPS"), Array.IndexOf(SettingsPolicy.FpsOptions, preferences.AnimationFps), i => { preferences.AnimationFps = SettingsPolicy.FpsOptions[i]; changed(); });
        var motion = new CheckBox { Content = UiText.T("减少动态效果", "Reduce motion"), IsChecked = preferences.ReduceMotion, Margin = new Thickness(0, 4, 0, 0) };
        motion.SetResourceReference(Control.ForegroundProperty, "SkinText");
        motion.Click += (_, _) => { preferences.ReduceMotion = motion.IsChecked == true; changed(); }; body.Children.Add(motion);
        Section("数字字体", "Number font");
        Choices(Array.ConvertAll(SettingsPolicy.NumberFonts, f => f == "skin" ? UiText.T("跟随皮肤（液晶 / 像素 / 辉光管）", "Follow skin (LCD / pixels / nixie)") : f), Array.IndexOf(SettingsPolicy.NumberFonts, preferences.NumberFont), i => { preferences.NumberFont = SettingsPolicy.NumberFonts[i]; changed(); Build(); });
        var fontPreview = Label("$1,234.56  ·  78.90%", 26);
        fontPreview.FontFamily = new FontFamily(SettingsPolicy.ResolveFont("Segoe UI", preferences));
        Page("warnings", "警告", "Warnings", "配置用量阈值", "Configure usage thresholds");
        Section("用量警告", "Usage warnings");
        var warning = new CheckBox { Content = UiText.T("启用警告（界面提示 + 系统通知）", "Enable warnings (widget + system notification)"), IsChecked = preferences.WarningEnabled, Margin = new Thickness(0, 0, 0, 9) };
        warning.SetResourceReference(Control.ForegroundProperty, "SkinText");
        warning.Click += (_, _) => { preferences.WarningEnabled = warning.IsChecked == true; changed(); }; body.Children.Add(warning);
        Choices(new[] { UiText.T("各账户配额百分比 (%)", "Per-account quota percentage (%)"), UiText.T("本月合计金额 (USD)", "Monthly total amount (USD)") }, preferences.WarningMode == "amount" ? 1 : 0, i => { preferences.WarningMode = i == 1 ? "amount" : "percent"; changed(); Build(); });
        Label(UiText.T("警告阈值", "Warning threshold"));
        var input = new Grid(); input.ColumnDefinitions.Add(new ColumnDefinition()); input.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        threshold = new TextBox { Text = (preferences.WarningMode == "amount" ? preferences.WarningAmount : preferences.WarningPercent).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), Padding = new Thickness(8), Background = Brushes.White, Foreground = Brushes.Black };
        input.Children.Add(threshold);
        var apply = new Button { Content = UiText.T("应用", "Apply"), Padding = new Thickness(14, 7, 14, 7), Margin = new Thickness(8, 0, 0, 0) };
        Grid.SetColumn(apply, 1); input.Children.Add(apply); body.Children.Add(input); apply.Click += (_, _) => CommitThreshold();
        threshold.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { CommitThreshold(); e.Handled = true; } };
        error = Label("", 11); error.Foreground = Brushes.OrangeRed;
        Label(UiText.T("百分比范围 1–100；金额范围 $0.01–$1,000,000,000。未提供具体限额的账户仅支持金额警告。提醒冷却跨重启保存，静音不隐藏界面警告。", "Percent: 1–100; amount: $0.01–$1,000,000,000. Accounts without a numeric quota support amount warnings only. Reminder cooldown persists after restart; muted notifications do not hide widget warnings."), 10);
        BuildReminderSettings();
        Page("measurement", "计量与时间", "Measurement", "本机时区与自定义基准", "Local timezone and custom baseline");
        Section("系统时间与配额重置", "System time and quota reset"); zoneText = Label("", 11); resetText = Label("", 11); UpdateClock();
        Section("基准周期增量", "Baseline Period Delta");
        string baselineDate = DateTime.Today.ToString("yyyy-MM-dd"), baselineTime = "00:00";
        if (DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var savedBaseline)) { var localSaved = SystemClock.Local(savedBaseline); baselineDate = localSaved.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture); baselineTime = localSaved.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture); }
        var baselineGrid = new Grid(); baselineGrid.ColumnDefinitions.Add(new ColumnDefinition()); baselineGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var dateBox = new TextBox { Text = baselineDate, Padding = new Thickness(8), Margin = new Thickness(0, 0, 6, 6), Background = Brushes.White, Foreground = Brushes.Black };
        var timeBox = new TextBox { Text = baselineTime, Padding = new Thickness(8), Margin = new Thickness(6, 0, 0, 6), Background = Brushes.White, Foreground = Brushes.Black };
        Grid.SetColumn(dateBox, 0); Grid.SetColumn(timeBox, 1); baselineGrid.Children.Add(dateBox); baselineGrid.Children.Add(timeBox); body.Children.Add(baselineGrid);
        var baselineActions = new WrapPanel();
        var baselineSave = new Button { Content = UiText.T("保存基准时间", "Save baseline"), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 6, 0) };
        var baselineClear = new Button { Content = UiText.T("清除", "Clear"), Padding = new Thickness(10, 7, 10, 7) };
        var baselineError = Label("", 11); baselineError.Foreground = Brushes.OrangeRed;
        baselineSave.Click += (_, _) => { baselineError.Foreground = Brushes.OrangeRed; if (!SystemClock.TryParseLocalMinute(dateBox.Text, timeBox.Text, out var utc) || utc > DateTimeOffset.UtcNow) { baselineError.Text = UiText.T("请输入不晚于当前的有效本机日期和时间。", "Enter a valid local date/time no later than now."); return; } preferences.MeasurementBaselineUtc = utc.ToString("O", System.Globalization.CultureInfo.InvariantCulture); baselineError.Foreground = Brushes.LimeGreen; baselineError.Text = UiText.T("已保存基准：" + SystemClock.Local(utc).ToString("yyyy/MM/dd HH:mm") + "（本机时间）", "Baseline saved: " + SystemClock.Local(utc).ToString("yyyy/MM/dd HH:mm") + " (local time)"); changed(); };
        baselineClear.Click += (_, _) => { preferences.MeasurementBaselineUtc = ""; dateBox.Text = DateTime.Today.ToString("yyyy-MM-dd"); timeBox.Text = "00:00"; baselineError.Foreground = Brushes.OrangeRed; baselineError.Text = UiText.T("基准时间已清除。", "Baseline cleared."); changed(); };
        baselineActions.Children.Add(baselineSave); baselineActions.Children.Add(baselineClear); body.Children.Add(baselineActions);
        Label(UiText.T("格式：YYYY-MM-DD 和 HH:mm。输入的是本机时间；保存后转换为绝对 UTC，时区变化不会改写基准点。自定义区间会持续保存采样记录。", "Format: YYYY-MM-DD and HH:mm. Input is local time and is saved as absolute UTC; timezone changes do not move the baseline. Samples continue to be recorded for the custom period."), 10);
        Page("updates", "更新与链接", "Updates & links", "版本更新与使用文档", "Version updates and documentation");
        Section("在线更新", "Online updates");
        var updates = new Button { Content = UiText.T("检查新版本 / 下载更新…", "Check version / download update…"), Padding = new Thickness(10,8,10,8), HorizontalAlignment = HorizontalAlignment.Left };
        updates.Click += (_, _) => { if (Owner is MainWindow main) main.OpenUpdates(); }; body.Children.Add(updates);
        Label(UiText.T("默认使用个人公开仓库，无需登录 GitHub。也可选择 EMU 私有仓库。更新保留历史、登录和设置。", "Updates use the public personal repository without GitHub sign-in. The private EMU mirror is optional. Updates retain history, sign-in and preferences."), 10);
        Section("网页链接", "Web links");
        var links = new WrapPanel();
        void Link(string label, string url) { var button = new Button { Content = label + " ↗", Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(9, 6, 9, 6) }; button.Click += (_, _) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); links.Children.Add(button); }
        Link(UiText.T("用量网页", "Usage page"), SourceMonitor.Target);
        Link(UiText.T("使用文档", "User guide"), "https://github.com/andylu1988/copilot-pulse#readme");
        Link(UiText.T("EMU 项目", "EMU project"), "https://github.com/andylu_microsoft/copilot-pulse"); body.Children.Add(links);
        EndPages(); building = false;
    }
    private void CommitThreshold()
    {
        bool valid = decimal.TryParse(threshold!.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out decimal value) &&
            (preferences.WarningMode == "percent" ? value >= 1 && value <= 100 : value >= .01m && value <= 1_000_000_000m);
        if (!valid) { error!.Text = UiText.T("请输入范围内的有效数字。", "Enter a valid number within the allowed range."); return; }
        if (preferences.WarningMode == "percent") preferences.WarningPercent = value; else preferences.WarningAmount = value;
        error!.Text = UiText.T("阈值已保存。", "Threshold saved."); changed();
    }
    internal void UpdateClock()
    {
        var now = DateTimeOffset.Now;
        zoneText!.Text = SystemClock.ZoneLabel(now);
        var next = HistoryRanges.Start("month", now).AddMonths(1);
        resetText!.Text = UiText.T($"下次重置：{SystemClock.Local(next):yyyy/MM/dd HH:mm}（本机时间）\n对应 UTC {next:yyyy/MM/dd HH:mm}；“今天”按本机午夜。", $"Next reset: {SystemClock.Local(next):yyyy/MM/dd HH:mm} (local)\nUTC {next:yyyy/MM/dd HH:mm}; Today starts at local midnight.");
    }
}
