using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CopilotPulse;

internal sealed class AboutWindow : Window
{
    private readonly Preferences preferences;
    private readonly UpdateService service = new();
    private readonly CancellationTokenSource cancel = new();
    private readonly TextBlock cloud = new(), checkedAt = new(), status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button refresh = new();
    private bool checking;
    internal AboutWindow(Preferences preferences, Action openUpdates)
    {
        this.preferences = preferences;
        Title = UiText.T("Copilot Pulse · 关于", "Copilot Pulse · About"); Icon = BrandIcon.Window;
        Width = 510; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "SkinPanel"); SetResourceReference(ForegroundProperty, "SkinText");
        var body = new StackPanel { Margin = new Thickness(26) }; _ = new UtilitySkin(this, body);
        var brand = new TextBlock { Text = "Copilot Pulse", FontSize = 28, FontWeight = FontWeights.SemiBold }; brand.SetResourceReference(TextBlock.ForegroundProperty, "SkinAccent"); body.Children.Add(brand);
        body.Children.Add(new TextBlock { Text = UiText.T("桌面上的 Copilot 用量脉搏", "Your Copilot usage, on the desktop"), Margin = new Thickness(0,8,0,20) });
        void Row(string label, TextBlock value)
        {
            var grid = new Grid { Margin = new Thickness(0,6,0,6) }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.Children.Add(new TextBlock { Text = label, Opacity = .7 }); value.TextWrapping = TextWrapping.Wrap; Grid.SetColumn(value,1); grid.Children.Add(value); body.Children.Add(grid);
        }
        Row(UiText.T("当前本地版本", "Local version"), new TextBlock { Text = UpdatePolicy.Current + " · Windows x64" });
        cloud.Text = UiText.T("等待检查…", "Waiting to check…"); Row(UiText.T("云端最新正式版", "Latest stable version"), cloud);
        checkedAt.Text = "—"; Row(UiText.T("上次检查时间", "Last checked"), checkedAt);
        Row(UiText.T("项目作者", "Author"), new TextBlock { Text = "Andy Lu" });
        Row(UiText.T("更新来源", "Update source"), new TextBlock { Text = UpdatePolicy.Repository(preferences.UpdateRepository) });
        status.Margin = new Thickness(0,12,0,14); status.FontSize = 12; body.Children.Add(status);
        var buttons = new WrapPanel(); body.Children.Add(buttons);
        void Button(Button button, string text, Action action)
        {
            button.Content = text; button.Padding = new Thickness(12,8,12,8); button.Margin = new Thickness(0,0,8,8);
            button.SetResourceReference(Control.BackgroundProperty,"SkinPanel"); button.SetResourceReference(Control.ForegroundProperty,"SkinText"); button.SetResourceReference(Control.BorderBrushProperty,"SkinAccent");
            button.Click += (_,_) => action(); buttons.Children.Add(button);
        }
        Button(refresh, UiText.T("检查最新版本", "Check latest version"), () => _ = CheckLatestAsync());
        Button(new Button(), UiText.T("在线更新", "Online updates"), openUpdates);
        Button(new Button(), UiText.T("项目主页 ↗", "Project ↗"), () => Process.Start(new ProcessStartInfo("https://github.com/andylu1988/copilot-pulse") { UseShellExecute = true }));
        body.Children.Add(new TextBlock { Text = UiText.T("非官方工具 · 本机保存用量历史与设置。每小时检查新版本，未打开提醒满 48 小时后自动安装并重启客户端。", "Unofficial tool. Usage history and preferences are stored locally. Checks hourly; unopened update notices install and restart after 48 hours."), FontSize = 11, Opacity = .7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,0) });
        Loaded += async (_,_) => await CheckLatestAsync(); Closed += (_,_) => cancel.Cancel();
    }
    internal async Task CheckLatestAsync()
    {
        if (checking || cancel.IsCancellationRequested) return;
        checking = true; refresh.IsEnabled = false; status.Text = UiText.T("正在检查云端正式版本…", "Checking the latest stable release…");
        try
        {
            Version? latest;
            if (App.Demo || App.Smoke) latest = UpdatePolicy.Current;
            else latest = (await service.LatestAsync(preferences.UpdateRepository,cancel.Token))?.Version;
            if (cancel.IsCancellationRequested) return;
            cloud.Text = latest?.ToString() ?? UiText.T("没有可用的正式安装包", "No stable installer available");
            checkedAt.Text = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
            status.Text = latest == null ? UiText.T("云端尚未提供正式安装包。", "No stable installer has been published.") : latest > UpdatePolicy.Current ? UiText.T("有新版本，可以打开在线更新。", "A new version is available. Open Online updates.") : UiText.T("当前本地版本已是最新。", "Your local version is up to date.");
            if (App.Demo || App.Smoke) status.Text += UiText.T("（演示数据）", " (demo data)");
        }
        catch (Exception)
        {
            if (!cancel.IsCancellationRequested) { cloud.Text = UiText.T("暂时无法检查", "Unavailable"); status.Text = UiText.T("请检查网络和仓库访问权限，再试一次。", "Check your network and repository access, then retry."); }
        }
        finally { checking = false; refresh.IsEnabled = true; }
    }
}
