using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CopilotPulse;

internal sealed class UpdateWindow : Window
{
    private readonly UpdateService service = new();
    private readonly CancellationTokenSource cancel = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12) };
    private readonly Button check = new(), download = new(), install = new();
    private UpdateRelease? release;
    private string? installer;
    private readonly Preferences preferences;
    internal UpdateWindow(Preferences preferences, UpdateRelease? initialRelease = null, string? initialInstaller = null)
    {
        this.preferences = preferences;
        Title = UiText.T("Copilot Pulse · 在线更新", "Copilot Pulse · Online updates"); Icon = BrandIcon.Window;
        Width = 510; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "SkinPanel"); SetResourceReference(ForegroundProperty, "SkinText");
        var body = new StackPanel { Margin = new Thickness(24) }; Content = body;
        body.Children.Add(new TextBlock { Text = UiText.T("在线更新", "Online updates"), FontSize = 24, FontWeight = FontWeights.SemiBold });
        body.Children.Add(new TextBlock { Text = UiText.T($"当前版本 {UpdatePolicy.Current} · Windows x64", $"Current version {UpdatePolicy.Current} · Windows x64"), Margin = new Thickness(0,10,0,12) });
        var repo = new ComboBox { ItemsSource = new[] { "andylu1988/copilot-pulse", "andylu_microsoft/copilot-pulse" }, SelectedIndex = preferences.UpdateRepository == "andylu_microsoft/copilot-pulse" ? 1 : 0, Background = Brushes.White, Foreground = Brushes.Black, MinHeight = 30 };
        body.Children.Add(repo);
        repo.SelectionChanged += (_, _) => { preferences.UpdateRepository = (string)repo.SelectedItem; if (!App.Demo && !App.Smoke) Store.Write("preferences.json", preferences); release = null; installer = null; download.IsEnabled = false; install.IsEnabled = false; status.Text = UiText.T("请选择检查更新。", "Select Check for updates."); };
        body.Children.Add(status);
        status.Text = UiText.T("默认从个人公开仓库检查正式版本，无需登录 GitHub。EMU 私有仓库需要有权限的 GitHub CLI 账户。", "Checks stable releases from the public personal repository without GitHub sign-in. The private EMU mirror needs an authorized GitHub CLI account.");
        var buttons = new WrapPanel(); body.Children.Add(buttons);
        void Add(Button button, string zh, string en) { button.Content = UiText.T(zh,en); button.Padding = new Thickness(10,8,10,8); button.Margin = new Thickness(0,0,6,8); buttons.Children.Add(button); }
        Add(check,"检查更新","Check for updates"); Add(download,"下载更新","Download"); Add(install,"安装并重启","Install and restart");
        download.IsEnabled = install.IsEnabled = false;
        var browser = new Button(); Add(browser,"浏览器下载 ↗","Browser download ↗");
        browser.Click += (_, _) => Process.Start(new ProcessStartInfo(UpdatePolicy.ReleasePage(preferences.UpdateRepository)) { UseShellExecute = true });
        body.Children.Add(new TextBlock { Text = UiText.T("每小时检查正式版本，未点击提醒满 48 小时后自动升级。点击安装后保存并关闭小组件，安装完成自动重新打开；历史、登录和偏好设置保留。打开更新窗口会取消当前版本的自动安装。", "Checks hourly. Unopened notices install automatically after 48 hours. Install saves and closes the widget, verifies SHA256, then reopens it after setup. History, sign-in and preferences are retained. Opening updates cancels automatic installation for the current version."), FontSize = 11, TextWrapping = TextWrapping.Wrap });
        check.Click += async (_, _) =>
        {
            check.IsEnabled = repo.IsEnabled = download.IsEnabled = install.IsEnabled = false; release = null; installer = null;
            status.Text = UiText.T("正在检查…","Checking…");
            try { release = await service.CheckAsync(preferences.UpdateRepository, cancel.Token); if (!cancel.IsCancellationRequested) { status.Text = release == null ? UiText.T("当前已是最新正式版本。","You have the latest stable version.") : UiText.T($"新版本 {release.Version} 可用。", $"Version {release.Version} is available."); download.IsEnabled = release != null; } }
            catch (Exception) { if (!cancel.IsCancellationRequested) status.Text = UiText.T("暂时无法检查：请确认网络、仓库访问权限及 GitHub CLI 登录账户，或点击浏览器下载。", "Unable to check. Verify network, repository access and GitHub CLI account, or use browser download."); }
            finally { check.IsEnabled = repo.IsEnabled = true; }
        };
        download.Click += async (_, _) =>
        {
            if (release == null) return;
            download.IsEnabled = check.IsEnabled = repo.IsEnabled = false; status.Text = UiText.T("正在下载并校验…", "Downloading and verifying…");
            try { installer = await service.DownloadAsync(release, cancel.Token); if (!cancel.IsCancellationRequested) { status.Text = UiText.T("校验通过，点击安装开始升级。", "Verified. Select Install to upgrade."); install.IsEnabled = true; } }
            catch (Exception) { if (!cancel.IsCancellationRequested) { status.Text = UiText.T("下载或校验失败，请重试或使用浏览器下载。", "Download or verification failed. Retry or use browser download."); download.IsEnabled = true; } }
            finally { check.IsEnabled = repo.IsEnabled = true; }
        };
        install.Click += async (_, _) =>
        {
            if (installer == null || release == null) return;
            install.IsEnabled = download.IsEnabled = check.IsEnabled = repo.IsEnabled = false;
            try { await UpdateHandoff.LaunchAsync(installer, release); Application.Current.Shutdown(); }
            catch (Exception) { status.Text = UiText.T("无法启动安装，请重新下载。", "Unable to launch installer. Download again."); check.IsEnabled = repo.IsEnabled = true; }
        };
        if (initialRelease != null && initialRelease.Repository == UpdatePolicy.Repository(preferences.UpdateRepository))
        {
            release = initialRelease; installer = initialInstaller;
            status.Text = UiText.T($"新版本 {release.Version} 可用。", $"Version {release.Version} is available.");
            download.IsEnabled = installer == null; install.IsEnabled = installer != null;
        }
        Closed += (_, _) => cancel.Cancel();
    }
}
