using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
namespace CopilotPulse;
internal sealed class DiagnosticsWindow : Window
{
    private readonly Func<Task<DiagnosticReport>> diagnose;
    private readonly StackPanel diagnostics = new();
    private readonly UtilitySkin skin;
    private DiagnosticReport? report;
    private bool busy, closed;
    internal DiagnosticsWindow(Func<Task<DiagnosticReport>> diagnose)
    {
        this.diagnose = diagnose;
        Title = UiText.T("Copilot Pulse · 连接诊断向导", "Copilot Pulse · Connection diagnostics");
        Icon = BrandIcon.Window; Width = 620; Height = Math.Min(740, SystemParameters.WorkArea.Height - 24);
        MinWidth = 460; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(22) };
        var actions = new WrapPanel(); DockPanel.SetDock(actions, Dock.Top); root.Children.Add(actions);
        actions.Children.Add(InsightUi.Button(UiText.T("重新检查", "Check again"), () => _ = ReloadAsync()));
        actions.Children.Add(InsightUi.Button(UiText.T("登录数据源", "Sign in"), () => (Owner as MainWindow)?.OpenDiagnosticLogin()));
        actions.Children.Add(InsightUi.Button(UiText.T("立即采样", "Refresh source"), () => (Owner as MainWindow)?.RefreshDiagnosticSource()));
        actions.Children.Add(InsightUi.Button(UiText.T("关闭", "Close"), Close));
        root.Children.Add(new ScrollViewer { Content = diagnostics, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        skin = new UtilitySkin(this, root);
        Loaded += async (_, _) => await ReloadAsync(); Closed += (_, _) => closed = true;
    }
    internal void ApplySkin() => skin.Apply();
    internal async Task ReloadAsync()
    {
        if (busy || closed) return; busy = true;
        try { report = await diagnose(); if (!closed) RenderDiagnostics(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        { if (!closed) { diagnostics.Children.Clear(); diagnostics.Children.Add(InsightUi.Text(UiText.T("暂时无法完成诊断，请重新检查。", "Diagnostics unavailable. Try again."))); } }
        finally { busy = false; }
    }
    internal void ExportReview(string directory)
    { if (report != null) File.WriteAllText(Path.Combine(directory,"diagnostics.json"), JsonSerializer.Serialize(report, Store.Json)); }
    private void RenderDiagnostics()
    {
        diagnostics.Children.Clear(); if (report == null) return;
        diagnostics.Children.Add(InsightUi.Text(UiText.T("连接诊断向导", "Connection diagnostics"), 21));
        foreach (var c in report.Checks) { var label = InsightUi.Text(c.State + " · " + c.Name + "\n" + c.Detail); label.Foreground = c.State == "OK" ? Brushes.LightGreen : c.State == "ERROR" ? Brushes.Salmon : Brushes.Khaki; diagnostics.Children.Add(InsightUi.Card(label)); }
        diagnostics.Children.Add(InsightUi.Button(UiText.T("导出匿名诊断报告…", "Export anonymous diagnostics…"), () => {
            var save = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "CopilotPulse-diagnostics.json" }; if (save.ShowDialog(this) != true) return;
            try { File.WriteAllText(save.FileName, JsonSerializer.Serialize(report, Store.Json)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { MessageBox.Show(this, UiText.T("保存失败，请更换位置。", "Could not save. Choose another location.")); }
        }));
        diagnostics.Children.Add(InsightUi.Text(UiText.T("报告仅包含版本、时区和固定诊断状态，不含账户名、用量金额、令牌、Cookie 或完整网页地址。", "Reports contain version, time zone and fixed diagnostic states only; no account names, amounts, tokens, cookies or full URLs."), 11));
    }
}
