using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CopilotPulse;

public partial class HistoryWindow : Window
{
    private readonly HistoryRepository? repository;
    private readonly IReadOnlyList<HistoryFrame>? demo;
    private IReadOnlyList<HistoryFrame> frames = Array.Empty<HistoryFrame>();
    private string range = "month";
    private readonly Preferences viewPreferences;
    private readonly Action? viewChanged;
    private string? series;
    private bool changes, closed;
    private int revision;
    private DateTimeOffset chartStart, chartEnd;
    private readonly DispatcherTimer todayRefresh = new() { Interval = TimeSpan.FromMinutes(1) };

    public HistoryWindow(HistoryRepository? repository, IReadOnlyList<HistoryFrame>? demo = null, Preferences? preferences = null, Action? viewChanged = null)
    {
        InitializeComponent();
        Icon = BrandIcon.Window;
        this.repository = repository;
        this.demo = demo;
        viewPreferences = preferences ?? new Preferences();
        this.viewChanged = viewChanged;
        range = HistoryRanges.Normalize(viewPreferences.HistoryRange);
        changes = viewPreferences.HistoryChanges;
        StorageButton.ToolTip = repository?.DirectoryPath ?? "示例模式";
        HistorySurface.SizeChanged += (_, _) => SkinTheme.ApplySurface(HistorySurface, HistoryPhotoShade, true);
        SkinTheme.ApplySurface(HistorySurface, HistoryPhotoShade, true);
        MaxHeight = SystemParameters.WorkArea.Height;
        MaxWidth = SystemParameters.WorkArea.Width;
        if (demo != null) { ConnectionLabel.Text = "示例数据 · 非真实历史"; ConnectionLabel.Foreground = Brush("#E4BA7C"); }
        Loaded += async (_, _) => await ReloadAsync();
        todayRefresh.Tick += async (_, _) => { if (IsVisible) await ReloadAsync(); };
        if (repository != null) repository.Changed += OnHistoryChanged;
        Closed += (_, _) => { closed = true; todayRefresh.Stop(); if (repository != null) repository.Changed -= OnHistoryChanged; };
        UpdateButtons();
    }

    public void ApplySkin(bool paletteChanged = true) { HistoryAtmosphere.RefreshAppearance(); SkinTheme.ApplySurface(HistorySurface, HistoryPhotoShade, true); UpdateButtons(); if (paletteChanged) RenderChart(); }

    private static SolidColorBrush Brush(string color) => SkinTheme.Resolve(color);
    private async void OnHistoryChanged() { if (!closed) await Dispatcher.InvokeAsync(ReloadAsync).Task.Unwrap(); }
    private DateTimeOffset RangeStart(DateTimeOffset now) => HistoryRanges.Start(range, now);

    public async Task ReloadAsync()
    {
        int request = ++revision;
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset start = RangeStart(now);
        DateTimeOffset end = HistoryRanges.End(range, now);
        try
        {
            var loaded = demo != null ? demo.Where(f => f.Time >= start && f.Time <= end).ToList() : await Task.Run(() => repository!.ReadRange(start, end));
            if (closed || request != revision) return;
            frames = loaded;
            chartStart = start;
            chartEnd = end;
            if (series != null && !frames.Any(f => f.Accounts.Any(a => a.Key == series))) { series = null; SeriesButton.Content = "总用量  ▾"; }
            RenderChart();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (request == revision) HistoryNotice.Text = "暂时无法读取历史文件，请检查磁盘或权限。";
        }
    }

    private void RenderChart()
    {
        var points = HistoryMath.Series(frames, series);
        LatestValue.Text = points.Count > 0 ? points[^1].Value.ToString("C2", CultureInfo.GetCultureInfo("en-US")) : "—";
        decimal? net = HistoryMath.NetChange(points);
        NetValue.Text = net.HasValue ? HistoryChart.Signed(net.Value) : "—";
        string? netReason = HistoryMath.NetChangeUnavailableReason(points);
        NetValue.ToolTip = netReason ?? "末次读数减去首次读数；零金额企业增减不影响比较，负变化保留。";
        NetValue.Foreground = Brush(net < 0 ? "#F1B58B" : "#79D9BF");
        SampleCount.Text = points.Count.ToString("N0") + " 次";
        CoverageLabel.Text = points.Count > 0 ? $"实际记录 {points[0].Time.LocalDateTime:MM/dd HH:mm} — {points[^1].Time.LocalDateTime:MM/dd HH:mm}" : "从首次记录开始积累";
        PeriodLabel.Text = range == "today" ? $"今天 · {chartStart.LocalDateTime:MM/dd} 00:00 — {chartEnd.LocalDateTime:HH:mm}（本机时间）" : range == "month" || HistoryRanges.IsMonth(range) ? $"{HistoryRanges.Label(range)} · 配额按 UTC 月份，起点 {chartStart.LocalDateTime:yyyy/MM/dd HH:mm}（本机时间）" : HistoryRanges.Label(range);
        Chart.Accent = series == null ? SkinTheme.Current.Accent : SkinTheme.Current.Secondary;
        Chart.SetData(points, changes, range == "today" ? chartStart : null, range == "today" ? chartEnd : null);
        Footnote.Text = changes ? "单次变化 = 本次减去上次连续读数；每次间隔取决于刷新频率。中断和跨月不计算增量。" : "鼠标悬停查看读数 · 采样中断与跨月重置分段显示 · 纵轴按实际金额缩放";
        HistoryNotice.Text = demo != null ? "示例数据用于验证外观，不会写入你的历史文件。" : repository?.LastError ?? (
            points.Count == 0 ? "这个时间范围内没有记录。可点击“月份 ▾”选择有记录的月份。" :
            $"最后记录 {points[^1].Time.LocalDateTime:MM/dd HH:mm:ss} · 数据仅保存在本机，不补造采集前的历史。");
        if (net < 0) Footnote.Text = "净变化为负：区间记录可能包含源页面累计值归零或金额修正，不代表负消费。  " + Footnote.Text;
        if (netReason != null) Footnote.Text = "净变化不可计算：" + netReason + "  " + Footnote.Text;
        if (demo == null && points.Count > 0)
        {
            bool recent = DateTimeOffset.Now - points[^1].Time < TimeSpan.FromMinutes(6);
            ConnectionLabel.Text = recent ? "随成功刷新持续记录" : "显示已保存记录";
            ConnectionLabel.Foreground = Brush(recent ? "#7EBEAB" : "#C8AC83");
        }
    }

    private void UpdateButtons()
    {
        foreach (var button in new[] { TodayButton, HoursButton, WeekButton, MonthButton }) button.Background = Brush((string)button.Tag == range ? "#284560" : "#111E31");
        MonthPickerButton.Content = HistoryRanges.IsMonth(range) ? range + " ▾" : "月份 ▾";
        MonthPickerButton.Background = Brush(HistoryRanges.IsMonth(range) ? "#284560" : "#111E31");
        if (range == "today" && !closed) todayRefresh.Start(); else todayRefresh.Stop();
        CumulativeButton.Background = Brush(changes ? "#111E31" : "#284560");
        ChangesButton.Background = Brush(changes ? "#284560" : "#111E31");
    }

    private async void ChangeRange(object sender, RoutedEventArgs e) { range = (string)((Button)sender).Tag; viewPreferences.HistoryRange = range; viewChanged?.Invoke(); UpdateButtons(); await ReloadAsync(); }
    private async void ChooseMonth(object sender, RoutedEventArgs e)
    {
        try
        {
            var all = demo ?? await Task.Run(() => repository!.ReadAll());
            if (closed) return;
            var months = all.Select(f => HistoryRanges.Period(f.Time)).Distinct().OrderByDescending(m => m).ToList();
            var menu = new ContextMenu { PlacementTarget = MonthPickerButton };
            if (months.Count == 0) menu.Items.Add(new MenuItem { Header = "还没有月份记录", IsEnabled = false });
            foreach (string month in months)
            {
                var item = new MenuItem { Header = month, IsCheckable = true, IsChecked = range == month };
                item.Click += async (_, _) => { range = month; viewPreferences.HistoryRange = range; viewChanged?.Invoke(); UpdateButtons(); await ReloadAsync(); };
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { HistoryNotice.Text = "暂时无法读取历史月份，请检查磁盘或权限。"; }
    }

    private void ChangeMode(object sender, RoutedEventArgs e) { changes = (string)((Button)sender).Tag == "changes"; viewPreferences.HistoryChanges = changes; viewChanged?.Invoke(); UpdateButtons(); RenderChart(); }
    private void ChooseSeries(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        void Add(string label, string? key)
        {
            var item = new MenuItem { Header = label, IsCheckable = true, IsChecked = series == key };
            item.Click += (_, _) => { series = key; SeriesButton.Content = label + "  ▾"; RenderChart(); };
            menu.Items.Add(item);
        }
        Add("总用量", null);
        foreach (var account in frames.SelectMany(f => f.Accounts).GroupBy(a => a.Key).Select(g => g.Last()).OrderBy(a => a.Name)) Add(account.Name, account.Key);
        menu.PlacementTarget = SeriesButton;
        menu.IsOpen = true;
    }

    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element != null) { if (element is Button) return; element = VisualTreeHelper.GetParent(element); }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void OpenStorage(object sender, RoutedEventArgs e)
    {
        if (repository != null) { Directory.CreateDirectory(repository.DirectoryPath); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(repository.DirectoryPath) { UseShellExecute = true }); }
    }

    private void CloseHistory(object sender, RoutedEventArgs e) => Close();
    internal void HoverForReview() => Chart.InspectPointForReview(36);
    internal void ShowChangesForReview() { changes = true; UpdateButtons(); RenderChart(); }
}
