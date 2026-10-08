using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace CopilotPulse;

public sealed class SourceMonitor : IDisposable
{
    public const string Target = "https://copilot.github.microsoft.com/?section=limits&tab=consumption";
    private readonly Window window;
    private readonly WebView2 web;
    private readonly string script;
    private bool ready, busy, disposed, initializationFailed, windowsAccountHintApplied;
    public bool IsVisible => window.IsVisible;
    public event Action<Consumption>? Received;
    public event Action<string>? Status;

    public SourceMonitor()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CopilotPulse.extract-consumption.js")!;
        using var reader = new StreamReader(stream);
        script = reader.ReadToEnd();
        web = new WebView2();
        window = new Window
        {
            Title = "Copilot Pulse · Microsoft 登录与数据源", Width = 1120, Height = 820,
            Icon = BrandIcon.Window, Content = web, Background = Brushes.White, WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false, ShowActivated = false, Opacity = 0
        };
        window.Closing += KeepWindow;
    }

    private void KeepWindow(object? sender, CancelEventArgs e) { if (!disposed) { e.Cancel = true; window.Hide(); } }
    private static bool IsSource(Uri? uri) => uri?.Scheme == "https" && uri.Host == "copilot.github.microsoft.com";

    public async Task InitializeAsync()
    {
        if (ready || initializationFailed || disposed) return;
        try
        {
            window.Show();
            var options = new CoreWebView2EnvironmentOptions { AllowSingleSignOnUsingOSPrimaryAccount = true };
            var env = await CoreWebView2Environment.CreateAsync(null, App.WebViewDirectory, options);
            await web.EnsureCoreWebView2Async(env);
            if (disposed) return;
            web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            web.CoreWebView2.Profile.IsPasswordAutosaveEnabled = false;
            web.CoreWebView2.Profile.IsGeneralAutofillEnabled = false;
            web.CoreWebView2.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            web.CoreWebView2.DownloadStarting += (_, e) => e.Cancel = true;
            web.CoreWebView2.NavigationStarting += (_, e) =>
            {
                if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) || uri.Scheme != "https") { e.Cancel = true; return; }
                var hinted = WindowsSso.HintForCopilotAuthorization(e.Uri);
                if (hinted != null) { windowsAccountHintApplied = true; e.Cancel = true; web.CoreWebView2.Navigate(hinted); }
            };
            web.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) && uri.Scheme == "https") web.CoreWebView2.Navigate(e.Uri);
            };
            web.CoreWebView2.NavigationCompleted += async (_, e) =>
            {
                if (disposed || busy || !e.IsSuccess || !IsSource(web.Source)) return;
                // After interactive sign-in, return to the requested consumption tab.
                if (web.Source?.Query.Contains("tab=consumption") != true) { web.CoreWebView2.Navigate(Target); return; }
                await ReadCurrentAsync();
            };
            ready = true;
            window.Hide();
            window.Opacity = 1;
            window.ShowActivated = true;
            await RefreshAsync();
        }
        catch (Exception)
        {
            initializationFailed = true;
            window.Hide();
            Status?.Invoke("runtime");
        }
    }

    public async Task RefreshAsync()
    {
        if (!ready || busy || disposed) return;
        if (window.IsVisible && !IsSource(web.Source)) { Status?.Invoke("login"); return; }
        busy = true;
        Status?.Invoke("syncing");
        try
        {
            var completion = new TaskCompletionSource<bool>();
            void OnCompleted(object? s, CoreWebView2NavigationCompletedEventArgs e)
            {
                // Adding the OS account hint replaces one navigation. Wait for its successor.
                if (e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) completion.TrySetResult(e.IsSuccess);
            }
            web.CoreWebView2.NavigationCompleted += OnCompleted;
            try
            {
                web.CoreWebView2.Navigate(Target);
                var winner = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(30)));
                if (disposed) return;
                if (winner != completion.Task || !await completion.Task) { web.CoreWebView2.Stop(); Status?.Invoke("network"); return; }
            }
            finally { web.CoreWebView2.NavigationCompleted -= OnCompleted; }
            await ExtractAsync();
        }
        catch (Exception) { if (!disposed) Status?.Invoke("network"); }
        finally { busy = false; }
    }

    private async Task ReadCurrentAsync()
    {
        if (busy || disposed) return;
        busy = true;
        Status?.Invoke("syncing");
        try { await ExtractAsync(); }
        catch (Exception) { if (!disposed) Status?.Invoke("network"); }
        finally { busy = false; }
    }

    private async Task ExtractAsync()
    {
        if (!IsSource(web.Source)) { Status?.Invoke("login"); return; }
        for (int i = 0; i < 24 && !disposed; i++)
        {
            if (!IsSource(web.Source)) { Status?.Invoke("login"); return; }
            string json = await web.ExecuteScriptAsync(script);
            var data = JsonSerializer.Deserialize<Consumption>(json, Store.Json);
            if (data?.Ok == true)
            {
                data.FetchedAt = DateTimeOffset.Now;
                Received?.Invoke(data);
                Status?.Invoke("live");
                return;
            }
            await Task.Delay(600);
        }
        if (!disposed) Status?.Invoke("layout");
    }

    public async Task<object> GetDiagnosticsAsync()
    {
        if (!ready || disposed) return new { initialized = false };
        string host = web.Source?.Host ?? "";
        bool isSource = IsSource(web.Source);
        // No URLs with query strings, cookies, account names, or token values.
        string prompts = await web.ExecuteScriptAsync("""
            (() => {
              const t = document.body?.innerText || '';
              return {
                emailPrompt: !!document.querySelector('input[type=email]'),
                passwordPrompt: !!document.querySelector('input[type=password]'),
                accountPicker: /pick an account|choose an account|选择帐户|选择账户/i.test(t),
                mfaPrompt: /approve sign in|approve the request|verify your identity|验证你的身份|批准登录/i.test(t),
                errorCode: (t.match(/AADSTS\d+/) || [null])[0],
                requestPrompt: new URL(location.href).searchParams.get('prompt')
              };
            })()
            """);
        string extraction = isSource ? await web.ExecuteScriptAsync(script) : "null";
        string pageShape = isSource ? await web.ExecuteScriptAsync("""
            (() => {
              const lines = (document.body?.innerText || '').split(/\r?\n/).map(s => s.trim()).filter(Boolean);
              // Only fixed UI labels and amounts; exclude account identifiers and authentication data.
              return {
                labels: lines.filter(s => /^(Your month-to-date consumption|Month-to-date consumption by enterprise|Internal USD cost|Monthly limit|[\d,.]+% consumed|\$[\d,.]+(?: internal cost)?$)/i.test(s)).map(s => s.slice(0,160)),
                loginRows: lines.filter(s => /^GitHub login\s/i.test(s)).length,
                standardLimitRows: lines.filter((s,i) => /^Monthly limit/i.test(s) && /^Standard$/i.test(lines[i+1] || '')).length,
                loading: lines.some(s => /^loading[.\u2026]*$/i.test(s)),
                signIn: lines.some(s => /^sign in$/i.test(s))
              };
            })()
            """) : "null";
        return new { initialized = true, windowsPrincipalAvailable = WindowsSso.HasWindowsPrincipal, windowsAccountHintApplied, sourceHost = host, sourcePage = isSource, prompts = JsonSerializer.Deserialize<JsonElement>(prompts), extractionReason = JsonSerializer.Deserialize<Consumption>(extraction, Store.Json)?.Reason, pageShape = JsonSerializer.Deserialize<JsonElement>(pageShape) };
    }
    public void ShowLogin()
    {
        if (initializationFailed)
        {
            MessageBox.Show("无法启动 Microsoft Edge WebView2。请安装或修复 WebView2 Runtime 后重启组件。\nhttps://developer.microsoft.com/microsoft-edge/webview2", "Copilot Pulse");
            return;
        }
        window.Opacity = 1;
        window.ShowInTaskbar = true;
        window.ShowActivated = true;
        window.Show();
        window.Activate();
        if (ready && web.Source == null) web.CoreWebView2.Navigate(Target);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        window.Closing -= KeepWindow;
        web.Dispose();
        window.Close();
    }
}
