using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CopilotPulse;

public partial class MainWindow
{
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly CancellationTokenSource updateCancel = new();
    private readonly UpdateService autoUpdates = new();
    private AutoUpdateState updateState = new();
    private UpdateRelease? availableUpdate;
    private UpdateWindow? updateWindow;
    private AboutWindow? aboutWindow;
    private string? stagedInstaller;
    private bool checkingUpdate, freshUpdateCheck, installingUpdate;

    private void StartAutomaticUpdates()
    {
        if (App.Demo || App.Smoke) return;
        updateState = Store.Read<AutoUpdateState>("update-state.json") ?? new();
        if (!Version.TryParse(updateState.Version, out var pending) || pending <= UpdatePolicy.Current)
        {
            AutoUpdatePolicy.Observe(updateState, null, preferences.UpdateRepository, DateTimeOffset.UtcNow);
            updateState.LastAttemptUtc = null;
            Store.Write("update-state.json", updateState);
        }
        RefreshUpdateBadge();
        updateTimer.Tick += async (_, _) => await PollUpdatesAsync();
        updateTimer.Start();
        _ = PollUpdatesAsync();
    }
    private void StopAutomaticUpdates()
    {
        updateTimer.Stop(); updateCancel.Cancel(); updateWindow?.Close(); aboutWindow?.Close();
    }
    private async Task PollUpdatesAsync()
    {
        if (closing || checkingUpdate || installingUpdate || updateWindow != null) return;
        checkingUpdate = true;
        try
        {
            string repository = UpdatePolicy.Repository(preferences.UpdateRepository);
            if (updateState.Repository != repository) { updateState = new(); availableUpdate = null; stagedInstaller = null; freshUpdateCheck = false; }
            if (AutoUpdatePolicy.CheckDue(updateState, DateTimeOffset.UtcNow))
            {
                updateState.LastAttemptUtc = DateTimeOffset.UtcNow;
                Store.Write("update-state.json", updateState);
                freshUpdateCheck = false;
                var release = await autoUpdates.CheckAsync(repository, updateCancel.Token);
                if (closing || repository != UpdatePolicy.Repository(preferences.UpdateRepository)) return;
                if (availableUpdate != release) stagedInstaller = null;
                availableUpdate = release; freshUpdateCheck = true;
                AutoUpdatePolicy.Observe(updateState, release, repository, DateTimeOffset.UtcNow);
                Store.Write("update-state.json", updateState);
                RefreshUpdateBadge();
            }
            if (availableUpdate != null && freshUpdateCheck && !updateState.Acknowledged && stagedInstaller == null)
                stagedInstaller = await autoUpdates.DownloadAsync(availableUpdate, updateCancel.Token);
            if (!closing && updateWindow == null && AutoUpdatePolicy.InstallDue(updateState, availableUpdate, DateTimeOffset.UtcNow, freshUpdateCheck) && stagedInstaller != null)
            {
                installingUpdate = true;
                RefreshUpdateBadge();
                try { await UpdateHandoff.LaunchAsync(stagedInstaller, availableUpdate!); if (!closing) Application.Current.Shutdown(); }
                catch { installingUpdate = false; stagedInstaller = null; freshUpdateCheck = false; }
            }
        }
        catch (Exception) { /* Retain the notice; retry on the next hourly check. No installation after a failed check. */ freshUpdateCheck = false; }
        finally { checkingUpdate = false; if (!closing) RefreshUpdateBadge(); }
    }
    private void RefreshUpdateBadge()
    {
        bool visible = Version.TryParse(updateState.Version, out var version) && version > UpdatePolicy.Current && updateState.Repository == UpdatePolicy.Repository(preferences.UpdateRepository);
        NewVersionButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Controls.Grid.SetRow(HeaderActions, visible ? 1 : 0);
        System.Windows.Controls.Grid.SetColumn(HeaderActions, visible ? 0 : 1);
        System.Windows.Controls.Grid.SetColumnSpan(HeaderActions, visible ? 2 : 1);
        HeaderActions.HorizontalAlignment = HorizontalAlignment.Right;
        HeaderActions.Margin = visible ? new Thickness(0, 8, 0, 0) : new Thickness(0);
        NewVersionButton.IsEnabled = !installingUpdate;
        NewVersionButton.Content = UiText.T("有新版本", "Update");
        string deadline = updateState.FirstSeenUtc.HasValue ? (updateState.FirstSeenUtc.Value + AutoUpdatePolicy.GracePeriod).ToLocalTime().ToString("MM-dd HH:mm") : "—";
        NewVersionButton.ToolTip = UiText.T($"版本 {updateState.Version} · " + (updateState.Acknowledged ? "点击手动更新" : $"未点击将在 {deadline} 自动安装并重启"), $"Version {updateState.Version} · " + (updateState.Acknowledged ? "Click to install" : $"Automatic install and restart at {deadline} unless opened"));
    }
    internal void OpenAbout()
    {
        if (aboutWindow != null) { aboutWindow.Activate(); return; }
        aboutWindow = new AboutWindow(preferences, OpenUpdates) { Owner = this };
        aboutWindow.Closed += (_, _) => aboutWindow = null;
        aboutWindow.Show();
    }
    private void NewVersionClick(object sender, RoutedEventArgs e) => OpenUpdates();
    internal void OpenUpdates()
    {
        if (installingUpdate) return;
        if (updateWindow != null) { updateWindow.Activate(); return; }
        if (updateState.FirstSeenUtc.HasValue) { updateState.Acknowledged = true; if (!App.Demo && !App.Smoke) Store.Write("update-state.json", updateState); }
        RefreshUpdateBadge();
        updateWindow = new UpdateWindow(preferences, availableUpdate, stagedInstaller) { Owner = this };
        updateWindow.Closed += (_, _) => { updateWindow = null; RefreshUpdateBadge(); };
        updateWindow.Show();
    }
    private void RefreshUsageProgress()
    {
        string text = "";
        string mini = "";
        if (data != null)
        {
            if (preferences.WarningEnabled && preferences.WarningMode == "amount")
            {
                decimal percent = data.Total / preferences.WarningAmount * 100;
                text = UiText.T($"已达金额阈值的 {percent:0.00}% · 阈值 {Money(preferences.WarningAmount)}", $"{percent:0.00}% of amount threshold · {Money(preferences.WarningAmount)}");
                mini = UiText.T($"阈值进度 {percent:0.00}%", $"Threshold {percent:0.00}%");
            }
            else
            {
                var available = data.Accounts.Select(SettingsPolicy.QuotaPercent).Where(p => p.HasValue).Select(p => p!.Value).ToArray();
                if (available.Length > 0)
                {
                    decimal max = available.Max();
                    text = UiText.T($"最高账户配额使用 {max:0.00}%", $"Highest account quota used {max:0.00}%");
                    if (preferences.WarningEnabled) text += UiText.T($" · 警告线 {preferences.WarningPercent:0.##}%", $" · Warn at {preferences.WarningPercent:0.##}%");
                    mini = UiText.T($"最高配额 {max:0.00}%", $"Max quota {max:0.00}%");
                }
                else
                {
                    text = UiText.T("页面未提供具体配额，百分比不可计算", "No numeric quota; percentage unavailable");
                    mini = UiText.T("配额百分比 —", "Quota —");
                }
            }
            if (data.FetchedAt.UtcDateTime.Month != DateTime.UtcNow.Month || data.FetchedAt.UtcDateTime.Year != DateTime.UtcNow.Year)
                text += UiText.T(" · 上月缓存", " · Previous month cached");
        }
        UsageProgress.Text = text;
        UsageProgress.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        bool warningActive = data != null && usageWarnings.Active(data, preferences).Count > 0;
        if (warningActive)
        {
            UsageProgress.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 122));
            MiniUsageProgress.Foreground = new SolidColorBrush(Color.FromRgb(255, 215, 122));
            MiniUsageProgress.FontWeight = FontWeights.Bold;
            MiniUsageProgress.Background = new SolidColorBrush(Color.FromRgb(56, 42, 18));
            MiniUsageProgress.Padding = new Thickness(4, 1, 4, 1);
            mini = "⚠ " + mini;
        }
        else
        {
            UsageProgress.SetResourceReference(TextBlock.ForegroundProperty, "SkinAccent");
            MiniUsageProgress.SetResourceReference(TextBlock.ForegroundProperty, "SkinText");
            MiniUsageProgress.FontWeight = FontWeights.Normal;
            MiniUsageProgress.Background = Brushes.Transparent;
            MiniUsageProgress.Padding = new Thickness(0);
        }
        if (preferences.MiniShowRecordDelta)
        {
            string recorded = !DateTimeOffset.TryParse(preferences.MeasurementBaselineUtc, out _)
                ? UiText.T("记录区间 · 请先增加记录点", "Session · Add a record point first")
                : !miniBaselineDelta.HasValue
                    ? UiText.T("记录区间 · 等待有效采样", "Session · Waiting for valid samples")
                    : UiText.T("记录区间增量", "Recorded period delta");
            mini = warningActive ? $"⚠ {recorded}" : recorded;
        }
        MiniUsageProgress.Text = mini;
        MiniUsageProgress.ToolTip = text;
        RefreshNumericState();
    }
}
