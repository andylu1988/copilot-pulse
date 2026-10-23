using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CopilotPulse;

public partial class MainWindow
{
    private void SelectMiniMetric(string metric, bool enterMini)
    {
        if (metric == "task" && !HasActiveMeasurement) return;
        preferences.MiniShowRecordDelta = metric == "task";
        preferences.MiniHistoryRange = metric is "task" or "total" ? "" : HistoryRanges.Normalize(metric);
        miniTrendDelta = null;
        RefreshMiniRecordDeltaButton(); RefreshUsageProgress();
        if (enterMini) SetDisplayMode("mini");
        SavePreferences(); _ = RefreshMiniHistoryAsync();
    }
    private IEnumerable<object> MiniMetricItems(bool enterMini)
    {
        RefreshMiniRecordDeltaButton();
        MenuItem Metric(string key, string label, bool enabled = true)
        {
            var item = new MenuItem { Header = label, Tag = key, IsCheckable = true, IsEnabled = enabled,
                IsChecked = key == "task" ? ShowMiniTaskDelta : key == "total" ? !ShowMiniTaskDelta && !ShowMiniHistoryDelta : ShowMiniHistoryDelta && preferences.MiniHistoryRange == key };
            item.Click += (_, _) => SelectMiniMetric(key, enterMini);
            if (!enabled) { item.ToolTip = UiText.T("请先开始任务计量。结束后自动恢复本月累计。", "Start a task first. Returns to monthly total when the task ends."); ToolTipService.SetShowOnDisabled(item, true); }
            return item;
        }
        yield return Metric("total", UiText.T("本月累计（总用量）", "Monthly total"));
        yield return Metric("task", UiText.T("任务增量", "Task delta"), HasActiveMeasurement);
        yield return new Separator();
        foreach (string range in new[] { "today", "24h", "7d", "month" })
            yield return Metric(range, UiText.Range(range) + UiText.T("增量", " delta"));
        string[] months;
        try { months = demoHistory?.Select(f => f.Period).Distinct().OrderByDescending(m => m).ToArray() ?? history?.AvailableMonths() ?? Array.Empty<string>(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { months = Array.Empty<string>(); }
        if (months.Length > 0)
        {
            var archive = new MenuItem { Header = UiText.T("历史月份增量", "Historical month delta") };
            foreach (string month in months) archive.Items.Add(Metric(month, month));
            yield return archive;
        }
        yield return new Separator();
        var dual = new MenuItem { Header = UiText.T("同时显示第二指标", "Show secondary metric"), Tag = "dual", IsCheckable = true, IsChecked = preferences.MiniDualMetrics,
            ToolTip = UiText.T("底部显示副指标，可在下方独立选择今天、七天等增量。", "Show a secondary metric below; choose today, seven days or another metric independently.") };
        dual.Click += (_, _) => { preferences.MiniDualMetrics = dual.IsChecked; RefreshUsageProgress(); SavePreferences(); };
        yield return dual;
        var secondary = new MenuItem { Header = UiText.T("副指标 ▸", "Secondary metric"), Tag = "secondary" };
        foreach (var key in new[] { "auto", "total", "today", "24h", "7d", "month", "task" })
        {
            string label = key == "auto" ? UiText.T("自动搭配", "Automatic pairing") : key == "total" ? UiText.T("本月累计", "Monthly total") : key == "task" ? UiText.T("任务增量", "Task delta") : UiText.Range(key) + UiText.T("增量", " delta");
            var item = new MenuItem { Header = label, Tag = key, IsCheckable = true, IsChecked = preferences.MiniDualMetrics && preferences.MiniSecondaryMetric == key, IsEnabled = key != "task" || HasActiveMeasurement };
            item.Click += (_, _) => SelectMiniSecondaryMetric(key);
            secondary.Items.Add(item);
        }
        yield return secondary;
    }
    private void SelectMiniSecondaryMetric(string key)
    {
        if (key == "task" && !HasActiveMeasurement) return;
        preferences.MiniSecondaryMetric = key; preferences.MiniDualMetrics = true;
        miniSecondaryDelta = null; RefreshUsageProgress(); SavePreferences(); _ = RefreshMiniHistoryAsync();
    }
    private string EffectiveMiniSecondaryMetric => preferences.MiniSecondaryMetric == "auto"
        ? ShowMiniTaskDelta || ShowMiniHistoryDelta ? "total" : HasActiveMeasurement ? "task" : "today"
        : preferences.MiniSecondaryMetric == "task" && !HasActiveMeasurement ? "today" : preferences.MiniSecondaryMetric;
    private ContextMenu BuildMiniMetricMenu(bool enterMini)
    {
        var menu = new ContextMenu();
        foreach (var item in MiniMetricItems(enterMini)) menu.Items.Add(item);
        return menu;
    }
}
