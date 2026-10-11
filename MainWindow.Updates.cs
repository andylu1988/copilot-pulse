using System;
using System.Linq;
using System.Windows;

namespace CopilotPulse;

public partial class MainWindow
{
    internal void OpenUpdates()
    {
        var window = new UpdateWindow(preferences) { Owner = this };
        window.Show();
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
        MiniUsageProgress.Text = mini;
        MiniUsageProgress.ToolTip = text;
    }
}
