using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CopilotPulse;

public partial class MainWindow : Window
{
    private readonly Preferences preferences;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private SourceMonitor? monitor;
    private Forms.NotifyIcon? tray;
    private System.Drawing.Icon? trayIcon;
    private Consumption? data;
    private string status = "connecting";
    private DateTimeOffset nextRefresh;
    private bool paused, closing;
    private int failures;
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public MainWindow()
    {
        InitializeComponent();
        Icon = BrandIcon.Window;
        MaxHeight = SystemParameters.WorkArea.Height;
        preferences = App.Demo || App.Smoke ? new Preferences() : Store.Read<Preferences>("preferences.json") ?? new Preferences();
        SettingsPolicy.Normalize(preferences);
        UiText.Language = preferences.Language;
        Topmost = preferences.Topmost;
        PinButton.Foreground = Brush(Topmost ? "#83D6EC" : "#7186A6");
        if (App.StartSkin != null) preferences.SkinId = SkinTheme.Find(App.StartSkin).Id;
        MonthLabel.Text = DateTime.UtcNow.ToString("MMM yyyy", Us).ToUpperInvariant();
        Surface.SizeChanged += (_, _) => ApplyPhotoBackground();
        InitializeSkin();
        InitializeDisplayMode();
        UiText.Apply(this);
        Loaded += LoadedAsync;
        Closing += (_, _) =>
        {
            closing = true;
            timer.Stop();
            StopAutomaticUpdates();
            skinWindow?.Close();
            settingsWindow?.Close();
            skinSaveTimer.Stop();
            historyWindow?.Close();
            recordingWindow?.Close();
            if (history != null) history.Changed -= HistoryChanged;
            SavePreferences();
            monitor?.Dispose();
            tray?.Dispose();
            trayIcon?.Dispose();
        };
        LocationChanged += (_, _) => { if (IsLoaded && !App.Demo && !App.Smoke) { preferences.Left = Left; preferences.Top = Top; } };
    }

    private async void LoadedAsync(object sender, RoutedEventArgs e)
    {
        PlaceOnScreen();
        InitializeHistory();
        if(App.MiniAtStartup || App.StartSkin != null) SavePreferences();
        if (App.Demo)
        {
            DemoBadge.Visibility = Visibility.Visible;
            RenderData(new Consumption
            {
                Ok = true, Total = 1234.56m, FetchedAt = DateTimeOffset.Now,
                Accounts = new() {
                    new Account { Name = "Example Enterprise", Kind = "EMU account", Login = "demo-emu", Cost = 1200m, Limit = 100000, Percent = 1.20 },
                    new Account { Name = "Example Organization", Kind = "GitHub.com account", Login = "demo-user", Cost = 34.56m, Limit = 100000, Percent = 0.03 }
                }
            });
            SetStatus("demo");
            if (App.UpdateReviewInstaller != null)
            {
                string setup = App.UpdateReviewInstaller;
                string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(setup)));
                var reviewRelease = new UpdateRelease(UpdatePolicy.Current, "v" + UpdatePolicy.Current, preferences.UpdateRepository, Path.GetFileName(setup), "", digest);
                await UpdateHandoff.LaunchAsync(setup, reviewRelease, new[] { "--demo", "--capture", Path.Combine(Path.GetDirectoryName(setup)!, "restarted.png") });
                Application.Current.Shutdown(); return;
            }
            if(App.SettingsReviewPath != null) { await ReviewSettingsAsync(App.SettingsReviewPath); Close(); return; }
            if(App.MiniReviewPath != null) { await ReviewMiniAsync(App.MiniReviewPath); Close(); return; }
            if (App.SkinReviewPath != null) { await ReviewSkinsAsync(App.SkinReviewPath); Close(); return; }
            if (App.HistoryReviewPath != null) { await ReviewHistoryAsync(App.HistoryReviewPath); Close(); return; }
            if (App.MenuReviewPath != null) { await ReviewMenusAsync(App.MenuReviewPath); Close(); return; }
            if (App.CapturePath != null) { await Task.Delay(400); Capture(App.CapturePath); Close(); }
            return;
        }
        if (!App.Smoke) CreateTray();
        if (!App.Smoke)
        {
            var cached = Store.Read<Consumption>("consumption.json");
            if (cached?.Ok == true && cached.Accounts.Count > 0) { RecordHistory(cached); RenderData(cached); SetStatus("cached"); }
        }
        monitor = new SourceMonitor();
        monitor.Received += d => { if (closing) return; RecordHistory(d); RenderData(d); EvaluateWarning(true); if (!App.Smoke) Store.Write("consumption.json", d); };
        monitor.Status += s => { if (!closing) SetStatus(s); };
        nextRefresh = DateTimeOffset.Now.AddSeconds(preferences.RefreshSeconds);
        timer.Tick += Tick;
        timer.Start();
        StartAutomaticUpdates();
        await monitor.InitializeAsync();
        if (App.OpenLogin && status == "login") monitor.ShowLogin();
        if (App.HistoryAtStartup && !App.Smoke) OpenHistory(this, new RoutedEventArgs());
        if (App.SkinsAtStartup && !App.Smoke) OpenSkins(this, new RoutedEventArgs());
        if (App.Smoke)
        {
            if (App.SsoCheck) for (int n = 0; n < 40 && status != "live"; n++) await Task.Delay(500);
            File.WriteAllText("smoke-result.json", JsonSerializer.Serialize(new { status, hasData = data != null, accountCount = data?.Accounts.Count ?? 0, ssoEnabled = true, freshProfile = App.SsoCheck, diagnostics = await monitor.GetDiagnosticsAsync() }));
            if (App.CapturePath != null) { await Task.Delay(300); Capture(App.CapturePath); }
            Close();
        }
    }

    private void PlaceOnScreen()
    {
        var area = SystemParameters.WorkArea;
        Left = preferences.Left >= 0 && preferences.Left < area.Right - 50 ? preferences.Left : area.Right - ActualWidth - 24;
        Top = preferences.Top >= area.Top && preferences.Top < area.Bottom - 60 ? preferences.Top : area.Top + 64;
        Top = Math.Max(area.Top, Math.Min(Top, area.Bottom - ActualHeight));
    }

    private static SolidColorBrush Brush(string color) => SkinTheme.Resolve(color);
    private static TextBlock Text(string text, double size, string color, bool bold = false) => new() { Text = text, FontSize = size, Foreground = Brush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
    private static string Money(decimal value) => value.ToString("C2", Us);

    private void RenderData(Consumption received)
    {
        data = received;
        RefreshNumericState();
        AccountsLabel.Text = $"{data.Accounts.Count} 个企业账户";
        MonthLabel.Text = data.FetchedAt.UtcDateTime.ToString("MMM yyyy", Us).ToUpperInvariant();
        EmptyState.Visibility = Visibility.Collapsed;
        AccountCards.Children.Clear();
        for (int i = 0; i < data.Accounts.Count; i++) AccountCards.Children.Add(CreateCard(data.Accounts[i], i));
        EvaluateWarning(false);
        UiText.Apply(this);
        RefreshUpdateBadge();
        UpdateTimestamp();
    }

    private UIElement CreateCard(Account account, int index)
    {
        string accent = index % 2 == 0 ? "#78B6FF" : "#68DAC8";
        var content = new StackPanel();
        var title = new Grid();
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        title.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = Text(account.Name, 13, "#E0E9F8", true);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.ToolTip = account.Name;
        name.Margin = new Thickness(0, 0, 6, 0);
        var badge = new Border { Background = Brush(index % 2 == 0 ? "#223B5B" : "#193F40"), CornerRadius = new CornerRadius(5), Padding = new Thickness(6, 3, 6, 3), Child = Text(account.Kind.StartsWith("EMU") ? "EMU" : "GITHUB.COM", 8, accent, true) };
        Grid.SetColumn(badge, 1);
        title.Children.Add(name); title.Children.Add(badge); content.Children.Add(title);
        var login = Text(account.Login, 10, "#738BAE"); login.FontFamily = new FontFamily("Cascadia Mono, Consolas"); login.Margin = new Thickness(0, 5, 0, 14); content.Children.Add(login);
        var amountRow = new Grid();
        amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        amountRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var amount = Text(Money(account.Cost), 21, "#EAF1FD", true);
        amount.FontFamily = new FontFamily(SettingsPolicy.ResolveFont("Segoe UI", preferences));
        var amountBox = new Viewbox { Height = 28, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = amount, Margin = new Thickness(0, 0, 8, 0) };
        amountRow.Children.Add(amountBox);
        decimal? quotaPercent = SettingsPolicy.QuotaPercent(account);
        var pct = Text(quotaPercent.HasValue ? quotaPercent.Value.ToString("0.00", Us) + "%" : "—", 13, accent, true); pct.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(pct, 1); amountRow.Children.Add(pct); content.Children.Add(amountRow);
        var rail = new Border { Background = Brush("#2B3950"), Height = 4, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 10, 0, 10), ClipToBounds = true };
        var fill = new Border { Background = Brush(accent), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
        rail.Child = fill;
        rail.SizeChanged += (_, _) => fill.Width = Math.Clamp((double)(quotaPercent ?? 0) / 100.0, 0, 1) * rail.ActualWidth;
        rail.ToolTip = quotaPercent.HasValue ? UiText.T($"已使用 {quotaPercent:0.00}%", $"Used {quotaPercent:0.00}%") : UiText.T("页面未提供具体限额，无法计算配额百分比。", "No numeric quota is available.");
        content.Children.Add(rail);
        var limit = new Grid();
        limit.Children.Add(Text("MONTHLY LIMIT", 8, "#617A9D", true));
        var limitAmount = Text(account.Limit is decimal numericLimit ? Money(numericLimit) : account.LimitLabel, 10, "#8EA6C7");
        if (account.Limit == null) limitAmount.ToolTip = "页面显示的限额方案：" + account.LimitLabel + "。页面未提供具体美元限额。";
        limitAmount.HorizontalAlignment = HorizontalAlignment.Right; limit.Children.Add(limitAmount); content.Children.Add(limit);
        return new Border { Child = content, Background = Brush(index % 2 == 0 ? (preferences.PhotoBackground ? "#D4152338" : "#152338") : (preferences.PhotoBackground ? "#D0132630" : "#132630")), BorderBrush = Brush(index % 2 == 0 ? "#2B3E59" : "#264348"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(SkinTheme.Current.Id == "matrix" ? 9 : 14), Padding = new Thickness(15, 14, 15, 13), Margin = new Thickness(0, 0, 0, 10) };
    }

    private void SetStatus(string value)
    {
        status = value;
        if (!App.Demo && !App.Smoke) Store.Write("connection.json", new { status, checkedAt = DateTimeOffset.Now, hasData = data != null });
        RefreshButton.IsEnabled = value != "syncing";
        bool good = value == "live";
        if (good) failures = 0;
        else if (value is "network" or "layout") failures++;
        int delay = value is "network" or "layout" ? Math.Min(900, Math.Max(60, preferences.RefreshSeconds) * (1 << Math.Min(failures - 1, 4))) : preferences.RefreshSeconds;
        if (value != "syncing") nextRefresh = DateTimeOffset.Now.AddSeconds(delay);
        StatusDot.Fill = Brush(good ? "#66D9B8" : value == "syncing" ? "#79B8F7" : "#C7A370");
        StatusLabel.Text = value switch
        {
            "live" => $"已同步 · {preferences.RefreshSeconds} 秒自动刷新", "syncing" => "正在同步…", "login" => "需要 Microsoft 登录",
            "network" => "连接失败 · 等待重试", "layout" => "页面数据未就绪", "runtime" => "WebView2 无法启动",
            "cached" => "上次保存的数据", "demo" => "示例预览", _ => "正在连接"
        };
        string notice = value switch
        {
            "login" => "点击右下角 ↗ 打开页面，完成 Microsoft 登录后自动同步。",
            "network" => "暂时无法刷新，请检查网络或 VPN。稍后自动重试。",
            "layout" => "未能完整读取用量。请打开原页面，确认 Consumption 标签已加载。",
            "runtime" => "请安装或修复 Microsoft Edge WebView2 Runtime，然后重启。",
            _ => ""
        };
        if (data != null && value is "login" or "network" or "layout" or "cached") notice = (notice + " 当前显示上次成功同步的数据。").Trim();
        AlertText.Text = notice;
        AlertBox.Visibility = notice.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        UiText.Apply(this);
        RefreshUpdateBadge();
        UpdateTimestamp();
    }

    private void UpdateTimestamp()
    {
        RefreshNumericState();
        if (data == null) { UpdateLabel.Text = UiText.Translate("尚未同步"); return; }
        string time = data.FetchedAt.ToLocalTime().ToString(data.FetchedAt.LocalDateTime.Date == DateTime.Now.Date ? "HH:mm:ss" : "MM/dd HH:mm", Us);
        UpdateLabel.Text = App.Demo ? "虚构示例 · 自动刷新未连接" : $"更新于 {time}";
        if (status == "live" && !paused) UpdateLabel.Text += $" · {Math.Max(0, (int)(nextRefresh - DateTimeOffset.Now).TotalSeconds)}s";
        if (paused) UpdateLabel.Text += " · 自动刷新已暂停";
        UpdateLabel.Text = UiText.Translate(UpdateLabel.Text);
    }

    private async void Tick(object? sender, EventArgs e)
    {
        CheckSystemZone();
        UpdateTimestamp();
        if (!closing && DateTimeOffset.Now - miniHistoryUpdated >= TimeSpan.FromMinutes(1)) { miniHistoryUpdated = DateTimeOffset.Now; await RefreshMiniHistoryAsync(); }
        if (paused || status == "syncing" || DateTimeOffset.Now < nextRefresh || closing) return;
        nextRefresh = DateTimeOffset.Now.AddSeconds(preferences.RefreshSeconds);
        if (monitor != null) await monitor.RefreshAsync();
    }

    private async void RefreshNow(object sender, RoutedEventArgs e) { if (monitor != null) await monitor.RefreshAsync(); }
    private void OpenSource(object sender, RoutedEventArgs e) { if (!App.Demo) monitor?.ShowLogin(); }
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (WindowDrag.TryMove(this, e)) SavePreferences();
    }
    private void TogglePin(object sender, RoutedEventArgs e) { Topmost = !Topmost; preferences.Topmost = Topmost; PinButton.Foreground = Brush(Topmost ? "#83D6EC" : "#7186A6"); SavePreferences(); }
    private void HideToTray(object sender, RoutedEventArgs e) { if (tray == null) Close(); else Hide(); }
    private void SavePreferences() { if (!App.Demo && !App.Smoke) Store.Write("preferences.json", preferences); }

    private ContextMenu BuildSettingsMenu()
    {
        var menu = new ContextMenu();
        var settings = new MenuItem { Header = "设置…" }; settings.Click += (_, _) => OpenPreferences(); menu.Items.Add(settings);
        var updates = new MenuItem { Header = UiText.T("检查更新…", "Check for updates…") }; updates.Click += (_, _) => OpenUpdates(); menu.Items.Add(updates);
        var about = new MenuItem { Header = UiText.T("关于…", "About…") }; about.Click += (_, _) => OpenAbout(); menu.Items.Add(about);
        menu.Items.Add(BuildDisplayModeMenu());
        menu.Items.Add(BuildSkinMenu());
        var motion = new MenuItem { Header = "减少动态效果", IsCheckable = true, IsChecked = preferences.ReduceMotion };
        motion.Click += (_,_) => { preferences.ReduceMotion=motion.IsChecked; SkinEdited(); skinWindow?.RefreshSelection(); }; menu.Items.Add(motion);
        var pin = new MenuItem { Header = "始终置顶", IsCheckable = true, IsChecked = Topmost }; pin.Click += TogglePin; menu.Items.Add(pin);
        var refresh = new MenuItem { Header = "自动刷新间隔" };
        foreach (int seconds in SettingsPolicy.RefreshOptions)
        {
            var item = new MenuItem { Header = seconds == 300 ? "5 分钟" : $"{seconds} 秒", IsCheckable = true, IsChecked = preferences.RefreshSeconds == seconds };
            item.Click += (_, _) => { preferences.RefreshSeconds = seconds; nextRefresh = DateTimeOffset.Now.AddSeconds(seconds); if (status == "live") SetStatus("live"); SavePreferences(); };
            refresh.Items.Add(item);
        }
        menu.Items.Add(refresh);
        var pause = new MenuItem { Header = "暂停自动刷新", IsCheckable = true, IsChecked = paused };
        pause.Click += (_, _) => { paused = pause.IsChecked; StatusDot.Fill = Brush(paused ? "#C7A370" : status == "live" ? "#66D9B8" : "#C7A370"); if (paused) StatusLabel.Text = "自动刷新已暂停"; else { SetStatus(status); nextRefresh = DateTimeOffset.Now; } UpdateTimestamp(); };
        menu.Items.Add(pause);
        var historyItem = new MenuItem { Header = "用量历史" }; historyItem.Click += OpenHistory; menu.Items.Add(historyItem);
        menu.Items.Add(new Separator());
        var open = new MenuItem { Header = "Windows 账户登录 / 数据源" }; open.Click += OpenSource; menu.Items.Add(open);
        var browser = new MenuItem { Header = "在系统浏览器打开页面" }; browser.Click += (_, _) => OpenSystemBrowser(); menu.Items.Add(browser);
        var exit = new MenuItem { Header = "退出 Copilot Pulse" }; exit.Click += (_, _) => Close(); menu.Items.Add(exit);
        UiText.Apply(menu);
        return menu;
    }

    private void ShowSettings(object sender, RoutedEventArgs e)
    {
        var menu = BuildSettingsMenu();
        menu.PlacementTarget = (Button)sender;
        menu.IsOpen = true;
    }

    private static void OpenSystemBrowser()
    {
        if (!App.Demo) Process.Start(new ProcessStartInfo(SourceMonitor.Target) { UseShellExecute = true });
    }

    private void CreateTray()
    {
        trayIcon = BrandIcon.CreateTray();
        tray = new Forms.NotifyIcon { Text = "Copilot Pulse · 本月用量", Visible = true, Icon = trayIcon };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); });
        var menu = BuildTrayMenu();
        tray.ContextMenuStrip = menu;
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip { Renderer = new DarkTrayRenderer(), BackColor = DarkTrayRenderer.Surface, ForeColor = DarkTrayRenderer.Text, Font = new System.Drawing.Font("Segoe UI", 9.5f), Padding = new Forms.Padding(4, 5, 4, 5), ShowImageMargin = false };
        menu.Items.Add(UiText.Translate("设置…"), null, (_, _) => Dispatcher.Invoke(OpenPreferences));
        menu.Items.Add(UiText.T("关于…", "About…"), null, (_, _) => Dispatcher.Invoke(OpenAbout));
        menu.Items.Add("显示小组件", null, (_, _) => Dispatcher.Invoke(() => { Show(); Activate(); }));
        menu.Items.Add("迷你数字模式", null, (_,_) => Dispatcher.Invoke(() => SetDisplayMode("mini")));
        menu.Items.Add("展开完整模式", null, (_,_) => Dispatcher.Invoke(() => SetDisplayMode("full")));
        menu.Items.Add("立即刷新", null, (_, _) => Dispatcher.Invoke(() => RefreshNow(this, new RoutedEventArgs())));
        menu.Items.Add("换肤与背景…", null, (_, _) => Dispatcher.Invoke(() => OpenSkins(this, new RoutedEventArgs())));
        menu.Items.Add("用量历史", null, (_, _) => Dispatcher.Invoke(() => OpenHistory(this, new RoutedEventArgs())));
        menu.Items.Add("Windows 账户登录 / 数据源", null, (_, _) => Dispatcher.Invoke(() => monitor?.ShowLogin()));
        menu.Items.Add("在系统浏览器打开页面", null, (_, _) => Dispatcher.Invoke(OpenSystemBrowser));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(Close));
        foreach (Forms.ToolStripItem item in menu.Items) if (item is not Forms.ToolStripSeparator) { item.Padding = new Forms.Padding(12, 7, 12, 7); item.Text = UiText.Translate(item.Text ?? ""); }
        return menu;
    }

    private void Capture(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(ActualWidth * 2), (int)Math.Ceiling(ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var output = File.Create(path); encoder.Save(output);
    }
}
