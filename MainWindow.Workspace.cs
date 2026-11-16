using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CopilotPulse;

public partial class MainWindow
{

    private bool clickThrough, automaticMove, fullscreenHidden, tucked, tuckedOnRightEdge, desktopDragging;
    private double untuckedLeft;
    private DateTimeOffset desktopIdle = DateTimeOffset.UtcNow, archiveDue;
    private readonly DispatcherTimer desktopTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out DesktopRect rect);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int ReadWindowStyle(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int WriteWindowStyle(IntPtr window, int index, int value);
    [StructLayout(LayoutKind.Sequential)] private struct DesktopRect { public int Left, Top, Right, Bottom; }
    private void InitializeWorkspace()
    {
        preferences.TaskTemplates ??= new();
        desktopTimer.Tick += (_, _) => DesktopTick();
        if (!App.Demo && !App.Smoke) desktopTimer.Start();
        Closing += (_, _) => { desktopTimer.Stop(); };
        MouseEnter += (_, _) => { desktopIdle = DateTimeOffset.UtcNow; RevealTucked(); };
        MouseMove += (_, _) => desktopIdle = DateTimeOffset.UtcNow;
        _ = RefreshArchivesAsync();
        if (!App.Demo && !App.Smoke && preferences.LastSeenVersion != "1.15.0")
            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(ShowWhatsNew));
    }
    private void ShowWhatsNew()
    {
        var window = new Window { Title = UiText.T("这次改了什么 · v1.15.0", "What's new · v1.15.0"), Width = 540, SizeToContent = SizeToContent.Height, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner, Icon = BrandIcon.Window };
        var body = new StackPanel { Margin = new Thickness(24) }; _ = new UtilitySkin(window, body);
        body.Children.Add(new TextBlock { Text = UiText.T("v1.15.0 更新要点", "What's new in v1.15.0"), FontSize = 22, Margin = new Thickness(0, 0, 0, 14) });
        body.Children.Add(new TextBlock { Text = UiText.T("• 原生 Windows ARM64 / x64 双架构与在线自动切换\n• 电源感知自适应帧率、冻结渲染缓存与后台低内存采集\n• 一键贴边收纳与中央恢复、桌面显示预设与可见增长脉冲\n• 数据新鲜度外框、开机启动与分类设置页", "• Native Windows ARM64 / x64 packages and automatic arch upgrade\n• Power-aware FPS pacing, frozen render caches and idle WebView2 memory target\n• Immediate edge tuck & center recovery, desktop presets and visible growth pulse\n• Data freshness frame, launch at sign-in and organized settings"), TextWrapping = TextWrapping.Wrap, LineHeight = 25 });
        body.Children.Add(InsightUi.Button(UiText.T("打开桌面与收纳设置", "Open desktop & tuck settings"), () => { window.Close(); OpenWorkspace(); }));
        body.Children.Add(InsightUi.Button(UiText.T("知道了", "Got it"), () => window.Close()));
        window.Closed += (_, _) => { preferences.LastSeenVersion = "1.15.0"; SavePreferences(); };
        window.Show();
    }
    private void ToggleAmountPrivacy()
    {
        preferences.HideAmounts = !preferences.HideAmounts;
        historyWindow?.Close(); recordingWindow?.Close(); tasksWindow?.Close(); insightsWindow?.Close(); aboutWindow?.Close(); updateWindow?.Close(); diagnosticsWindow?.Close();
        if (data != null) RenderData(data);
        EvaluateWarning(false);
        _ = RefreshMiniHistoryAsync(); RefreshDisplayEffects(); UpdateTrayTooltip(); SavePreferences();
    }
    private bool AmountDetailsHidden()
    {
        if (!preferences.HideAmounts) return false;
        var window = new Window { Owner = this, Title = UiText.T("金额已隐藏", "Amounts hidden"), Width = 420, SizeToContent = SizeToContent.Height, WindowStartupLocation = WindowStartupLocation.CenterOwner, Icon = BrandIcon.Window };
        var panel = new StackPanel { Margin = new Thickness(24) }; _ = new UtilitySkin(window, panel);
        panel.Children.Add(new TextBlock { Text = UiText.T("共享屏幕保护已开启。趋势详情、计量记录及预估含金额，关闭保护后可查看。主界面仍显示进度与采样状态。", "Screen-sharing protection is on. History, measurement details and forecasts contain amounts. Turn protection off to view them. Progress and sampling status remain visible on the main panel."), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(InsightUi.Button(UiText.T("关闭保护并返回", "Turn protection off"), () => { window.Close(); ToggleAmountPrivacy(); }));
        window.Show(); return true;
    }
    private void SetClickThrough(bool enabled)
    {
        if (enabled && !registeredHotkeys.Contains(4105) && (tray == null || !tray.Visible)) return;
        RevealTucked(); clickThrough = enabled;
        var handle = new WindowInteropHelper(this).Handle;
        int style = ReadWindowStyle(handle, -20);
        WriteWindowStyle(handle, -20, enabled ? style | 0x20 | 0x80000 : style & ~0x20);
    }
    private void RestoreDesktopInteraction()
    {
        SetClickThrough(false); preferences.DesktopLocked = false; fullscreenHidden = false;
        RevealTucked(); Show(); Activate(); SavePreferences();
    }
    private void TuckedStripEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        desktopIdle = DateTimeOffset.UtcNow;
        RevealTucked();
    }
    private void TuckedStripClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        desktopIdle = DateTimeOffset.UtcNow;
        RevealTucked();
        Show(); Activate();
        ShowDesktopStatus(UiText.T("已展开 Copilot Pulse", "Copilot Pulse revealed"));
        e.Handled = true;
    }
    private void RevealTucked()
    {
        bool wasTucked = tucked;
        if (tucked)
        {
            automaticMove = true; Left = untuckedLeft; tucked = false; automaticMove = false;
        }
        TuckedStrip.Visibility = Visibility.Collapsed;
        TuckedStrip.Opacity = 0;
        DesktopRoot.Clip = null;
        desktopIdle = DateTimeOffset.UtcNow;
        if (wasTucked) ShowDesktopStatus(UiText.T("已展开 Copilot Pulse", "Copilot Pulse revealed"));
    }
    private void BeginDesktopDrag()
    {
        RevealTucked();
        desktopDragging = true;
    }
    private void EndDesktopDrag()
    {
        desktopDragging = false;
        desktopIdle = DateTimeOffset.UtcNow;
        SnapDesktop();
        untuckedLeft = Left;
        SavePreferences();
    }
    private void DesktopLocationChanged()
    {
        if (!IsLoaded || automaticMove) return;
        // A user/external move invalidates the old hidden position. Never jump back to it.
        if (tucked)
        {
            tucked = false;
            TuckedStrip.Visibility = Visibility.Collapsed;
            TuckedStrip.Opacity = 0;
            DesktopRoot.Clip = null;
        }
        untuckedLeft = Left;
        desktopIdle = DateTimeOffset.UtcNow;
        if (!App.Demo && !App.Smoke) { preferences.Left = Left; preferences.Top = Top; }
    }
    private void CenterDesktop()
    {
        RevealTucked();
        preferences.EdgeTuck = false;
        preferences.HideForFullscreen = false;
        fullscreenHidden = false;
        var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        var origin = PointToScreen(new Point()); var dpi = VisualTreeHelper.GetDpi(this);
        Left += (area.Left + (area.Width - ActualWidth * dpi.DpiScaleX) / 2 - origin.X) / dpi.DpiScaleX;
        Top += (area.Top + (area.Height - ActualHeight * dpi.DpiScaleY) / 2 - origin.Y) / dpi.DpiScaleY;
        Show(); Activate(); SavePreferences();
        ShowDesktopStatus(UiText.T("已移回中央 · 自动收纳已关闭", "Centered · automatic tuck is off"));
    }
    private void ConfigureTuckedStrip(bool dockRight)
    {
        TuckedStrip.HorizontalAlignment = dockRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        TuckedStrip.CornerRadius = dockRight ? new CornerRadius(0, 7, 7, 0) : new CornerRadius(7, 0, 0, 7);
        TuckedAccent.HorizontalAlignment = dockRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        TuckedAccent.Margin = dockRight ? new Thickness(10, 10, 0, 10) : new Thickness(0, 10, 10, 10);
        TuckedArrow.Text = dockRight ? "‹" : "›";
        TuckedStrip.ToolTip = UiText.T(dockRight ? "点击或移入展开 Copilot Pulse（右侧收纳）" : "点击或移入展开 Copilot Pulse（左侧收纳）",
            dockRight ? "Click or hover to reveal Copilot Pulse (tucked right)" : "Click or hover to reveal Copilot Pulse (tucked left)");
        System.Windows.Automation.AutomationProperties.SetName(TuckedStrip, UiText.T("Copilot Pulse 收纳边条", "Copilot Pulse tuck strip"));
    }
    private void TuckAtEdge(bool dockRight, bool showStatus)
    {
        if (tucked) return;
        var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        var origin = PointToScreen(new Point());
        var dpi = VisualTreeHelper.GetDpi(this);
        double targetX = dockRight
            ? area.Right - 24 * dpi.DpiScaleX
            : area.Left - (ActualWidth - 24) * dpi.DpiScaleX;
        untuckedLeft = Left;
        automaticMove = true;
        Left += (targetX - origin.X) / dpi.DpiScaleX;
        tucked = true;
        tuckedOnRightEdge = dockRight;
        automaticMove = false;
        ConfigureTuckedStrip(dockRight);
        TuckedStrip.Visibility = Visibility.Visible;
        TuckedStrip.Opacity = 1;
        // Off-screen content must not leak onto an adjacent monitor.
        DesktopRoot.Clip = new RectangleGeometry(new Rect(dockRight ? 0 : Math.Max(0, ActualWidth - 24), 0, 24, ActualHeight));
        if (showStatus)
            ShowDesktopStatus(UiText.T(dockRight ? "已收纳到右侧 · 移入或点击边条展开" : "已收纳到左侧 · 移入或点击边条展开",
                dockRight ? "Tucked right · hover or click the strip to reveal" : "Tucked left · hover or click the strip to reveal"));
    }
    private void TuckDesktopNow(bool dockRight)
    {
        RevealTucked();
        SetClickThrough(false);
        preferences.DesktopLocked = false;
        preferences.EdgeTuck = true;
        preferences.EdgeSnap = true;
        DockDesktop(dockRight);
        UpdateLayout();
        TuckAtEdge(dockRight, true);
        SavePreferences();
    }
    private void SnapDesktop()
    {
        if (!preferences.EdgeSnap || preferences.DesktopLocked || tucked) return;
        var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;
        var origin = PointToScreen(new Point()); var dpi = VisualTreeHelper.GetDpi(this);
        double snapX = 32 * dpi.DpiScaleX, snapY = 32 * dpi.DpiScaleY;
        if (Math.Abs(origin.X - screen.Left) < snapX) Left += (screen.Left - origin.X) / dpi.DpiScaleX;
        else if (Math.Abs(origin.X + ActualWidth * dpi.DpiScaleX - screen.Right) < snapX) Left += (screen.Right - origin.X - ActualWidth * dpi.DpiScaleX) / dpi.DpiScaleX;
        if (Math.Abs(origin.Y - screen.Top) < snapY) Top += (screen.Top - origin.Y) / dpi.DpiScaleY;
        else if (Math.Abs(origin.Y + ActualHeight * dpi.DpiScaleY - screen.Bottom) < snapY) Top += (screen.Bottom - origin.Y - ActualHeight * dpi.DpiScaleY) / dpi.DpiScaleY;
        SavePreferences();
    }
    private void DesktopTick()
    {
        if (closing || desktopDragging || System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed) return;
        bool full = false;
        var foreground = GetForegroundWindow(); var own = new WindowInteropHelper(this).Handle;
        if (preferences.HideForFullscreen && foreground != own && foreground != IntPtr.Zero && GetWindowRect(foreground, out var rect))
        {
            var screen = System.Windows.Forms.Screen.FromHandle(foreground).Bounds;
            // Shell desktop windows are not fullscreen applications.
            int style = ReadWindowStyle(foreground, -16);
            full = (style & 0x40000000) == 0 && rect.Left <= screen.Left && rect.Top <= screen.Top && rect.Right >= screen.Right && rect.Bottom >= screen.Bottom && foreground != DesktopShell() && !IsDesktopWindow(foreground);
        }
        if (full && IsVisible && !OwnedWindows.Cast<Window>().Any(w => w.IsVisible)) { fullscreenHidden = true; Hide(); }
        else if (!full && fullscreenHidden) { fullscreenHidden = false; Show(); }
        if (!preferences.EdgeTuck || preferences.DesktopLocked || clickThrough || !IsVisible || IsMouseOver || OwnedWindows.Cast<Window>().Any(w => w.IsVisible) || DateTimeOffset.UtcNow - desktopIdle < TimeSpan.FromSeconds(3)) return;
        if (!tucked)
        {
            var area = System.Windows.Forms.Screen.FromHandle(own).WorkingArea;
            var origin = PointToScreen(new Point()); var dpi = VisualTreeHelper.GetDpi(this);
            bool left = Math.Abs(origin.X - area.Left) < 4, right = Math.Abs(origin.X + ActualWidth * dpi.DpiScaleX - area.Right) < 4;
            // Keep 24 DIPs, including the 8–10 DIP transparent window margin.
            // Previously all 8 remaining DIPs could be transparent and unclickable.
            if (left || right) TuckAtEdge(right, false);
        }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, System.Text.StringBuilder name, int count);
    private static bool IsDesktopWindow(IntPtr window)
    {
        var name = new System.Text.StringBuilder(256); GetClassName(window, name, name.Capacity);
        return name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
    }
    [DllImport("user32.dll", EntryPoint = "GetShellWindow")] private static extern IntPtr DesktopShell();
    private async Task RefreshArchivesAsync()
    {
        if (App.Demo || App.Smoke || history == null || DateTimeOffset.UtcNow < archiveDue) return;
        archiveDue = DateTimeOffset.UtcNow.AddHours(1);
        var tasks = measurementTasks?.Tasks.Select(t => t.Copy()).ToArray() ?? Array.Empty<MeasurementTask>();
        var zone = TimeZoneInfo.Local;
        try
        {
            await Task.Run(() => {
                var frames = history.ReadAll();
                string current = HistoryRanges.Period(DateTimeOffset.UtcNow);
                var directory = Path.Combine(App.DataDirectory, "archives"); Directory.CreateDirectory(directory);
                foreach (var month in frames.Select(f => f.Period).Distinct().Where(m => string.CompareOrdinal(m, current) < 0))
                {
                    string path = Path.Combine(directory, month + ".json");
                    File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(ActivitySummary.Month(month, frames, tasks, zone), Store.Json));
                    File.Move(path + ".tmp", path, true);
                }
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { archiveDue = DateTimeOffset.UtcNow.AddMinutes(5); }
    }
    private void OpenWorkspace()
    {
        OpenPreferences(); settingsWindow!.SelectPageForReview("desktop");
    }
    private UIElement BuildDesktopOptions(Window window, string section)
    {
        var options = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        foreach (var type in new[] { typeof(Expander), typeof(CheckBox) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(Control.ForegroundProperty, SkinTheme.Resolve(SkinTheme.Current.Text)));
            options.Resources.Add(type, style);
        }
        void Note(string zh, string en) => options.Children.Add(new TextBlock { Text = UiText.T(zh, en), TextWrapping = TextWrapping.Wrap, FontSize = 11, Margin = new Thickness(0, 4, 0, 12) });
        void Toggle(string zh, string en, bool initial, Action<bool> change)
        {
            var box = new CheckBox { Content = new TextBlock { Text = UiText.T(zh, en), TextWrapping = TextWrapping.Wrap }, IsChecked = initial, Margin = new Thickness(0, 7, 0, 7) };
            box.SetResourceReference(Control.ForegroundProperty, "SkinText");
            box.Click += (_, _) => { change(box.IsChecked == true); SavePreferences(); };
            options.Children.Add(box);
        }
        if (section == "measurement")
        {
            Toggle("结束任务后等待数据更新", "Wait for source updates after ending a task", preferences.DelayedSettlement, v => preferences.DelayedSettlement = v);
            Note("等待 3 次成功采样，最多 10 分钟；可能包含并行任务用量。", "Wait for 3 successful samples, up to 10 minutes; concurrent usage may be included.");
            options.Children.Add(InsightUi.Button(UiText.T("打开任务计量记录…", "Open task measurements…"), () => OpenTasks(this, new RoutedEventArgs())));
            return options;
        }
        if (section == "appearance")
        {
            Toggle("迷你模式显示双指标", "Show two metrics in mini mode", preferences.MiniDualMetrics, v => { preferences.MiniDualMetrics = v; RefreshUsageProgress(); });
            Toggle("隐藏金额（屏幕分享）", "Hide amounts (screen sharing)", preferences.HideAmounts, v => { if (preferences.HideAmounts != v) ToggleAmountPrivacy(); });
            Toggle("用量增长脉冲", "Usage growth pulse", preferences.GrowthPulse, v => { preferences.GrowthPulse = v; RefreshDisplayEffects(); RefreshNumericState(); });
            options.Children.Add(InsightUi.Button(UiText.T("预览用量增长脉冲", "Preview usage growth pulse"), PreviewGrowthPulse));
            Toggle("边框显示数据新鲜度", "Show data freshness in the frame", preferences.FreshnessAppearance, v => { preferences.FreshnessAppearance = v; RefreshNumericState(); });
            var presets = new StackPanel(); AddDesktopPresets(presets, window);
            options.Children.Add(new Expander { Header = UiText.T("桌面显示预设", "Desktop display presets"), Content = presets, Margin = new Thickness(0, 12, 0, 0) });
            return options;
        }
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        foreach (bool right in new[] { false, true })
            actions.Children.Add(InsightUi.Button(UiText.T(right ? "靠右并收纳" : "靠左并收纳", right ? "Tuck right" : "Tuck left"), () => { window.Close(); TuckDesktopNow(right); }));
        options.Children.Add(actions);
        options.Children.Add(InsightUi.Button(UiText.T("移回中央，停止自动隐藏", "Center and stop automatic hiding"), () => { window.Close(); CenterDesktop(); }));
        Note("收纳后，移入屏幕边条即可展开。拖回中央后保持展开。", "Hover over the edge strip to reveal. Drag back to the center to keep the panel open.");
        Toggle("拖近屏幕边缘时吸附", "Snap near screen edges", preferences.EdgeSnap, v => { preferences.EdgeSnap = v; if (!v) { preferences.EdgeTuck = false; RevealTucked(); settingsWindow?.RebuildDesktopPage(); } });
        Toggle("贴边后自动收纳", "Automatically tuck at a screen edge", preferences.EdgeTuck, v => { preferences.EdgeTuck = v; if (v) preferences.EdgeSnap = true; else RevealTucked(); settingsWindow?.RebuildDesktopPage(); });
        Note("开启后：贴近左 / 右边缘，松开鼠标并移开 3 秒。拖动和打开设置时暂停收纳。", "When enabled: dock left or right, release and move the pointer away for 3 seconds. Tuck pauses during dragging and while settings are open.");
        Toggle("锁定窗口位置", "Lock window position", preferences.DesktopLocked, v => { RevealTucked(); preferences.DesktopLocked = v; });
        var advanced = new StackPanel();
        var hide = new CheckBox { Content = UiText.T("全屏应用时隐藏", "Hide for fullscreen apps"), IsChecked = preferences.HideForFullscreen, Margin = new Thickness(0, 8, 0, 8) };
        hide.Click += (_, _) => { preferences.HideForFullscreen = hide.IsChecked == true; SavePreferences(); }; advanced.Children.Add(hide);
        advanced.Children.Add(InsightUi.Button(UiText.T("开启鼠标穿透", "Enable click-through"), () => { window.Close(); SetClickThrough(true); }));
        advanced.Children.Add(new TextBlock { Text = UiText.T("穿透后通过托盘或 Ctrl+Alt+P 恢复操作。", "Restore interaction from the tray or Ctrl+Alt+P."), FontSize = 11, TextWrapping = TextWrapping.Wrap });
        options.Children.Add(new Expander { Header = UiText.T("高级窗口行为", "Advanced window behavior"), Content = advanced, Margin = new Thickness(0, 16, 0, 0) });
        return options;
    }
    private async Task LoadActivityAsync(StackPanel panel, Window owner)
    {
        var labelStyle = new Style(typeof(TextBlock)); labelStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, SkinTheme.Resolve(SkinTheme.Current.Text))); panel.Resources[typeof(TextBlock)] = labelStyle;
        var statusText = new TextBlock { Text = UiText.T("正在读取历史…", "Loading history…") }; panel.Children.Add(statusText);
        var tasks = measurementTasks?.Tasks.Select(t => t.Copy()).ToArray() ?? Array.Empty<MeasurementTask>();
        var zone = TimeZoneInfo.Local;
        try
        {
            var frames = await Task.Run(() => demoHistory ?? history?.ReadAll() ?? Array.Empty<HistoryFrame>());
            if (!owner.IsVisible) return;
            statusText.Text = ""; statusText.Visibility = Visibility.Collapsed;
            var months = frames.Select(f => f.Period).Append(HistoryRanges.Period(DateTimeOffset.UtcNow)).Distinct().OrderByDescending(m => m).ToArray();
            var select = new ComboBox { ItemsSource = months, SelectedIndex = 0, Width = 124, FontWeight = FontWeights.Normal }; select.SetResourceReference(FrameworkElement.StyleProperty, "SkinComboBoxStyle");
            var content = new StackPanel();
            var report = new Border { Child = content, Background = SkinTheme.Resolve(SkinTheme.Current.Chart), BorderBrush = SkinTheme.Resolve(SkinTheme.Current.Edge), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), Padding = new Thickness(14), Margin = new Thickness(0, 14, 2, 8) }; panel.Children.Add(report);
            var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(select, Dock.Left); heading.Children.Add(select);
            var total = new TextBlock { FontSize = 23, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = SkinTheme.Resolve(SkinTheme.Current.Accent) };
            heading.Children.Add(total); content.Children.Add(heading);
            var calendar = new StackPanel(); content.Children.Add(calendar);
            async Task RenderMonth()
            {
                string? month = select.SelectedItem as string; if (month == null) return;
                var archive = await Task.Run(() => ActivitySummary.Month(month, frames, tasks, zone));
                if ((string?)select.SelectedItem != month || !owner.IsVisible) return;
                calendar.Children.Clear();
                string amount = preferences.HideAmounts ? "••••" : archive.LastObserved?.ToString("C2", Us) ?? "—";
                total.Text = amount;
                total.ToolTip = UiText.T($"最近观测累计 · UTC 配额月 {month}\n采样覆盖约 {archive.CoveragePercent:0.0}% · 已保存任务 {archive.SavedTasks}\n最近采样 {archive.LastSampleUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—"}\n观测摘要；缺失数据未补算。", $"Last observed total · UTC quota month {month}\nEstimated coverage {archive.CoveragePercent:0.0}% · Saved tasks {archive.SavedTasks}\nLast sample {archive.LastSampleUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—"}\nObserved summary; missing data is not estimated.");
                var weekdays = new System.Windows.Controls.Primitives.UniformGrid { Columns = 7, Margin = new Thickness(0, 0, 0, 6) };
                foreach (string name in UiText.T("一,二,三,四,五,六,日", "M,T,W,T,F,S,S").Split(','))
                    weekdays.Children.Add(new TextBlock { Text = name, TextAlignment = TextAlignment.Center, FontSize = 11, FontWeight = FontWeights.Normal, Foreground = SkinTheme.Resolve(SkinTheme.Current.Faint) });
                calendar.Children.Add(weekdays);
                var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 7 };
                var detail = new TextBlock { Tag = "heatmap-detail", FontSize = 12, FontWeight = FontWeights.Normal, Margin = new Thickness(3, 10, 3, 0), Foreground = SkinTheme.Resolve(SkinTheme.Current.Muted), TextWrapping = TextWrapping.Wrap };
                var cells = new List<(DailyUsage Day, Border Cell)>();
                void SelectDay(DailyUsage selected)
                {
                    string dailyAmount = selected.Delta.HasValue ? preferences.HideAmounts ? "••••" : selected.Delta.Value.ToString("C2", Us) : UiText.T("无连续采样", "No continuous samples");
                    detail.Text = UiText.T($"{selected.Date:MM/dd} · 当日增量 {dailyAmount} · {selected.Samples} 次采样", $"{selected.Date:MMM d} · Daily change {dailyAmount} · {selected.Samples} samples");
                    foreach (var item in cells)
                    {
                        bool chosen = item.Day.Date == selected.Date, current = item.Day.Date == DateTime.Today;
                        item.Cell.BorderBrush = SkinTheme.Resolve(chosen || current ? SkinTheme.Current.Accent : SkinTheme.Current.Edge);
                        item.Cell.BorderThickness = new Thickness(chosen ? 2 : current ? 1 : .4);
                    }
                }
                var tileTemplate = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><ContentPresenter/><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Opacity' Value='0.85'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
                decimal maximum = Math.Max(.01m, archive.Days.Max(d => d.Delta ?? 0));
                var accent = (Color)ColorConverter.ConvertFromString(SkinTheme.Current.Accent);
                var surface = (Color)ColorConverter.ConvertFromString(SkinTheme.Current.Panel);
                SolidColorBrush Heat(double strength) => new(Color.FromRgb((byte)(surface.R + (accent.R - surface.R) * strength), (byte)(surface.G + (accent.G - surface.G) * strength), (byte)(surface.B + (accent.B - surface.B) * strength)));
                int offset = ((int)archive.Days[0].Date.DayOfWeek + 6) % 7;
                for (int i = 0; i < offset; i++) grid.Children.Add(new Border());
                foreach (var day in archive.Days)
                {
                    bool future = day.Date > DateTime.Today, today = day.Date == DateTime.Today;
                    double strength = day.Delta.HasValue ? .14 + .62 * Math.Sqrt((double)(day.Delta.Value / maximum)) : 0;
                    string value = !day.Delta.HasValue ? UiText.T("无连续采样", "No continuous samples") : preferences.HideAmounts ? "••••" : day.Delta.Value.ToString("C2", Us);
                    var tile = new Grid();
                    var ink = strength > .52 ? SkinTheme.Resolve(SkinTheme.Current.Chart) : SkinTheme.Resolve(future ? SkinTheme.Current.Faint : SkinTheme.Current.Text);
                    var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    labels.Children.Add(new TextBlock { Text = day.Date.ToString("yyyy-MM") == month ? day.Date.Day.ToString() : day.Date.ToString("M/d"), FontSize = 12, FontWeight = FontWeights.Medium, TextAlignment = TextAlignment.Center, Foreground = ink });
                    if (day.Delta.HasValue) labels.Children.Add(new TextBlock { Text = preferences.HideAmounts ? "••••" : day.Delta.Value.ToString("0.00", Us), FontSize = 10, FontWeight = FontWeights.Normal, TextAlignment = TextAlignment.Center, Foreground = ink, TextTrimming = TextTrimming.CharacterEllipsis });
                    tile.Children.Add(labels);
                    if (!day.Delta.HasValue && !future) tile.Children.Add(new Border { Width = 3, Height = 3, CornerRadius = new CornerRadius(2), Background = SkinTheme.Resolve(SkinTheme.Current.Faint), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 4) });
                    var cell = new Border { Height = 38, CornerRadius = new CornerRadius(7), Margin = new Thickness(2), Background = Heat(strength), BorderBrush = SkinTheme.Resolve(today ? SkinTheme.Current.Accent : SkinTheme.Current.Edge), BorderThickness = new Thickness(today ? 1 : .4), Opacity = future ? .35 : 1, Child = tile };
                    cells.Add((day, cell));
                    var button = new Button { Content = cell, Template = tileTemplate, Padding = new Thickness(0), Tag = day, Cursor = System.Windows.Input.Cursors.Hand, ToolTip = UiText.T($"本机日期 {day.Date:yyyy-MM-dd}\n观测增量：{value}\n{day.Samples} 次采样 · UTC 配额月 {month}", $"Local date {day.Date:yyyy-MM-dd}\nObserved change: {value}\n{day.Samples} samples · UTC quota month {month}") };
                    System.Windows.Automation.AutomationProperties.SetName(button, UiText.T($"{day.Date:yyyy-MM-dd} 当日增量 {value}", $"{day.Date:yyyy-MM-dd} Daily change {value}"));
                    button.Click += (_, _) => SelectDay(day); grid.Children.Add(button);
                }
                calendar.Children.Add(grid);
                calendar.Children.Add(detail);
                SelectDay(archive.Days.FirstOrDefault(d => d.Date == DateTime.Today) ?? archive.Days.LastOrDefault(d => d.Delta.HasValue) ?? archive.Days[0]);
                var legend = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 3, 0) };
                TextBlock Key(string text) => new() { Text = text, FontSize = 10, FontWeight = FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, Foreground = SkinTheme.Resolve(SkinTheme.Current.Faint) };
                legend.Children.Add(Key(UiText.T("· 缺采样    少 ", "· Missing    Less ")));
                foreach (double level in new[] { .14, .28, .44, .60, .76 }) legend.Children.Add(new Border { Width = 11, Height = 11, CornerRadius = new CornerRadius(3), Margin = new Thickness(2, 0, 2, 0), Background = Heat(level) });
                legend.Children.Add(Key(UiText.T(" 多", " More"))); calendar.Children.Add(legend);
            }
            select.SelectionChanged += async (_, _) => { try { await RenderMonth(); } catch (Exception e) when (e is IOException or ArgumentException or FormatException) { statusText.Visibility = Visibility.Visible; statusText.Text = UiText.T("月度摘要暂不可用。", "Monthly summary unavailable."); } };
            await RenderMonth();
            panel.Children.Add(InsightUi.Button(UiText.T("复制月度卡片图片", "Copy monthly card image"), () => {
                try { report.UpdateLayout(); var image = new System.Windows.Media.Imaging.RenderTargetBitmap(Math.Max(1, (int)report.ActualWidth * 2), Math.Max(1, (int)report.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32); image.Render(report); Clipboard.SetImage(image); }
                catch (System.Runtime.InteropServices.COMException) { statusText.Visibility = Visibility.Visible; statusText.Text = UiText.T("剪贴板忙，请重试。", "Clipboard busy; retry."); }
            }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { statusText.Text = UiText.T("历史暂不可读，原始数据保留。", "History unavailable; original data retained."); }
    }
}
