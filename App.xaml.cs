using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace CopilotPulse;

public partial class App : Application
{
    private Mutex? mutex;
    private FileStream? storageLock;
    public static string DataDirectory { get; private set; } = DataStorage.SharedDirectory;
    public static string WebViewDirectory => Path.Combine(SsoCheck ? DataDirectory : DataStorage.LegacyDirectory, "WebView2");
    public static string? UpdateReviewInstaller { get; private set; }
    public static bool Demo { get; private set; }
    public static string? CapturePath { get; private set; }
    public static bool Smoke { get; private set; }
    public static bool OpenLogin { get; private set; }
    public static string? MenuReviewPath { get; private set; }
    public static bool SsoCheck { get; private set; }
    public static string? HistoryReviewPath { get; private set; }
    public static bool HistoryAtStartup { get; private set; }
    public static bool SkinsAtStartup { get; private set; }
    public static string? SkinReviewPath { get; private set; }
    public static bool MiniAtStartup { get; private set; }
    public static string? StartSkin { get; private set; }
    public static string? SettingsReviewPath { get; private set; }
    public static string? MiniReviewPath { get; private set; }
    public static string? TasksReviewPath { get; private set; }
    public static string? AppearanceReviewPath { get; private set; }
    public static bool PerformanceReview { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        int applyUpdate = Array.IndexOf(e.Args, "--apply-update");
        if (applyUpdate >= 0)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int result = applyUpdate + 1 < e.Args.Length ? await UpdateHandoff.ApplyAsync(e.Args[applyUpdate + 1]) : 1;
            Shutdown(result); return;
        }
        base.OnStartup(e);
        if (Array.IndexOf(e.Args, "--review-software") >= 0)
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        Demo = Array.IndexOf(e.Args, "--demo") >= 0;
        int appearanceReview = Array.IndexOf(e.Args, "--review-appearance");
        if (appearanceReview >= 0 && appearanceReview + 1 < e.Args.Length) { AppearanceReviewPath = Path.GetFullPath(e.Args[appearanceReview + 1]); Demo = true; }
        int performanceReview = Array.IndexOf(e.Args, "--review-performance");
        if (performanceReview >= 0 && performanceReview + 1 < e.Args.Length) { AppearanceReviewPath = Path.GetFullPath(e.Args[performanceReview + 1]); PerformanceReview = true; Demo = true; }
        int tasksReview = Array.IndexOf(e.Args, "--review-tasks");
        if (tasksReview >= 0 && tasksReview + 1 < e.Args.Length) { TasksReviewPath = Path.GetFullPath(e.Args[tasksReview + 1]); Demo = true; }
        int updateReview = Array.IndexOf(e.Args, "--review-update");
        if (updateReview >= 0 && updateReview + 1 < e.Args.Length) { UpdateReviewInstaller = Path.GetFullPath(e.Args[updateReview + 1]); Demo = true; }
        Smoke = Array.IndexOf(e.Args, "--smoke") >= 0;
        OpenLogin = Array.IndexOf(e.Args, "--login") >= 0;
        HistoryAtStartup = Array.IndexOf(e.Args, "--history") >= 0;
        SkinsAtStartup = Array.IndexOf(e.Args, "--skins") >= 0;
        MiniAtStartup = Array.IndexOf(e.Args, "--mini") >= 0;
        int skin = Array.IndexOf(e.Args, "--skin");
        if(skin>=0 && skin+1<e.Args.Length) StartSkin=e.Args[skin+1];
        int settingsReview = Array.IndexOf(e.Args, "--review-settings");
        if(settingsReview >= 0 && settingsReview + 1 < e.Args.Length) { SettingsReviewPath = Path.GetFullPath(e.Args[settingsReview + 1]); Demo = true; }
        int miniReview=Array.IndexOf(e.Args,"--review-mini");
        if(miniReview>=0 && miniReview+1<e.Args.Length) {MiniReviewPath=Path.GetFullPath(e.Args[miniReview+1]);Demo=true;}
        int skinReview = Array.IndexOf(e.Args, "--review-skins");
        if (skinReview >= 0 && skinReview + 1 < e.Args.Length) { SkinReviewPath = Path.GetFullPath(e.Args[skinReview + 1]); Demo = true; }
        int review = Array.IndexOf(e.Args, "--review-menus");
        if (review >= 0 && review + 1 < e.Args.Length) { MenuReviewPath = Path.GetFullPath(e.Args[review + 1]); Demo = true; }
        int historyReview = Array.IndexOf(e.Args, "--review-history");
        if (historyReview >= 0 && historyReview + 1 < e.Args.Length) { HistoryReviewPath = Path.GetFullPath(e.Args[historyReview + 1]); Demo = true; }
        int sso = Array.IndexOf(e.Args, "--sso-check");
        if (sso >= 0 && sso + 1 < e.Args.Length) { SsoCheck = true; Smoke = true; DataDirectory = Path.GetFullPath(e.Args[sso + 1]); }
        int capture = Array.IndexOf(e.Args, "--capture");
        if (capture >= 0 && capture + 1 < e.Args.Length) CapturePath = Path.GetFullPath(e.Args[capture + 1]);
        mutex = new Mutex(true, "Local\\CopilotPulse" + (Demo || Smoke ? ".Test." + Environment.ProcessId : ""), out bool first);
        if (!first) { MessageBox.Show("Copilot Pulse 已在运行。请从系统托盘打开。", "Copilot Pulse"); Shutdown(); return; }
        if (!Demo) Directory.CreateDirectory(DataDirectory);
        if (!Demo && !Smoke)
        {
            // A shared-directory lock also protects against per-launch named-object virtualization.
            try { storageLock = new FileStream(Path.Combine(DataDirectory, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { MessageBox.Show("Copilot Pulse 已在运行。请从系统托盘打开。", "Copilot Pulse"); Shutdown(); return; }
            var migration = DataStorage.Migrate(DataDirectory, DataStorage.LegacySources());
            if (migration.Imported > 0) HistoryAtStartup = true;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            if (Demo)
            {
                string? review = AppearanceReviewPath ?? TasksReviewPath ?? SettingsReviewPath ?? MiniReviewPath ?? SkinReviewPath ?? HistoryReviewPath ?? MenuReviewPath;
                if (review != null) { Directory.CreateDirectory(review); File.WriteAllText(Path.Combine(review,"review-error.txt"),args.Exception.ToString()); }
                args.Handled = true; Shutdown(1); return;
            }
            // Never write authentication URLs or page contents to logs.
            try { File.AppendAllText(Path.Combine(DataDirectory, "errors.log"), $"{DateTimeOffset.Now:O} {args.Exception.GetType().Name}: application error\n"); } catch { }
            MessageBox.Show("组件遇到错误，请重新启动。用量及登录状态保留在本机。", "Copilot Pulse");
            args.Handled = true;
        };
        // Build the atlas before WPF enters OnRender, avoiding nested rendering
        // and first-use font work when a Matrix page becomes visible.
        var startup = System.Diagnostics.Stopwatch.StartNew();
        DisplayArt.PrepareMatrix();
        if (AppearanceReviewPath != null) { Directory.CreateDirectory(AppearanceReviewPath); File.WriteAllText(Path.Combine(AppearanceReviewPath, "atlas-timing.txt"), $"Atlas initialization: {startup.Elapsed.TotalMilliseconds:0.0} ms\n"); }
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e) { storageLock?.Dispose(); mutex?.Dispose(); base.OnExit(e); }
}
