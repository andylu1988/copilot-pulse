using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace CopilotPulse;
public partial class MainWindow
{
    private async Task ReviewWorkspaceAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        int checks = 0;
        void Check(bool ok, string name) { File.AppendAllText(Path.Combine(directory,"workspace-checks.txt"), (ok?"PASS ":"FAIL ")+name+"\n"); if(!ok) throw new InvalidOperationException(name); checks++; }
        preferences.SkinId="matrix"; preferences.Language="zh-CN"; UiText.Language="zh-CN"; SkinEdited(); SetDisplayMode("compact");
        foreach (var available in new[] { Array.Empty<string>(), new[] { "代码审查", "修复登录 Bug" } })
        {
            var dialog = new TaskNameDialog(this, available); dialog.Show(); await Task.Delay(120);
            var panel = dialog.DialogBody;
            var checkbox = panel.Children.OfType<CheckBox>().Single();
            Check(checkbox.Foreground == dialog.FindResource("SkinText"), "Template checkbox uses readable skin foreground");
            var choices = panel.Children.OfType<ComboBox>().SingleOrDefault();
            Check(available.Length == 0 ? choices == null : choices?.Style == dialog.FindResource("SkinComboBoxStyle"), "Template dropdown is skinned and omitted when empty");
            CaptureControl(dialog, Path.Combine(directory, available.Length == 0 ? "task-name-empty.png" : "task-name-templates.png")); dialog.Close();
        }
        SaveTaskTemplate("代码审查"); SaveTaskTemplate("代码审查");
        Check(preferences.TaskTemplates.Count(t=>t=="代码审查")==1,"Template deduplication");
        var start=demoHistory![70].Time;
        var task=measurementTasks!.Start("本地测试 · 代码审查",start,60);
        measurementTasks.AddMarker(task.Id,"开始跑测试",demoHistory[75].Time);
        measurementTasks.AddMarker(task.Id,"切换模型",demoHistory[80].Time);
        preferences.MeasurementBaselineUtc=start.ToString("O");
        await RefreshMiniHistoryAsync();
        Check(HasActiveMeasurement,"Active task linked to main panel");
        OpenTasks(this,new RoutedEventArgs()); await tasksWindow!.ReloadAsync();
        tasksWindow.TaskListForReview(task.Id); await Task.Delay(150);
        CaptureControl(tasksWindow,Path.Combine(directory,"tasks-markers-matrix.png")); tasksWindow.Close();
        preferences.MiniDualMetrics=true; preferences.MiniShowRecordDelta=true; SetDisplayMode("mini"); await RefreshMiniHistoryAsync(); RefreshUsageProgress(); await Task.Delay(100);
        Check(MiniUsageProgress.Text.Contains("本月"),"Mini task primary and monthly secondary"); Capture(Path.Combine(directory,"mini-dual-task.png"));
        preferences.MiniShowRecordDelta=false; RefreshUsageProgress(); RefreshDisplayEffects(); await Task.Delay(100);
        Check(MiniUsageProgress.Text.Contains("任务"),"Mini monthly primary and task secondary"); Capture(Path.Combine(directory,"mini-dual-month.png"));
        ToggleAmountPrivacy(); await RefreshMiniHistoryAsync();
        Check(Money(1234.56m)=="••••","Money formatting hidden");
        Check(!MiniUsageProgress.Text.Any(char.IsDigit),"Mini secondary amount hidden");
        MiniDisplay.UpdateLayout();
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)MiniDisplay.ActualWidth,(int)MiniDisplay.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(MiniDisplay);
        var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(8,40,1,1),pixel,4,0);
        Check(pixel[3] > 220,"Hidden mini amount retains opaque skin background");
        Check(MiniUsageProgress.ActualHeight >= 12,"Mini secondary label has room for text");
        var miniMenu=BuildMiniMetricMenu(false);
        var dual=miniMenu.Items.OfType<MenuItem>().Single(i=>i.Tag?.ToString()=="dual");
        dual.IsChecked=false; dual.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(!preferences.MiniDualMetrics,"Mini menu disables dual metrics");
        dual.IsChecked=true; dual.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(preferences.MiniDualMetrics,"Mini menu enables dual metrics");
        var primary=miniMenu.Items.OfType<MenuItem>().ToArray();
        primary.Single(i=>i.Tag?.ToString()=="task").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(ShowMiniTaskDelta && MiniUsageProgress.Text.Contains("本月"),"Mini menu selects task primary");
        primary.Single(i=>i.Tag?.ToString()=="total").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Check(!ShowMiniTaskDelta && MiniUsageProgress.Text.Contains("任务"),"Mini menu selects monthly primary");
        Capture(Path.Combine(directory,"mini-privacy.png")); ToggleAmountPrivacy();
        SetDisplayMode("compact"); SetDisplayMode("mini"); ExpandMini(this,new RoutedEventArgs()); Check(preferences.DisplayMode=="compact","Mini restores compact panel");
        SelectMiniMetric("7d", false); await RefreshMiniHistoryAsync();
        Check(ShowMiniHistoryDelta && MiniDisplay.Value == miniTrendDelta && MiniDisplay.MetricCaption.Contains("7"), "Mini selects seven-day observed delta");
        SetDisplayMode("mini");
        Capture(Path.Combine(directory,"mini-seven-day.png"));
        var chooser = BuildMiniMetricMenu(false); chooser.PlacementTarget = MiniHost; chooser.IsOpen = true; await Task.Delay(150);
        Check(chooser.Items.OfType<MenuItem>().Single(i => i.Tag?.ToString() == "7d").IsChecked,"Selected mini range marked in dropdown");
        CaptureControl(chooser,Path.Combine(directory,"mini-metrics-menu-zh.png")); chooser.IsOpen = false;
        UiText.Language = "en"; var englishChooser = BuildMiniMetricMenu(false); englishChooser.PlacementTarget = MiniHost; englishChooser.IsOpen = true; await Task.Delay(100);
        Check(englishChooser.Items.OfType<MenuItem>().Single(i => i.Tag?.ToString() == "today").Header.ToString()!.Contains("Today"),"Mini metric menu translated into English");
        CaptureControl(englishChooser,Path.Combine(directory,"mini-metrics-menu-en.png")); englishChooser.IsOpen = false; UiText.Language = "zh-CN";
        SelectMiniMetric("task", false); await RefreshMiniHistoryAsync();
        preferences.MeasurementBaselineUtc=""; preferences.MiniShowRecordDelta=true; RefreshMiniRecordDeltaButton(); RefreshUsageProgress();
        Check(!ShowMiniTaskDelta && !preferences.MiniShowRecordDelta,"Task end clears unavailable mini task metric");
        Check(!ShowMiniHistoryDelta && MiniDisplay.Value == data!.Total,"Task end restores monthly total");
        Check(!BuildMiniMetricMenu(false).Items.OfType<MenuItem>().Single(i=>i.Tag?.ToString()=="task").IsEnabled,"Task metric disabled when no task is active");
        SelectMiniMetric("total", false); await RefreshMiniHistoryAsync();
        Check(MiniUsageProgress.Text.Contains("今天"),"No-task dual metrics use today's delta");
        SelectMiniSecondaryMetric("7d"); await RefreshMiniHistoryAsync(); await Task.Delay(100);
        Check(!HasActiveMeasurement && !ShowMiniHistoryDelta && MiniDisplay.Value == data!.Total && MiniUsageProgress.Text.Contains("7") && miniSecondaryDelta.HasValue, "Monthly total plus seven-day secondary works without a task");
        Check(MiniUsageProgress.Text.Contains(HistoryChart.Signed(miniSecondaryDelta!.Value)), "Seven-day secondary shows computed delta");
        MiniUsageProgress.UpdateLayout();
        Check(MiniUsageProgress.ActualHeight >= 16 && MiniUsageProgress.ActualWidth > 150, "Secondary metric has visible readable space");
        Capture(Path.Combine(directory,"mini-month-seven-days.png"));
        var secondaryChoice = BuildMiniMetricMenu(false).Items.OfType<MenuItem>().Single(i=>i.Tag?.ToString()=="secondary");
        secondaryChoice.Items.OfType<MenuItem>().Single(i=>i.Tag?.ToString()=="today").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        await RefreshMiniHistoryAsync();
        Check(preferences.MiniDualMetrics && MiniUsageProgress.Text.Contains("今天") && !HasActiveMeasurement, "Secondary menu chooses today independently without task measurement");
        Capture(Path.Combine(directory,"mini-month-today.png"));
        var savedMetric = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(preferences))!;
        Check(savedMetric.MiniSecondaryMetric == "today", "Secondary metric choice persists");
        ToggleAmountPrivacy();
        Check(!MiniUsageProgress.Text.Any(char.IsDigit), "Explicit secondary metric respects amount privacy");
        ToggleAmountPrivacy();
        OpenWorkspace(); await Task.Delay(200);
        Check(settingsWindow!=null,"Tasks and desktop integrated into settings"); CaptureControl(settingsWindow!,Path.Combine(directory,"workspace-options-matrix.png")); settingsWindow!.Close();
        OpenInsights(1); await Task.Delay(150); Check(insightsWindow!=null && !insightsWindow.ActivityReadyForReview,"Forecast opens without loading activity history");
        OpenInsights(3);
        for(int attempt=0; attempt<40 && !insightsWindow!.ActivityReadyForReview; attempt++) await Task.Delay(100);
        Check(insightsWindow!.ActivityReadyForReview,"Monthly records and heatmap load inside forecast"); await Task.Delay(200);
        System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject node)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(node); index++)
            {
                var child = VisualTreeHelper.GetChild(node, index); yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }
        var days = Descendants(insightsWindow!).OfType<Button>().Where(b => b.Tag is DailyUsage).ToArray();
        var measuredDay = days.First(b => b.Tag is DailyUsage d && d.Delta.HasValue);
        var daily = (DailyUsage)measuredDay.Tag;
        measuredDay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var selectedDetail = Descendants(insightsWindow!).OfType<TextBlock>().Single(t => t.Tag?.ToString() == "heatmap-detail");
        Check(selectedDetail.Text.Contains(daily.Date.ToString("MM/dd")) && selectedDetail.Text.Contains(daily.Delta!.Value.ToString("C2",Us)), "Clicking heatmap day displays its date and exact observed change");
        Check(Descendants(measuredDay).OfType<TextBlock>().Any(t => t.Text == daily.Delta!.Value.ToString("0.00",Us)), "Heatmap cells show daily amount without hover");
        var missingDay = days.First(b => b.Tag is DailyUsage d && !d.Delta.HasValue);
        missingDay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(selectedDetail.Text.Contains("无连续采样"), "Missing daily samples are not shown as zero");
        measuredDay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        CaptureControl(insightsWindow!,Path.Combine(directory,"heatmap-archives-matrix.png")); insightsWindow!.Close();
        preferences.HideAmounts = true;
        // Open the renderer directly: normal navigation deliberately blocks amount details in privacy mode.
        var privateWindow = new InsightsWindow(measurementTasks, () => Task.FromResult<System.Collections.Generic.IReadOnlyList<HistoryFrame>>(demoHistory!), () => data, BuildDiagnosticReport, 3, LoadActivityAsync) { Owner = this };
        privateWindow.Show();
        for (int attempt = 0; attempt < 40 && !privateWindow.ActivityReadyForReview; attempt++) await Task.Delay(100);
        var privateDay = Descendants(privateWindow).OfType<Button>().First(b => b.Tag is DailyUsage d && d.Delta.HasValue);
        privateDay.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(Descendants(privateDay).OfType<TextBlock>().Any(t => t.Text == "••••") && Descendants(privateWindow).OfType<TextBlock>().Single(t => t.Tag?.ToString() == "heatmap-detail").Text.Contains("••••") && privateDay.ToolTip.ToString()!.Contains("••••"), "Heatmap privacy covers cell, selected detail and tooltip");
        privateWindow.Close(); preferences.HideAmounts = false;
        preferences.DesktopLocked=true; RestoreDesktopInteraction(); Check(!preferences.DesktopLocked && !clickThrough,"Desktop interaction recovery");
        ShowWhatsNew(); await Task.Delay(100); var card=OwnedWindows.Cast<Window>().First(w=>w.Title.Contains("这次改了什么")); CaptureControl(card,Path.Combine(directory,"whats-new-matrix.png")); card.Close();
        File.AppendAllText(Path.Combine(directory,"workspace-checks.txt"),"TOTAL "+checks+" checks\n");
    }
}
