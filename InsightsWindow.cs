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
        var m = UsageInsights.Interval(t, frames, now); var p = Report(UiText.T("任务观测报告", "Task observation report"));
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
    private readonly Func<Task<IReadOnlyList<HistoryFrame>>> load;
    private readonly Func<Consumption?> consumption;
    private readonly Func<Task<DiagnosticReport>> diagnose;
    private readonly TaskRepository? tasks;
    private readonly DockPanel tabs = new();
    private readonly WrapPanel tabHeaders = new();
    private readonly ContentControl tabContent = new();
    private readonly List<(Button Button, UIElement Content)> tabPages = new();
    private readonly StackPanel compare = new(), forecast = new(), diagnostics = new();
    private readonly ComboBox left = new(), right = new();
    private readonly CheckBox anonymous = new() { IsChecked = true };
    private readonly StackPanel comparisonBody = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(60) };
    private IReadOnlyList<HistoryFrame> frames = Array.Empty<HistoryFrame>();
    private DiagnosticReport? report;
    private bool busy, closed, binding;
    private readonly Border skinSurface = new(), skinShade = new();
    private readonly Atmosphere atmosphere = new() { IsHitTestVisible = false };
    internal void ApplySkin()
    {
        SkinTheme.ApplySurface(skinSurface, skinShade, true);
        atmosphere.RefreshAppearance();
    }
    internal InsightsWindow(TaskRepository? tasks, Func<Task<IReadOnlyList<HistoryFrame>>> load, Func<Consumption?> consumption, Func<Task<DiagnosticReport>> diagnose, int selected = 0)
    {
        this.tasks = tasks; this.load = load; this.consumption = consumption; this.diagnose = diagnose;
        Width = 980; Height = 780; MinWidth = 680; MinHeight = 460; MaxHeight = SystemParameters.WorkArea.Height - 20;
        Title = UiText.T("Copilot Pulse · 用量预估", "Copilot Pulse · Usage forecast"); Icon = BrandIcon.Window; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "SkinPanel"); SetResourceReference(ForegroundProperty, "SkinText");
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
        var layers = new Grid { ClipToBounds = true };
        skinSurface.Child = skinShade; skinShade.Child = atmosphere;
        layers.Children.Add(skinSurface);
        layers.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(95, 5, 12, 18)), IsHitTestVisible = false });
        var root = new DockPanel { Margin = new Thickness(20) }; layers.Children.Add(root); Content = layers;
        GlassChrome.SetSource(this, skinSurface);
        SizeChanged += (_, _) => ApplySkin(); ApplySkin();
        var header = new StackPanel(); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        header.Children.Add(InsightUi.Text(UiText.T("用量预估", "Usage forecast"), 26));
        var actions = new WrapPanel(); header.Children.Add(actions);
        actions.Children.Add(InsightUi.Button(UiText.T("刷新", "Refresh"), async () => await ReloadAsync()));
        actions.Children.Add(InsightUi.Button(UiText.T("登录数据源", "Sign in"), () => { if (Owner is MainWindow main) main.OpenDiagnosticLogin(); }));
        actions.Children.Add(InsightUi.Button(UiText.T("立即采样", "Refresh source"), () => { if (Owner is MainWindow main) main.RefreshDiagnosticSource(); }));
        actions.Children.Add(InsightUi.Button(UiText.T("关闭", "Close"), Close));
        DockPanel.SetDock(tabHeaders, Dock.Top); tabs.Children.Add(tabHeaders); tabs.Children.Add(tabContent);
        AddTab(UiText.T("区间对比", "Compare tasks"), compare); AddTab(UiText.T("用量预估", "Usage forecast"), forecast); AddTab(UiText.T("连接诊断向导", "Connection diagnostics"), diagnostics); root.Children.Add(tabs);
        compare.Children.Add(InsightUi.Text(UiText.T("选择两个任务，对比观测花费、持续时间与单次最大变化。", "Choose two tasks to compare observed cost, duration and largest sample change.")));
        var pickers = new Grid(); pickers.ColumnDefinitions.Add(new()); pickers.ColumnDefinitions.Add(new());
        foreach (var box in new[] { left, right }) { box.MinHeight = 34; box.Margin = new Thickness(0, 5, 10, 8); box.DisplayMemberPath = "Name"; box.SelectedValuePath = "Id"; box.Background = Brushes.White; box.Foreground = Brushes.Black; box.SelectionChanged += (_, _) => { if (!binding) RenderComparison(); }; pickers.Children.Add(box); } Grid.SetColumn(right, 1); compare.Children.Add(pickers);
        anonymous.Content = UiText.T("截图隐藏任务名", "Hide task names in screenshots"); anonymous.SetResourceReference(Control.ForegroundProperty, "SkinText"); compare.Children.Add(anonymous);
        compare.Children.Add(InsightUi.Button(UiText.T("导出对比截图…", "Export comparison image…"), () => { if (Selected(left) is {} a && Selected(right) is {} b && a.Id != b.Id) InsightUi.Export(this, ComparisonReport(a, b, anonymous.IsChecked == true)); })); compare.Children.Add(comparisonBody);
        SelectTab(selected);
        Loaded += async (_, _) => await ReloadAsync(); timer.Tick += async (_, _) => await ReloadAsync(); timer.Start();
        Closed += (_, _) => { closed = true; timer.Stop(); };
    }
    private void AddTab(string title, StackPanel panel)
    {
        int index = tabPages.Count;
        var button = InsightUi.Button(title, () => SelectTab(index));
        button.SetResourceReference(Control.ForegroundProperty, "SkinText");
        var page = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(12) };
        tabPages.Add((button, page)); tabHeaders.Children.Add(button);
    }
    internal void SelectTab(int index)
    {
        if (index < 0 || index >= tabPages.Count) return;
        tabContent.Content = tabPages[index].Content;
        for (int i = 0; i < tabPages.Count; i++)
        {
            tabPages[i].Button.FontWeight = i == index ? FontWeights.Bold : FontWeights.Normal;
            tabPages[i].Button.SetResourceReference(Control.BackgroundProperty, i == index ? "SkinChart" : "SkinHover");
            tabPages[i].Button.BorderThickness = new Thickness(i == index ? 2 : 1);
        }
    }
    internal async Task ReloadAsync()
    {
        if (busy || closed) return; busy = true;
        try
        {
            frames = await load(); if (closed) return;
            binding = true; var a = left.SelectedValue; var b = right.SelectedValue; var items = tasks?.Tasks.OrderByDescending(t => t.StartUtc).Select(t => new TaskRow(t.Id, UiText.TaskName(t.Name) + " · " + t.StartUtc.ToLocalTime().ToString("MM/dd HH:mm"), "", "", "", 0)).ToArray() ?? Array.Empty<TaskRow>();
            left.ItemsSource = items; right.ItemsSource = items; left.SelectedValue = a; right.SelectedValue = b;
            if (left.SelectedIndex < 0 && items.Length > 0) left.SelectedIndex = 0;
            if (right.SelectedIndex < 0 && items.Length > 1) right.SelectedIndex = 1;
            binding = false; RenderComparison(); RenderForecast();
            report = await diagnose(); if (!closed) RenderDiagnostics();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException) { diagnostics.Children.Clear(); diagnostics.Children.Add(InsightUi.Text(UiText.T("诊断或历史暂不可读，请稍后刷新。", "Diagnostics or history unavailable. Refresh to retry."))); }
        finally { binding = false; busy = false; }
    }
    internal void ExportReview(string directory)
    {
        if (Selected(left) is {} a && Selected(right) is {} b) InsightUi.SavePng(ComparisonReport(a,b,true), Path.Combine(directory,"comparison-report.png"));
        if (report != null) File.WriteAllText(Path.Combine(directory,"diagnostics.json"),JsonSerializer.Serialize(report, Store.Json));
    }
    private MeasurementTask? Selected(ComboBox box) => tasks?.Tasks.FirstOrDefault(t => t.Id == (box.SelectedValue as string));
    private FrameworkElement ComparisonReport(MeasurementTask a, MeasurementTask b, bool hide)
    {
        var now = DateTimeOffset.UtcNow; var p = InsightUi.Report(UiText.T("任务区间对比", "Task comparison"));
        var grid = new Grid(); grid.ColumnDefinitions.Add(new()); grid.ColumnDefinitions.Add(new());
        int i = 0; foreach (var t in new[] { a, b }) { var card = InsightUi.Card(InsightUi.Text((i == 0 ? "A · " : "B · ") + InsightUi.IntervalText(t, UsageInsights.Interval(t, frames, now), hide))); Grid.SetColumn(card, i++); grid.Children.Add(card); } p.Children.Add(grid);
        foreach (var t in new[] { a, b })
        {
            var chart = new HistoryChart { Height = 170, Margin = new Thickness(12) }; var m = UsageInsights.Interval(t, frames, now);
            chart.SetData(m.Delta.HasValue ? HistoryMath.RecordedSeries(frames.Where(f => f.Time >= t.StartUtc && f.Time <= (t.EndUtc ?? now)).ToArray(), null) : Array.Empty<HistoryPoint>()); p.Children.Add(chart);
        }
        var ma = UsageInsights.Interval(a, frames, now); var mb = UsageInsights.Interval(b, frames, now);
        p.Children.Add(InsightUi.Card(InsightUi.Text(UiText.T($"A − B 观测增量差：{InsightUi.Money(ma.Delta - mb.Delta)}。图表按各自时间显示，采样间隔不同会影响峰值；页面延迟可能使花费归属偏移。", $"A − B observed delta: {InsightUi.Money(ma.Delta - mb.Delta)}. Charts use their own time ranges. Sampling cadence affects peaks; source delay may shift task attribution."), 11))); return p;
    }
    private void RenderComparison()
    {
        comparisonBody.Children.Clear();
        if (Selected(left) is not {} a || Selected(right) is not {} b || a.Id == b.Id) { comparisonBody.Children.Add(InsightUi.Text(UiText.T("至少需要两个不同任务；在主界面开始并保存任务后再选择。", "Select two distinct saved tasks. Start and save tasks from the widget."))); return; }
        comparisonBody.Children.Add(ComparisonReport(a, b, false));
    }
    private int forecastHours = 24;
    private void RenderForecast() => RenderForecast(consumption(), frames, DateTimeOffset.UtcNow);
    internal void ReviewForecast(Consumption value, IReadOnlyList<HistoryFrame> history, DateTimeOffset now) => RenderForecast(value, history, now);
    private void RenderForecast(Consumption? value, IReadOnlyList<HistoryFrame> history, DateTimeOffset now)
    {
        forecast.Children.Clear(); var reset = HistoryRanges.Start("month", now).AddMonths(1);
        forecast.Children.Add(InsightUi.Text(UiText.T($"配额与消耗预测 · 距重置 {(reset-now).TotalDays:0.0} 天", $"Quota & usage forecast · {(reset-now).TotalDays:0.0} days to reset"), 21));
        forecast.Children.Add(InsightUi.Text(UiText.T($"重置：{reset:yyyy-MM-dd HH:mm} UTC · 本机 {reset.ToLocalTime():MM/dd HH:mm}", $"Reset: {reset:yyyy-MM-dd HH:mm} UTC · Local {reset.ToLocalTime():MM/dd HH:mm}"), 12));
        var choices = new WrapPanel(); forecast.Children.Add(choices);
        foreach (var choice in new[] { (1, UiText.T("近1小时", "Last hour")), (24, UiText.T("过去24小时", "Last 24 hours")), (0, UiText.T("本月记录", "Observed this month")) })
        {
            var button = InsightUi.Button(choice.Item2, () => { forecastHours = choice.Item1; RenderForecast(); });
            button.FontWeight = forecastHours == choice.Item1 ? FontWeights.Bold : FontWeights.Normal;
            button.BorderThickness = new Thickness(forecastHours == choice.Item1 ? 2 : 1); choices.Children.Add(button);
        }
        if (value?.Ok != true || value.Accounts.Count == 0) { forecast.Children.Add(InsightUi.Text(UiText.T("没有有效源读数，请先登录并采样。", "No valid source reading. Sign in and refresh the source."))); return; }
        forecast.Children.Add(InsightUi.Text(UiText.T($"源读数：{value.FetchedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · 预测时间以此为准", $"Source reading: {value.FetchedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss} · Estimates anchored to this time"), 11));
        foreach (var account in value.Accounts)
        {
            var from = forecastHours == 0 ? HistoryRanges.Start("month", now) : value.FetchedAt.AddHours(-forecastHours);
            var f = QuotaForecasting.Build(history, account, value.FetchedAt, from, now, history.LastOrDefault()?.Cadence ?? 60);
            var panel = new StackPanel(); panel.Children.Add(InsightUi.Text(account.Name, 14));
            if (f.State is ForecastState.InvalidReading or ForecastState.WrongMonth or ForecastState.Stale)
            {
                var warning = InsightUi.Text(ForecastReason(f.State), 13); warning.Foreground = Brushes.Khaki; panel.Children.Add(warning);
                forecast.Children.Add(InsightUi.Card(panel)); continue;
            }
            if (f.State == ForecastState.Ready)
            {
                panel.Children.Add(InsightUi.Text(UiText.T("预计月末累计用量", "Projected month-end usage"), 12));
                var projected = InsightUi.Text($"≈ ${f.ProjectedCost:N2}" + (f.ProjectedPercent.HasValue ? $"  ·  {f.ProjectedPercent:0.##}%" : ""), 32);
                projected.FontWeight = FontWeights.SemiBold;
                panel.Children.Add(projected);
                string depletion = f.Exhausted ? UiText.T("已耗尽 · 请核对源页面", "Already exhausted · Check source")
                    : f.DepletesBeforeReset == true ? UiText.T($"按此速度会耗尽 · 预计在 {f.Depletion!.Value.ToLocalTime():MM/dd HH:mm}", $"Will run out at this rate · Estimated depletion {f.Depletion!.Value.ToLocalTime():MM/dd HH:mm}")
                    : f.DepletesBeforeReset == false ? UiText.T("按此速度，重置前不会耗尽", "Not expected to run out before reset at this rate")
                    : UiText.T("耗尽风险未知 · 页面额度信息不足", "Depletion risk unknown · Insufficient source quota information");
                var status = InsightUi.Text(depletion, 17); status.FontWeight = FontWeights.SemiBold;
                status.Foreground = f.Exhausted || f.DepletesBeforeReset == true ? Brushes.Salmon : f.DepletesBeforeReset == false ? Brushes.LightGreen : Brushes.Khaki;
                panel.Children.Add(status);
            }
            else
            {
                panel.Children.Add(InsightUi.Text(UiText.T("预测暂不可用", "Forecast unavailable"), 24));
                panel.Children.Add(InsightUi.Text(f.Exhausted ? UiText.T("已达到或超过额度，请核对源页面。", "Quota reached or exceeded. Check the source page.") : ForecastReason(f.State), 14));
            }
            var progress = new ProgressBar { Minimum = 0, Maximum = 100, Value = (double)(100 - f.RemainingPercent.GetValueOrDefault(100)), Height = 7, Margin = new Thickness(0,4,0,10), Foreground = f.Exhausted || f.DepletesBeforeReset == true ? Brushes.Salmon : Brushes.MediumSeaGreen, Background = Brushes.DimGray }; panel.Children.Add(progress);
            panel.Children.Add(InsightUi.Text(UiText.T($"当前已用 ${account.Cost:N2} · 页面 {account.Percent:0.##}%", $"Currently used ${account.Cost:N2} · Source {account.Percent:0.##}%"), 11));
            panel.Children.Add(InsightUi.Text(f.Remaining.HasValue
                ? UiText.T($"数值上限 ${account.Limit:N2} · 剩余 ${f.Remaining:N2}（{f.RemainingPercent:0.##}%）", $"Numeric limit ${account.Limit:N2} · Remaining ${f.Remaining:N2} ({f.RemainingPercent:0.##}%)")
                : UiText.T($"Standard · 页面剩余约 {f.RemainingPercent:0.##}% · 未提供数值上限", $"Standard · Source remaining ≈ {f.RemainingPercent:0.##}% · No numeric limit provided"), 12));
            if (f.DailyAllowance.HasValue) panel.Children.Add(InsightUi.Text(UiText.T($"剩余额度均摊 ${f.DailyAllowance:N2}/天", $"Remaining quota spread: ${f.DailyAllowance:N2}/day"), 11));
            else if (f.DailyPercentAllowance.HasValue) panel.Children.Add(InsightUi.Text(UiText.T($"剩余额度均摊约 {f.DailyPercentAllowance:0.##}%/天", $"Remaining quota spread: ≈ {f.DailyPercentAllowance:0.##}%/day"), 11));
            if (f.State == ForecastState.Ready)
            {
                var rate = f.Rate!;
                panel.Children.Add(InsightUi.Text(UiText.T($"观测速度 ${rate.PerHour:0.####}/小时 · ${rate.PerHour*24:N2}/天", $"Observed rate ${rate.PerHour:0.####}/h · ${rate.PerHour*24:N2}/day"), 11));
                panel.Children.Add(InsightUi.Text(UiText.T($"覆盖 {rate.From.ToLocalTime():MM/dd HH:mm}–{rate.Until.ToLocalTime():MM/dd HH:mm} · {(rate.Until-rate.From).TotalHours:0.##} 小时 · {rate.Samples} 次采样", $"Coverage {rate.From.ToLocalTime():MM/dd HH:mm}–{rate.Until.ToLocalTime():MM/dd HH:mm} · {(rate.Until-rate.From).TotalHours:0.##} hours · {rate.Samples} samples"), 11));
                if (f.HasGaps) panel.Children.Add(InsightUi.Text(UiText.T("覆盖内存在采样断档；速度为跨断档的端点平均，不代表持续采样。", "Coverage includes sampling gaps. Rate is an endpoint average across gaps, not continuous observation."), 11));
            }
            if (f.PercentEstimate) panel.Children.Add(InsightUi.Text(UiText.T("≈ 百分比外推：按当前金额与页面已用百分比的比例估算，百分比经过四舍五入；不推定官方数值上限。", "≈ Percentage extrapolation uses current cost and the rounded source percentage. It does not establish an official monetary limit."), 11));
            panel.Children.Add(InsightUi.Text(UiText.T("成功采样不代表页面已完成结算；工作强度变化会影响预测。", "Successful sampling does not prove the page has settled usage. Workload changes affect projections."), 11));
            forecast.Children.Add(InsightUi.Card(panel));
        }
    }
    internal void SelectForecastRangeForReview(int index)
    {
        var choices = forecast.Children.OfType<WrapPanel>().Single();
        ((Button)choices.Children[index]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        int expected = new[] { 1, 24, 0 }[index];
        if (forecastHours != expected) throw new InvalidOperationException("Forecast range button did not change basis");
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
        ForecastState.Stale => UiText.T("源读数已延迟，已暂停预测。请点“立即采样”；刷新此窗口不会刷新源页面。", "Source reading is stale. Prediction paused. Choose Refresh source; refreshing this window does not refresh the source page."),
        ForecastState.MissingAccount => UiText.T("所选覆盖内有账户缺失，无法比较。请选择更短区间或继续采样。", "Account missing within selected coverage. Choose a shorter range or collect more samples."),
        ForecastState.Correction => UiText.T("所选覆盖内有金额下调，已暂停外推，避免把数据修正当成消耗。", "Downward correction within selected coverage. Extrapolation paused to avoid treating corrections as usage."),
        _ => UiText.T("所选区间内读数不足：至少需要两次可比较读数，跨度不少于一分钟。", "Insufficient coverage: at least two comparable readings spanning one minute are required.")
    };

    private void RenderDiagnostics()
    {
        diagnostics.Children.Clear(); if (report == null) return;
        diagnostics.Children.Add(InsightUi.Text(UiText.T("连接诊断向导", "Connection diagnostics"), 21));
        foreach (var c in report.Checks) { var label = InsightUi.Text(c.State + " · " + c.Name + "\n" + c.Detail); label.Foreground = c.State == "OK" ? Brushes.LightGreen : c.State == "ERROR" ? Brushes.Salmon : Brushes.Khaki; diagnostics.Children.Add(InsightUi.Card(label)); }
        diagnostics.Children.Add(InsightUi.Button(UiText.T("导出匿名诊断报告…", "Export anonymous diagnostics…"), () => {
            var save = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "CopilotPulse-diagnostics.json" }; if (save.ShowDialog(this) != true) return;
            try { File.WriteAllText(save.FileName, JsonSerializer.Serialize(report, Store.Json)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(this, UiText.T("保存失败，请更换位置。", "Could not save. Choose another location.")); }
        }));
        diagnostics.Children.Add(InsightUi.Text(UiText.T("报告仅包含版本、时区和固定诊断状态，不含账户名、用量金额、令牌、Cookie 或完整网页地址。", "Reports contain version, time zone and fixed diagnostic states only; no account names, amounts, tokens, cookies or full URLs."), 11));
    }
}
