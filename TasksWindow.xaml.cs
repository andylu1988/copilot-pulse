using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CopilotPulse;

public sealed record TaskRow(string Id, string Name, string Start, string End, string Delta, int Samples);
public partial class TasksWindow : Window
{
    private readonly TaskRepository tasks;
    private readonly Func<Task<(IReadOnlyList<HistoryFrame> Frames, IReadOnlyList<SamplingEvent> Events)>> load;
    private readonly Func<(string Label, string Hint)> health;
    private readonly Action start, end;
    private readonly Action<string, string> rename;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(10) };
    private IReadOnlyList<HistoryFrame> frames = Array.Empty<HistoryFrame>();
    private IReadOnlyList<SamplingEvent> events = Array.Empty<SamplingEvent>();
    private bool loading, closed;
    private string? editingTaskId;
    private MeasurementTask? Selected => tasks.Tasks.FirstOrDefault(t => t.Id == (TaskList.SelectedItem as TaskRow)?.Id);
    public TasksWindow(TaskRepository tasks, Func<Task<(IReadOnlyList<HistoryFrame>, IReadOnlyList<SamplingEvent>)>> load, Func<(string, string)> health, Action start, Action end, Action<string, string> rename)
    {
        InitializeComponent(); Icon = BrandIcon.Window;
        this.tasks = tasks; this.load = load; this.health = health; this.start = start; this.end = end; this.rename = rename;
        MaxHeight = SystemParameters.WorkArea.Height - 30;
        SkinTheme.ApplySurface(TaskSurface, TaskShade, true);
        Resources[typeof(Button)] = new Style(typeof(Button), (Style)FindResource("GlassButtonStyle"));
        GlassChrome.SetSource(this, TaskSurface);
        UtilitySkin.ApplyTitleBar(this);
        ApplyLanguage();
        Loaded += async (_, _) => await ReloadAsync(); timer.Tick += async (_, _) => await ReloadAsync(); timer.Start();
        Closed += (_, _) => { closed = true; timer.Stop(); };
    }
    internal void ApplyLanguage()
    {
        Title = UiText.T("Copilot Pulse · 计量记录", "Copilot Pulse · Measurement history");
        Heading.Text = UiText.T("计量记录", "Measurement history");
        Subtitle.Text = UiText.T("为单次任务记录观测花费 · 结束后保留，随时回看", "Observe usage for a task · Saved after ending, ready to revisit");
        NameColumn.Header = UiText.T("任务", "Task"); StartColumn.Header = UiText.T("开始 · 本机时间", "Start · Local"); EndColumn.Header = UiText.T("结束 · 本机时间", "End · Local"); DeltaColumn.Header = "Δ USD"; SamplesColumn.Header = UiText.T("采样数", "Samples");
        NewButton.Content = UiText.T("＋ 开始任务计量", "＋ Start measurement"); RenameButton.Content = UiText.T("保存备注", "Save name"); EndButton.Content = UiText.T("结束并保存", "End & save"); CopyButton.Content = UiText.T("复制摘要", "Copy summary");
        ImageButton.Content = UiText.T("导出任务截图…", "Export task image…");
        ExportButton.Content = UiText.T("导出所选计量…", "Export selected measurement…"); ExportAllButton.Content = UiText.T("导出全部采样与计量…", "Export all samples & measurements…"); Anonymous.Content = UiText.T("匿名化（推荐）", "Anonymize (recommended)"); DetailsExpander.Header = UiText.T("缺失采样与刷新日志 ▾", "Missing samples & refresh log ▾");
        ExportNote.Text = UiText.T("CSV / JSON 包含区间、成功采样和刷新状态。匿名化移除账户和任务名，仍保留时间与金额。增量从首次有效采样起算；页面可能延迟，不等于精确的单任务账单。", "CSV / JSON include tasks, successful samples and refresh states. Anonymization removes accounts and task names; times and amounts remain. Deltas start at the first valid sample; delayed source readings are not exact per-task billing.");
    }
    internal async Task ReloadAsync()
    {
        if (loading || closed) return;
        loading = true;
        try
        {
            var loaded = await load(); if (closed) return;
            frames = loaded.Frames; events = loaded.Events;
            string? id = (TaskList.SelectedItem as TaskRow)?.Id;
            var rows = tasks.Tasks.OrderByDescending(t => t.StartUtc).Select(t => {
                var m = SamplingHealth.Measure(t, frames, events, DateTimeOffset.UtcNow);
                return new TaskRow(t.Id, UiText.TaskName(t.Name), t.StartUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"), t.EndUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? UiText.T("记录中", "Recording"), m.Delta.HasValue ? HistoryChart.Signed(m.Delta.Value) : "—", m.Samples);
            }).ToList();
            TaskList.ItemsSource = rows;
            TaskList.SelectedItem = rows.FirstOrDefault(r => r.Id == id) ?? rows.FirstOrDefault();
            EmptyLabel.Text = rows.Count == 0 ? UiText.T("暂无任务。点击“开始任务计量”，输入名称后开始采样。", "No tasks yet. Start a task and give it a name.") : UiText.T($"{rows.Count} 个已保存任务 · 选择一行查看", $"{rows.Count} saved tasks · Select a row to inspect");
            NewButton.IsEnabled = !tasks.Tasks.Any(t => t.EndUtc == null);
            var h = health(); HealthLabel.Text = h.Label; HealthHint.Text = h.Hint;
            ShowSelected();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Result.Text = UiText.T("读取失败，原始记录已保留。请检查磁盘或权限。", "Could not read data. Original records retained; check disk and permissions."); }
        finally { loading = false; }
    }
    private void SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!loading) ShowSelected(); }
    private void ShowSelected()
    {
        var task = Selected;
        ImageButton.IsEnabled = RenameButton.IsEnabled = CopyButton.IsEnabled = ExportButton.IsEnabled = task != null;
        EndButton.IsEnabled = task != null && task.EndUtc == null;
        if (task == null) { TaskName.Text = ""; SelectionSummary.Text = ""; SelectionHealth.Text = ""; SamplingDetails.Text = ""; TaskChart.SetData(Array.Empty<HistoryPoint>()); return; }
        // Avoid replacing text while a user edits the note.
        if (!TaskName.IsKeyboardFocusWithin || editingTaskId != task.Id) { TaskName.Text = UiText.TaskName(task.Name); editingTaskId = task.Id; }
        var now = DateTimeOffset.UtcNow;
        var m = SamplingHealth.Measure(task, frames, events, now);
        SelectionSummary.Text = LocalSummary(task, m);
        SelectionHealth.Text = UiText.T($"估计缺失 {m.Gaps.Sum(g => (long)g.EstimatedMissing)} 次 · 失败刷新 {m.FailedAttempts} 次 · 重试 {m.RetryAttempts} 次 · 最近成功 {m.LastSuccess?.ToLocalTime().ToString("MM/dd HH:mm:ss") ?? "—"}", $"~{m.Gaps.Sum(g => (long)g.EstimatedMissing)} missing · {m.FailedAttempts} failed refreshes · {m.RetryAttempts} retries · Last success {m.LastSuccess?.ToLocalTime().ToString("MM/dd HH:mm:ss") ?? "—"}");
        var subset = frames.Where(f => f.Time >= task.StartUtc && f.Time <= (task.EndUtc ?? now)).ToList();
        TaskChart.Accent = SkinTheme.Current.Accent;
        TaskChart.SetData(m.Delta.HasValue ? HistoryMath.RecordedSeries(subset, null) : Array.Empty<HistoryPoint>(), from: task.StartUtc, until: task.EndUtc ?? now);
        var details = m.Gaps.Select(g => UiText.T($"缺失区间 {g.From.ToLocalTime():MM/dd HH:mm:ss} → {g.Until.ToLocalTime():MM/dd HH:mm:ss} · 约 {g.EstimatedMissing} 次", $"Gap {g.From.ToLocalTime():MM/dd HH:mm:ss} → {g.Until.ToLocalTime():MM/dd HH:mm:ss} · ~{g.EstimatedMissing} samples"));
        SamplingDetails.Text = string.Join("\n", details.Concat(events.Where(e => e.Time >= task.StartUtc && e.Time <= (task.EndUtc ?? now)).TakeLast(100).Select(e => $"{e.Time.ToLocalTime():MM/dd HH:mm:ss}  {e.State}  ({e.Cadence}s)")));
        if (SamplingDetails.Text.Length == 0) SamplingDetails.Text = UiText.T("无缺失标记或刷新事件。旧历史没有刷新日志。", "No gaps or refresh events. Older history has no refresh log.");
    }
    private async void NewTask(object sender, RoutedEventArgs e) { start(); await ReloadAsync(); }
    private async void EndTask(object sender, RoutedEventArgs e) { if (Selected?.EndUtc == null) end(); await ReloadAsync(); }
    private async void Rename(object sender, RoutedEventArgs e) { if (Selected is { } t) { rename(t.Id, TaskName.Text); TaskList.Focus(); await ReloadAsync(); } }
    private void CopySummary(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } t) return;
        try { Clipboard.SetText(LocalSummary(t, SamplingHealth.Measure(t, frames, events, DateTimeOffset.UtcNow))); Result.Text = UiText.T("摘要已复制。", "Summary copied."); }
        catch (System.Runtime.InteropServices.COMException) { Result.Text = UiText.T("剪贴板暂不可用，请重试。", "Clipboard busy; try again."); }
    }
    private static string LocalSummary(MeasurementTask task, TaskMetrics m) => UiText.T(
        $"{UiText.TaskName(task.Name)} · {task.StartUtc.ToLocalTime():yyyy-MM-dd HH:mm} → {(task.EndUtc.HasValue ? task.EndUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "记录中")} · 净增量 {(m.Delta.HasValue ? HistoryChart.Signed(m.Delta.Value) + " USD" : "—")} · {m.Samples} 个有效采样",
        TaskExport.Summary(task, m));
    private void ExportImage(object sender, RoutedEventArgs e) { if (Selected is {} t) InsightUi.Export(this, InsightUi.TaskReport(t, frames, DateTimeOffset.UtcNow, Anonymous.IsChecked == true)); }
    private async void ExportSelected(object sender, RoutedEventArgs e) => await ExportAsync(false);
    private async void ExportAll(object sender, RoutedEventArgs e) => await ExportAsync(true);
    private async Task ExportAsync(bool all)
    {
        await ReloadAsync();
        var task = Selected; if (!all && task == null) return;
        var dialog = new SaveFileDialog { Title = UiText.T("导出观测数据", "Export observed data"), Filter = "CSV (*.csv)|*.csv|JSON (*.json)|*.json", FileName = "CopilotPulse-" + (all ? "history" : "task") + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var selectedTasks = all ? tasks.Tasks.ToArray() : new[] { task! };
            var selectedFrames = all ? frames : frames.Where(f => f.Time >= task!.StartUtc && f.Time <= (task.EndUtc ?? now)).ToList();
            var selectedEvents = all ? events : events.Where(e => e.Time >= task!.StartUtc && e.Time <= (task.EndUtc ?? now)).ToList();
            string text = dialog.FilterIndex == 2 ? TaskExport.Json(selectedTasks, selectedFrames, selectedEvents, now, Anonymous.IsChecked == true) : TaskExport.Csv(selectedTasks, selectedFrames, selectedEvents, now, Anonymous.IsChecked == true);
            await File.WriteAllTextAsync(dialog.FileName, text, new UTF8Encoding(true));
            Result.Text = UiText.T("已导出：", "Exported: ") + dialog.FileName;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Result.Text = UiText.T("导出失败，请选择可写目录。", "Export failed. Choose a writable folder."); }
    }
    internal void SelectForReview(int index) { TaskList.SelectedIndex = index; ShowSelected(); DetailsExpander.IsExpanded = true; }
}

internal sealed class TaskNameDialog : Window
{
    private readonly TextBox input;
    public string TaskName => TaskRepository.CleanName(input.Text);
    public TaskNameDialog(Window owner)
    {
        Owner = owner; Title = UiText.T("开始任务计量", "Start task measurement"); Width = 450; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner; Icon = BrandIcon.Window;
        SetResourceReference(BackgroundProperty, "SkinPanel"); SetResourceReference(ForegroundProperty, "SkinText");
        var panel = new StackPanel { Margin = new Thickness(22) }; _ = new UtilitySkin(this, panel);
        panel.Children.Add(new TextBlock { Text = UiText.T("给这次任务起个名字", "Name this task"), FontSize = 19 });
        panel.Children.Add(new TextBlock { Text = UiText.T("例如：修复登录 Bug / 准备客户方案 / 代码审查", "For example: Login fix / Customer proposal / Code review"), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
        input = new TextBox { MaxLength = 100, FontSize = 14, Padding = new Thickness(10), Text = UiText.T("未命名任务", "Untitled task") }; input.SetResourceReference(StyleProperty, "GlassTextBoxStyle"); panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
        var cancel = new Button { Content = UiText.T("取消", "Cancel"), IsCancel = true }; buttons.Children.Add(cancel);
        var ok = new Button { Content = UiText.T("开始计量", "Start measurement"), IsDefault = true, Margin = new Thickness(8, 0, 0, 0) }; ok.SetResourceReference(BackgroundProperty, "SkinHover"); ok.Click += (_, _) => DialogResult = true; buttons.Children.Add(ok); panel.Children.Add(buttons);
        Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
    }
}
