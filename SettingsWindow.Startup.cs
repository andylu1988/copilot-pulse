using System;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace CopilotPulse;

internal sealed partial class SettingsWindow
{
    internal void RebuildDesktopPage() { Build(); SelectPageForReview("desktop"); }
    private void BuildStartupOption()
    {
        bool enabled = false; string? failure = null;
        try { enabled = StartupRegistration.Enabled; }
        catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException) { failure = UiText.T("无法读取开机启动状态。", "Cannot read startup registration."); }
        var toggle = new CheckBox { Content = UiText.T("登录 Windows 时启动 Copilot Pulse", "Start Copilot Pulse when signing in to Windows"), IsChecked = enabled, Margin = new Thickness(0, 6, 0, 10), IsEnabled = failure == null && !App.Demo && !App.Smoke };
        toggle.SetResourceReference(Control.ForegroundProperty, "SkinText"); body.Children.Add(toggle);
        var feedback = Label(failure ?? UiText.T("仅当前 Windows 用户；默认关闭。启动后恢复上次显示模式。", "Current Windows user only; off by default. Restores the last display mode."), 11);
        toggle.Click += (_, _) => {
            try { StartupRegistration.Set(toggle.IsChecked == true); feedback.Text = UiText.T("已保存。可在 Windows 设置 → 应用 → 启动中管理。", "Saved. Manage it in Windows Settings → Apps → Startup."); }
            catch (Exception e) when (e is SecurityException or UnauthorizedAccessException or IOException or InvalidOperationException) {
                toggle.IsChecked = enabled; feedback.Text = UiText.T("保存失败，请检查 Windows 启动应用权限。", "Could not save. Check Windows startup app permissions."); return;
            }
            enabled = toggle.IsChecked == true;
        };
    }
}
