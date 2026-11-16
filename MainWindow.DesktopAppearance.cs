using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CopilotPulse;

public partial class MainWindow
{
    private decimal? pulseTotal;
    private DateTimeOffset? pulseTime;
    private string freshnessState = "";
    private int growthPulseCount;
    private int growthPulseSequence;
    private Storyboard? growthStoryboard;
    private int desktopStatusSequence;
    private void RefreshOutlineGeometry()
    {
        bool mini = preferences.DisplayMode == "mini";
        FreshnessFrame.Margin = GrowthFrame.Margin = new Thickness(mini ? 9 : 10);
        FreshnessFrame.CornerRadius = GrowthFrame.CornerRadius = mini
            ? new CornerRadius(DisplayArt.Radius(SkinTheme.Current)) : Surface.CornerRadius;
    }
    private void RefreshDesktopAppearance(string state)
    {
        RefreshOutlineGeometry();
        string kind = paused ? "paused" : data == null && state is ("connecting" or "live" or "demo") ? "empty" : state;
        if (!GrowthPulseAllowed()) StopGrowthPulse();
        freshnessState = kind;
        string color = kind switch {
            "live" or "demo" => SkinTheme.Current.Accent,
            "syncing" => "#79B8F7", "paused" or "empty" => SkinTheme.Current.Faint,
            "login" or "runtime" or "network" or "layout" => "#F09380", _ => "#E1B663" };
        FreshnessFrame.BorderBrush = Brush(color);
        FreshnessFrame.Opacity = preferences.FreshnessAppearance ? kind is "live" or "demo" ? .40 : .85 : 0;
        FreshnessFrame.BorderThickness = new Thickness(kind is "stale" or "network" or "layout" or "login" ? 2 : 1);
        StatusDot.Fill = Brush(color);
        string label = kind switch {
            "live" or "demo" => UiText.T("数据新鲜", "Fresh reading"), "syncing" => UiText.T("正在采样", "Sampling"),
            "paused" => UiText.T("采样已暂停", "Sampling paused"), "empty" => UiText.T("等待首次采样", "Awaiting first sample"),
            "login" => UiText.T("需要登录", "Sign-in required"), _ => UiText.T("数据延迟或连接异常", "Delayed reading or connection issue") };
        string hint = label + (data == null ? "" : UiText.T(" · 最近成功 ", " · Last success ") + data.FetchedAt.ToLocalTime().ToString("MM/dd HH:mm:ss"));
        Surface.ToolTip = hint; MiniHost.ToolTip = hint; StatusDot.ToolTip = hint;
        if (data != null)
        {
            // Only a newer source sample within the same UTC quota month can pulse.
            if (pulseTotal.HasValue && pulseTime.HasValue && data.FetchedAt > pulseTime && data.Total > pulseTotal
                && HistoryRanges.Period(data.FetchedAt) == HistoryRanges.Period(pulseTime.Value))
                PulseUsageChange(data.Total - pulseTotal.Value, data.FetchedAt);
            if (!pulseTime.HasValue || data.FetchedAt >= pulseTime) { pulseTotal = data.Total; pulseTime = data.FetchedAt; }
        }
    }
    private bool GrowthPulseAllowed() =>
        preferences.GrowthPulse && !preferences.ReduceMotion && !preferences.HideAmounts
        && SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast && SkinTheme.Current.Id != "eink";
    private void StopGrowthPulse()
    {
        growthPulseSequence++;
        growthStoryboard?.Stop();
        growthStoryboard = null;
        GrowthBadge.BeginAnimation(OpacityProperty, null);
        GrowthBadgeTransform.BeginAnimation(TranslateTransform.YProperty, null);
        GrowthBadge.Visibility = Visibility.Collapsed;
        GrowthBadge.Opacity = 0;
        GrowthBadgeTransform.Y = 8;
        GrowthFrame.BeginAnimation(OpacityProperty, null);
        GrowthFrame.Opacity = 0;
    }
    private void PulseUsageChange(decimal? delta = null, DateTimeOffset? sampledAt = null, bool preview = false)
    {
        StopGrowthPulse();
        if (!GrowthPulseAllowed()) return;
        growthPulseCount++;
        decimal shown = delta ?? .12m;
        var local = (sampledAt ?? DateTimeOffset.Now).ToLocalTime();
        GrowthText.Text = UiText.T("新增 ", "Added ") + "+" + shown.ToString("C2", Us);
        GrowthMeta.Text = preview ? UiText.T("效果预览 · 不计入用量", "Effect preview · not recorded") : UiText.T($"采样 {local:HH:mm:ss}", $"Sampled {local:HH:mm:ss}");
        GrowthBadge.Visibility = Visibility.Visible;
        GrowthBadge.Opacity = 0;
        GrowthBadgeTransform.Y = 8;

        var storyboard = new Storyboard();
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var hold = new DoubleAnimation(1, 1, TimeSpan.FromMilliseconds(1800)) { BeginTime = TimeSpan.FromMilliseconds(180) };
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(320)) { BeginTime = TimeSpan.FromMilliseconds(1980), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var slide = new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(fadeIn, GrowthBadge); Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        Storyboard.SetTarget(hold, GrowthBadge); Storyboard.SetTargetProperty(hold, new PropertyPath(UIElement.OpacityProperty));
        Storyboard.SetTarget(fadeOut, GrowthBadge); Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        Storyboard.SetTarget(slide, GrowthBadgeTransform); Storyboard.SetTargetProperty(slide, new PropertyPath(TranslateTransform.YProperty));
        storyboard.Children.Add(fadeIn); storyboard.Children.Add(hold); storyboard.Children.Add(fadeOut); storyboard.Children.Add(slide);
        int sequence = ++growthPulseSequence;
        storyboard.Completed += (_, _) =>
        {
            if (sequence != growthPulseSequence) return;
            growthStoryboard = null;
            GrowthBadge.Visibility = Visibility.Collapsed;
            GrowthBadge.Opacity = 0;
        };
        growthStoryboard = storyboard;
        Timeline.SetDesiredFrameRate(storyboard, preferences.AnimationFps);
        storyboard.Begin();

        GrowthFrame.BorderBrush = Brush(SkinTheme.Current.Accent);
        var animation = new DoubleAnimation(.95, 0, TimeSpan.FromMilliseconds(2300));
        Timeline.SetDesiredFrameRate(animation, preferences.AnimationFps);
        GrowthFrame.BeginAnimation(OpacityProperty, animation);
    }
    private async void ShowDesktopStatus(string text)
    {
        int sequence = ++desktopStatusSequence;
        DesktopStatusText.Text = text;
        DesktopStatusToast.Visibility = Visibility.Visible;
        DesktopStatusToast.BeginAnimation(OpacityProperty, null);
        DesktopStatusToast.Opacity = 0;
        if (!preferences.ReduceMotion && SystemParameters.ClientAreaAnimation)
        {
            var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(160));
            Timeline.SetDesiredFrameRate(fade, preferences.AnimationFps);
            DesktopStatusToast.BeginAnimation(OpacityProperty, fade);
        }
        else DesktopStatusToast.Opacity = 1;
        await Task.Delay(1500);
        if (sequence != desktopStatusSequence) return;
        if (!preferences.ReduceMotion && SystemParameters.ClientAreaAnimation)
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
            Timeline.SetDesiredFrameRate(fade, preferences.AnimationFps);
            fade.Completed += (_, _) => { if (sequence == desktopStatusSequence) DesktopStatusToast.Visibility = Visibility.Collapsed; };
            DesktopStatusToast.BeginAnimation(OpacityProperty, fade);
        }
        else DesktopStatusToast.Visibility = Visibility.Collapsed;
    }
    private async void PreviewGrowthPulse()
    {
        settingsWindow?.Close();
        RevealTucked();
        Show(); Activate();
        await Task.Delay(220);
        if (!GrowthPulseAllowed())
        {
            ShowDesktopStatus(UiText.T("脉冲未启用：请检查增长脉冲、隐藏金额、减少动画及系统动画设置。", "Pulse unavailable: check growth pulse, amount privacy, reduced motion and system animation settings."));
            return;
        }
        PulseUsageChange(.12m, DateTimeOffset.Now, preview: true);
    }
    private void DockDesktop(bool right)
    {
        RevealTucked(); UpdateLayout();
        var area = System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).WorkingArea;
        var origin = PointToScreen(new Point()); var dpi = VisualTreeHelper.GetDpi(this);
        Left += ((right ? area.Right - ActualWidth * dpi.DpiScaleX : area.Left) - origin.X) / dpi.DpiScaleX;
        desktopIdle = DateTimeOffset.UtcNow; SavePreferences();
    }
    private void ApplyDesktopPreset(string preset)
    {
        RevealTucked(); SetClickThrough(false); preferences.DesktopLocked = false;
        preferences.EdgeSnap = true; preferences.EdgeTuck = preset == "focus";
        preferences.HideForFullscreen = preset == "focus";
        preferences.MiniDualMetrics = true;
        SelectMiniMetric(preset == "focus" ? HasActiveMeasurement ? "task" : "today" : "total", false);
        preferences.MiniSecondaryMetric = preset == "focus" ? "total" : "today";
        if (preferences.HideAmounts != (preset == "sharing")) ToggleAmountPrivacy();
        preferences.Topmost = preset != "quiet"; Topmost = preferences.Topmost;
        preferences.ReduceMotion = preset == "quiet";
        SetDisplayMode(preset is "focus" or "quiet" ? "mini" : "compact");
        RefreshDisplayEffects(); RefreshNumericState(); RefreshUsageProgress();
        _ = RefreshMiniHistoryAsync(); SavePreferences();
        settingsWindow?.RebuildDesktopPage();
    }
    private void AddDesktopPresets(StackPanel options, Window owner)
    {
        options.Children.Add(new TextBlock { Text = UiText.T("桌面显示预设", "Desktop display presets"), FontSize = 16, FontWeight = FontWeights.SemiBold });
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Margin = new Thickness(0, 8, 0, 8) };
        foreach (var entry in new[] { ("glance", "日常看板", "Daily glance"), ("focus", "专注计量", "Focus measurement"), ("quiet", "安静桌面", "Quiet desktop"), ("sharing", "屏幕分享", "Screen sharing") })
            grid.Children.Add(InsightUi.Button(UiText.T(entry.Item2, entry.Item3), () => ApplyDesktopPreset(entry.Item1)));
        options.Children.Add(grid);
        options.Children.Add(new TextBlock { Text = UiText.T("日常：紧凑累计＋今日 · 专注：迷你任务＋累计、靠边收纳 · 安静：关闭动画与置顶 · 分享：隐藏金额", "Daily: compact total + today · Focus: mini task + total, edge tuck · Quiet: no motion or topmost · Sharing: hide amounts"), FontSize = 11, TextWrapping = TextWrapping.Wrap });

    }
}
