using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
namespace CopilotPulse;
internal sealed partial class SettingsWindow
{
    private void Check(string zh, string en, bool value, Action<bool> update)
    {
        var c = new CheckBox { Content = UiText.T(zh, en), IsChecked = value, Margin = new Thickness(0, 5, 0, 8) };
        c.SetResourceReference(Control.ForegroundProperty, "SkinText");
        c.Click += (_, _) => { update(c.IsChecked == true); changed(); }; body.Children.Add(c);
    }
    internal void ShowRemindersForReview()
    {
        SelectPageForReview("notifications");
    }
    private void BuildReminderSettings()
    {
        Page("notifications", "通知", "Notifications", "提醒频率与免打扰", "Reminder frequency and quiet hours");
        Section("通知与免打扰", "Notifications and quiet hours");
        Label(UiText.T("阈值 / 采样中断重复提醒间隔", "Threshold / interruption reminder interval"));
        int[] repeats = { 0, 60, 240, 1440 };
        Choices(new[] { UiText.T("同一事件仅一次", "Once per event"), UiText.T("每小时最多一次", "At most once per hour"), UiText.T("每4小时最多一次", "At most once per 4 hours"), UiText.T("每天最多一次", "At most once per day") }, Array.IndexOf(repeats, preferences.ReminderRepeatMinutes), i => { preferences.ReminderRepeatMinutes = repeats[i]; changed(); });
        Check("完全静默（仍显示主界面警告）", "Silent notifications (widget warnings remain)", preferences.NotificationsSilent, v => preferences.NotificationsSilent = v);
        Check("启用免打扰时段（本机时间）", "Enable quiet hours (local time)", preferences.QuietHoursEnabled, v => preferences.QuietHoursEnabled = v);
        Label(UiText.T("免打扰开始 / 结束（相同时间表示全天）", "Quiet start / end (equal hours means all day)"));
        string[] hours = Enumerable.Range(0, 24).Select(h => $"{h:00}:00").ToArray();
        Choices(hours, preferences.QuietStartHour, i => { preferences.QuietStartHour = i; changed(); });
        Choices(hours, preferences.QuietEndHour, i => { preferences.QuietEndHour = i; changed(); });
        Check("配额重置前提醒（每次重置仅一次）", "Remind before quota reset (once per reset)", preferences.ResetReminder, v => preferences.ResetReminder = v);
        int[] lead = { 1, 6, 12, 24, 48, 72, 168 };
        Choices(lead.Select(h => UiText.T($"提前 {h} 小时", $"{h} hours before reset")).ToArray(), Array.IndexOf(lead, preferences.ResetReminderHours), i => { preferences.ResetReminderHours = lead[i]; changed(); });
        Check("成功采样中断提醒（主动暂停不提醒）", "Sampling interruption reminders (except manual pauses)", preferences.SamplingReminder, v => preferences.SamplingReminder = v);
        int[] minutes = { 2, 5, 10, 15, 30, 60, 120 };
        Choices(minutes.Select(m => UiText.T($"{m} 分钟没有成功采样", $"No successful sample for {m} minutes")).ToArray(), Array.IndexOf(minutes, preferences.SamplingReminderMinutes), i => { preferences.SamplingReminderMinutes = minutes[i]; changed(); });
        Check("警告解除 / 采样恢复时通知", "Notify when warning clears / sampling resumes", preferences.RecoveryReminder, v => preferences.RecoveryReminder = v);
        Page("shortcuts", "快捷键", "Shortcuts", "随时执行常用操作", "Quick access from anywhere");
        Section("全局快捷操作", "Global shortcuts");
        Check("启用全局快捷键", "Enable global shortcuts", preferences.GlobalHotkeys, v => preferences.GlobalHotkeys = v);
        Label(UiText.T("Ctrl+Alt+T 开始/结束任务\nCtrl+Alt+M 切换迷你（返回原紧凑/完整模式）\nCtrl+Alt+R 刷新 · Ctrl+Alt+H 最近任务\n快捷键冲突会提示，也可从连接诊断检查注册状态。", "Ctrl+Alt+T Start/end task\nCtrl+Alt+M Toggle mini (restore compact/full)\nCtrl+Alt+R Refresh · Ctrl+Alt+H Recent tasks\nConflicts are reported and can be checked in Diagnostics."), 11);
    }
}
