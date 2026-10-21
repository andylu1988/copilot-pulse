using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CopilotPulse;

public sealed record DiagnosticCheck(string Name, string State, string Detail);
public sealed record DiagnosticReport(DateTimeOffset CheckedUtc, string Version, string TimeZone, bool Demo, IReadOnlyList<DiagnosticCheck> Checks);

internal static class InsightUi
{
    internal static TextBlock Text(string text, double size = 13) { var label = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 8) }; label.SetResourceReference(TextBlock.ForegroundProperty, "SkinText"); return label; }
    internal static Button Button(string label, Action click)
    {
        var b = new Button { Content = label, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 5, 8, 5) };
        b.SetResourceReference(FrameworkElement.StyleProperty, "GlassButtonStyle");
        b.SetResourceReference(Control.ForegroundProperty, "SkinText"); b.SetResourceReference(Control.BackgroundProperty, "SkinHover"); b.Click += (_, _) => click(); return b;
    }
    internal static Border Card(UIElement child) { var b = new Border { Child = child, Padding = new Thickness(18), Margin = new Thickness(0, 6, 8, 10), CornerRadius = new CornerRadius(12) }; b.SetResourceReference(Border.BackgroundProperty, "SkinChart"); return b; }
    internal static void SavePng(FrameworkElement element, string path, double width = 920)
    {
        element.Measure(new Size(width, double.PositiveInfinity)); element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height)); element.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * 1.5), (int)Math.Ceiling(element.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }
    internal static void Export(Window owner, FrameworkElement report)
    {
        var save = new SaveFileDialog { Filter = "PNG (*.png)|*.png", FileName = "CopilotPulse-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png" };
        if (save.ShowDialog(owner) != true) return;
        try { SavePng(report, save.FileName); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(owner, UiText.T("无法写入图片，请更换保存位置。", "Cannot write image. Choose another folder.")); }
    }
    internal static StackPanel Report(string heading)
    {
        var p = new StackPanel { Margin = new Thickness(0), Background = SkinTheme.Resolve("#101D30") }; p.SetResourceReference(TextBlock.ForegroundProperty, "SkinText");
        p.Children.Add(new Border { Child = Text("Copilot Pulse · " + heading, 23), Padding = new Thickness(20) }); return p;
    }
    internal static FrameworkElement TaskReport(MeasurementTask t, IReadOnlyList<HistoryFrame> frames, DateTimeOffset now, bool anonymous)
    {
        var m = UsageInsights.Interval(t, frames, now); var p = Report(UiText.T("任务计量报告", "Task measurement report"));
        p.Children.Add(Card(Text(IntervalText(t, m, anonymous)))); var chart = new HistoryChart { Height = 250, Margin = new Thickness(18) };
        chart.SetData(m.Delta.HasValue ? HistoryMath.RecordedSeries(frames.Where(f => f.Time >= t.StartUtc && f.Time <= (t.EndUtc ?? now)).ToArray(), null) : Array.Empty<HistoryPoint>());
        p.Children.Add(chart); p.Children.Add(Card(Text(UiText.T("观测增量可用于估计单次任务花费；源页面可能延迟，非精确账单。", "Observed deltas help estimate a task's cost; delayed source readings are not exact billing."), 11))); return p;
    }
    internal static string IntervalText(MeasurementTask t, IntervalInsight m, bool anonymous = false) =>
        (anonymous ? UiText.T("匿名任务", "Anonymous task") : UiText.TaskName(t.Name)) + "\n" +
        $"{t.StartUtc.ToLocalTime():yyyy-MM-dd HH:mm} → {(t.EndUtc ?? DateTimeOffset.UtcNow).ToLocalTime():yyyy-MM-dd HH:mm}\n" +
        UiText.T($"观测增量 {Money(m.Delta)} · 持续 {m.Duration.TotalMinutes:0.#} 分钟\n单次最大变化 {Money(m.PeakChange)} · {m.Samples} 个有效采样", $"Observed delta {Money(m.Delta)} · Duration {m.Duration.TotalMinutes:0.#} min\nLargest sample change {Money(m.PeakChange)} · {m.Samples} valid samples");
    internal static string Money(decimal? n) => n.HasValue ? HistoryChart.Signed(n.Value) : "—";
}

internal sealed class InsightsWindow : Window
{
    private readonly Func<Consumption?> consumption;
    private readonly StackPanel forecast = new();
    private readonly UtilitySkin skin;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(60) };
    private bool closed;
    internal InsightsWindow(TaskRepository? tasks, Func<Task<IReadOnlyList<HistoryFrame>>> load,
        Func<Consumption?> consumption, Func<Task<DiagnosticReport>> diagnose, int selected = 1)
    {
        this.consumption = consumption;
        Width = 560; Height = 570; MinWidth = 460; MinHeight = 390;
        MaxHeight = SystemParameters.WorkArea.Height - 24;
        Title = UiText.T("Copilot Pulse · 用量预估", "Copilot Pulse · Usage forecast");
        Icon = BrandIcon.Window; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(22) };
        var header = new DockPanel { Margin = new Thickness(0,0,0,12) };
        var close = InsightUi.Button("✕", Close); close.ToolTip = UiText.T("关闭", "Close");
        close.Width = 36; close.HorizontalAlignment = HorizontalAlignment.Right;
        DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
        header.Children.Add(InsightUi.Text(UiText.T("用量预估", "Usage forecast"), 24));
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        root.Children.Add(new ScrollViewer { Content = forecast, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        skin = new UtilitySkin(this, root);
        Loaded += async (_, _) => { await ReloadAsync(); timer.Start(); };
        timer.Tick += async (_, _) => await ReloadAsync();
        Closed += (_, _) => { closed = true; timer.Stop(); };
    }
    internal void ApplySkin() => skin.Apply();
    internal Task ReloadAsync() { if (!closed) RenderForecast(); return Task.CompletedTask; }
    private void RenderForecast() => RenderForecast(consumption(), DateTimeOffset.UtcNow);
    internal void ReviewForecast(Consumption value, IReadOnlyList<HistoryFrame> history, DateTimeOffset now, Preferences? settings = null) => RenderForecast(value, now, settings);
    private void RenderForecast(Consumption? value, DateTimeOffset now, Preferences? settings = null)
    {
        forecast.Children.Clear();
        var reset = HistoryRanges.Start("month", now).AddMonths(1);
        forecast.Children.Add(InsightUi.Text(UiText.T("Overall · 本月用量预估", "Overall · Monthly usage forecast"), 21));
        forecast.Children.Add(InsightUi.Text(UiText.T($"距重置 {(reset-now).TotalDays:0.0} 天 · 本机 {reset.ToLocalTime():MM/dd HH:mm}（UTC {reset:MM/dd HH:mm}）", $"{(reset-now).TotalDays:0.0} days to reset · Local {reset.ToLocalTime():MM/dd HH:mm} (UTC {reset:MM/dd HH:mm})"), 12));
        if (value?.Ok != true) { forecast.Children.Add(InsightUi.Text(UiText.T("没有有效源读数，请先登录并采样。", "No valid source reading. Sign in and refresh the source."))); return; }
        int cadence = Owner is MainWindow main ? main.ForecastRefreshSeconds : 60;
        var f = QuotaForecasting.MonthlyOverall(value, now, cadence, settings ?? (Owner as MainWindow)?.ForecastPreferences);
        var panel = new StackPanel();
        if (f.State != ForecastState.Ready)
        {
            panel.Children.Add(InsightUi.Text(UiText.T("预测暂不可用", "Forecast unavailable"), 24));
            var warning = InsightUi.Text(f.State == ForecastState.Insufficient
                ? UiText.T("UTC 配额月刚开始，请等待下次采样。", "The UTC quota month just started. Wait for the next sample.") : ForecastReason(f.State), 13);
            warning.Foreground = Brushes.Khaki; panel.Children.Add(warning);
        }
        else
        {
            panel.Children.Add(InsightUi.Text(UiText.T("预计月末累计用量", "Projected month-end usage"), 12));
            var projected = InsightUi.Text($"≈ ${f.ProjectedCost:N2}", 36); projected.FontWeight = FontWeights.SemiBold; panel.Children.Add(projected);
            string risk = f.UsesAlertThreshold ? f.Exhausted ? UiText.T("已达到提醒阈值", "Alert threshold reached")
                : f.DepletesBeforeReset == true ? UiText.T($"按本月平均速度预计达到提醒阈值 · {f.Depletion!.Value.ToLocalTime():MM/dd HH:mm}", $"Expected to reach alert threshold at the monthly average rate · {f.Depletion!.Value.ToLocalTime():MM/dd HH:mm}")
                : f.DepletesBeforeReset == false ? UiText.T("按本月平均速度，月末预计不超出提醒阈值", "Month-end usage is expected to stay below the alert threshold")
                : UiText.T("已读取百分比阈值；页面缺少数值额度，无法换算 Overall 金额，请设置金额阈值。", "Percentage threshold loaded; source limits are missing. Set an amount threshold for Overall.")
                : f.Exhausted ? UiText.T("总体额度已耗尽", "Overall quota exhausted")
                : f.DepletesBeforeReset == true ? UiText.T($"按本月平均速度会耗尽 · 预计 {f.Depletion!.Value.ToLocalTime():MM/dd HH:mm}", $"Will run out at the monthly average rate · Estimated {f.Depletion!.Value.ToLocalTime():MM/dd HH:mm}")
                : f.DepletesBeforeReset == false ? UiText.T("按本月平均速度，重置前不会耗尽", "Not expected to run out before reset at the monthly average rate")
                : UiText.T("总体额度未提供，无法判断是否耗尽", "Overall quota unavailable; depletion cannot be determined");
            var status = InsightUi.Text(risk, 17); status.FontWeight = FontWeights.SemiBold;
            status.Foreground = f.DepletesBeforeReset == true ? Brushes.Salmon : f.DepletesBeforeReset == false ? Brushes.LightGreen : Brushes.Khaki; panel.Children.Add(status);
            if (f.RemainingPercent.HasValue)
            {
                panel.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = (double)(100 - f.RemainingPercent.Value), Height = 7,
                    Margin = new Thickness(0,8,0,10), Foreground = f.DepletesBeforeReset == true ? Brushes.Salmon : Brushes.MediumSeaGreen, Background = Brushes.DimGray });
                panel.Children.Add(InsightUi.Text(f.UsesAlertThreshold
                    ? UiText.T($"提醒阈值 ${f.Target:N2} · 已达 {(f.Target > 0 ? value.Total/f.Target.Value*100 : 100):0.##}% · 距阈值 ${f.Remaining:N2}", $"Alert threshold ${f.Target:N2} · {(f.Target > 0 ? value.Total/f.Target.Value*100 : 100):0.##}% reached · ${f.Remaining:N2} to threshold")
                    : UiText.T($"已用 {100-f.RemainingPercent.Value:0.##}% · 剩余 ${f.Remaining:N2} · 额度按页面数值上限合计", $"Used {100-f.RemainingPercent.Value:0.##}% · Remaining ${f.Remaining:N2} · Quota sums explicit source limits"), 11));
            }
            panel.Children.Add(InsightUi.Text(UiText.T($"本月累计 ${value.Total:N2} · 日均 ${f.Rate!.PerHour*24:N2}", $"Month-to-date ${value.Total:N2} · Daily average ${f.Rate!.PerHour*24:N2}"), 12));
            panel.Children.Add(InsightUi.Text(UiText.T("计算：本月累计 ÷ 本月已过时间 × 本月总时长。按 UTC 配额月计算，无需历史采样。", "Calculation: month-to-date total ÷ elapsed month × full month. Uses the UTC quota month; no history required."), 11));
        }
        panel.Children.Add(InsightUi.Text(UiText.T($"源读数：{value.FetchedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · 页面数据可能延迟，预测仅供参考。", $"Source reading: {value.FetchedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · Source data may lag; estimates are indicative."), 11));
        forecast.Children.Add(InsightUi.Card(panel));
    }
    internal string ForecastReviewText()
    {
        var texts = new List<string>();
        void Walk(UIElement e)
        {
            if (e is TextBlock t) texts.Add(t.Text);
            if (e is Panel p) foreach (UIElement child in p.Children) Walk(child);
            if (e is Border b && b.Child != null) Walk(b.Child);
        }
        Walk(forecast); return string.Join("\n", texts);
    }
    private static string ForecastReason(ForecastState state) => state switch
    {
        ForecastState.InvalidReading => UiText.T("源时间或数值无效，已暂停预测；请检查系统时钟并重新采样。", "Invalid source time or values. Prediction paused; check the system clock and refresh."),
        ForecastState.WrongMonth => UiText.T("读数不属于当前 UTC 配额月，已暂停预测；请立即采样。", "Reading is outside this UTC quota month. Prediction paused; refresh the source."),
        ForecastState.Stale => UiText.T("源读数已延迟，已暂停预测。请使用主界面刷新按钮更新数据。", "Source reading is stale. Prediction paused. Refresh the source from the main panel."),
        ForecastState.MissingAccount => UiText.T("所选覆盖内有账户缺失，无法比较。请选择更短区间或继续采样。", "Account missing within selected coverage. Choose a shorter range or collect more samples."),
        ForecastState.Correction => UiText.T("所选覆盖内有金额下调，已暂停外推，避免把数据修正当成消耗。", "Downward correction within selected coverage. Extrapolation paused to avoid treating corrections as usage."),
        _ => UiText.T("所选区间内读数不足：至少需要两次可比较读数，跨度不少于一分钟。", "Insufficient coverage: at least two comparable readings spanning one minute are required.")
    };

}
