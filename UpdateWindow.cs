using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CopilotPulse;

internal sealed record UpdateActions(
    Func<string, CancellationToken, Task<UpdateRelease?>> Check,
    Func<UpdateRelease, CancellationToken, Task<string>> Download,
    Func<string, UpdateRelease, Task> Apply,
    Action Complete);

internal sealed class UpdateWindow : Window
{
    private readonly UpdateService service = new();
    private readonly CancellationTokenSource cancel = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12) };
    private readonly Button check = new(), install = new();
    private readonly ComboBox repo;
    private readonly UpdateActions actions;
    private bool busy, applying;
    internal Task? PendingUpdate { get; private set; }
    private UpdateRelease? release;
    private string? installer;
    private readonly Preferences preferences;
    internal UpdateWindow(Preferences preferences, UpdateRelease? initialRelease = null, string? initialInstaller = null, UpdateActions? actions = null)
    {
        this.preferences = preferences;
        this.actions = actions ?? new UpdateActions(service.CheckAsync, service.DownloadAsync,
            (path, available) => UpdateHandoff.LaunchAsync(path, available), () => Application.Current.Shutdown());
        Title = UiText.T("Copilot Pulse · 在线更新", "Copilot Pulse · Online updates"); Icon = BrandIcon.Window;
        Width = 510; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "SkinPanel"); SetResourceReference(ForegroundProperty, "SkinText");
        var body = new StackPanel { Margin = new Thickness(24) }; Content = body;
        body.Children.Add(new TextBlock { Text = UiText.T("在线更新", "Online updates"), FontSize = 24, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = UiText.T($"当前版本 {UpdatePolicy.Current} · Windows x64", $"Current version {UpdatePolicy.Current} · Windows x64"), Margin = new Thickness(0,10,0,12) });
        repo = new ComboBox { ItemsSource = new[] { "andylu1988/copilot-pulse", "andylu_microsoft/copilot-pulse" }, SelectedIndex = preferences.UpdateRepository == "andylu_microsoft/copilot-pulse" ? 1 : 0, Background = Brushes.White, Foreground = Brushes.Black, MinHeight = 30 };
        body.Children.Add(repo);
        repo.SelectionChanged += (_, _) => { preferences.UpdateRepository = (string)repo.SelectedItem; if (!App.Demo && !App.Smoke) Store.Write("preferences.json", preferences); release = null; installer = null; install.IsEnabled = true; status.Text = UiText.T("请选择检查更新。", "Select Check for updates."); };
        body.Children.Add(status);
        status.Text = UiText.T("默认从个人公开仓库检查正式版本，无需登录 GitHub。EMU 私有仓库需要有权限的 GitHub CLI 账户。", "Checks stable releases from the public personal repository without GitHub sign-in. The private EMU mirror needs an authorized GitHub CLI account.");
        var buttons = new WrapPanel(); body.Children.Add(buttons);
        void Add(Button button, string zh, string en) { button.Content = UiText.T(zh,en); button.Padding = new Thickness(10,8,10,8); button.Margin = new Thickness(0,0,6,8); buttons.Children.Add(button); }
        Add(install,"一键更新并重启","Update and restart"); Add(check,"检查更新","Check for updates");
        var browser = new Button(); Add(browser,"浏览器下载 ↗","Browser download ↗");
        browser.Click += (_, _) => Process.Start(new ProcessStartInfo(UpdatePolicy.ReleasePage(preferences.UpdateRepository)) { UseShellExecute = true });
        body.Children.Add(new TextBlock { Text = UiText.T("每小时检查正式版本，未点击提醒满 48 小时后自动升级。点击“一键更新并重启”后自动检查、下载、校验、安装并重新打开；历史、登录和偏好设置保留。打开更新窗口会取消当前版本的自动安装。", "Checks hourly. Unopened notices install automatically after 48 hours. Update and restart checks, downloads and verifies SHA256, then silently installs and reopens the widget. History, sign-in and preferences are retained. Opening updates cancels automatic installation for the current version."), FontSize = 11, TextWrapping = TextWrapping.Wrap });
        check.Click += async (_, _) =>
        {
            if (busy) return;
            SetBusy(true); release = null; installer = null;
            status.Text = UiText.T("正在检查…", "Checking…");
            try
            {
                release = await this.actions.Check(preferences.UpdateRepository, cancel.Token);
                if (!cancel.IsCancellationRequested) status.Text = release == null
                    ? UiText.T("当前已是最新正式版本。", "You have the latest stable version.")
                    : UiText.T($"新版本 {release.Version} 可用，点击一键更新。", $"Version {release.Version} is available. Select Update and restart.");
            }
            catch (Exception) { if (!cancel.IsCancellationRequested) status.Text = UiText.T("检查失败，请检查网络或仓库权限后重试。", "Check failed. Verify network or repository access and retry."); }
            finally { SetBusy(false); }
        };
        install.Click += (_, _) => PendingUpdate = UpdateAsync();
        if (initialRelease != null && initialRelease.Repository == UpdatePolicy.Repository(preferences.UpdateRepository))
        {
            release = initialRelease; installer = initialInstaller;
            status.Text = UiText.T($"新版本 {release.Version} 可用。", $"Version {release.Version} is available.");
            install.IsEnabled = true;
        }
        Closing += (_, e) => { if (applying) e.Cancel = true; };
        Closed += (_, _) => cancel.Cancel();
    }    private void SetBusy(bool value)
    {
        busy = value;
        check.IsEnabled = repo.IsEnabled = install.IsEnabled = !value;
    }
    private async Task UpdateAsync()
    {
        if (busy) return;
        SetBusy(true);
        string phase = "check";
        try
        {
            status.Text = UiText.T("正在检查最新版本…", "Checking the latest release…");
            var latest = await actions.Check(preferences.UpdateRepository, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            if (latest == null)
            {
                status.Text = UiText.T("当前已是最新正式版本，无需更新。", "You already have the latest stable version.");
                return;
            }
            if (release != latest) installer = null;
            release = latest;
            phase = "download";
            status.Text = UiText.T($"正在下载并校验 {latest.Version}…", $"Downloading and verifying {latest.Version}…");
            if (installer == null || !File.Exists(installer)) installer = await actions.Download(latest, cancel.Token);
            if (cancel.IsCancellationRequested) return;
            phase = "install";
            applying = true;
            status.Text = UiText.T("即将安装并自动重启，请稍候…", "Installing and restarting automatically…");
            await actions.Apply(installer, latest);
            applying = false;
            actions.Complete();
        }
        catch (Exception)
        {
            installer = null;
            if (!cancel.IsCancellationRequested) status.Text = phase switch
            {
                "check" => UiText.T("检查失败，请检查网络或仓库权限后重试。", "Check failed. Verify network or repository access and retry."),
                "download" => UiText.T("下载或校验失败，当前版本未修改。点击一键更新重试。", "Download or verification failed; your installed version is unchanged. Retry Update and restart."),
                _ => UiText.T("无法启动安装，当前客户端保留。点击一键更新重试。", "Unable to start setup; the current client is retained. Retry Update and restart.")
            };
        }
        finally { applying = false; SetBusy(false); }
    }
}
