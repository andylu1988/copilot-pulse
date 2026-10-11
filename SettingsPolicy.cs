using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CopilotPulse;

public static class SettingsPolicy
{
    public static readonly int[] RefreshOptions = { 15, 30, 60, 120, 300, 600 };
    public static readonly int[] FpsOptions = { 10, 15, 20, 30, 60 };
    public static readonly string[] NumberFonts = { "skin", "Segoe UI", "Bahnschrift", "Consolas", "Cascadia Mono", "Georgia", "Impact" };
    public static string ResolveFont(string fallback, Preferences p) => p.NumberFont == "skin" ? fallback : p.NumberFont;
    public static decimal? QuotaPercent(Account a) => a.Limit > 0 ? a.Cost / a.Limit.Value * 100 : null;
    public static void Normalize(Preferences p)
    {
        if (!RefreshOptions.Contains(p.RefreshSeconds)) p.RefreshSeconds = 60;
        if (!FpsOptions.Contains(p.AnimationFps)) p.AnimationFps = 20;
        if (!NumberFonts.Contains(p.NumberFont)) p.NumberFont = "skin";
        if (p.UpdateRepository != "andylu_microsoft/copilot-pulse") p.UpdateRepository = "andylu1988/copilot-pulse";
        p.Language = p.Language == "en" ? "en" : "zh-CN";
        p.WarningMode = p.WarningMode == "amount" ? "amount" : "percent";
        p.WarningPercent = Math.Clamp(p.WarningPercent, 1, 100);
        p.WarningAmount = Math.Clamp(p.WarningAmount, .01m, 1_000_000_000m);
    }
}

public static class SystemClock
{
    public static DateTimeOffset Local(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, TimeZoneInfo.Local);
    public static string ZoneLabel(DateTimeOffset time)
    {
        var zone = TimeZoneInfo.Local;
        var offset = zone.GetUtcOffset(time);
        return $"{zone.StandardName} · UTC{(offset < TimeSpan.Zero ? "−" : "+")}{offset.Duration():hh\\:mm}";
    }
}

public sealed class UsageWarnings
{
    private readonly HashSet<string> notified = new();
    public IReadOnlyList<string> Active(Consumption value, Preferences p)
    {
        if (!p.WarningEnabled || !value.Ok || HistoryRanges.Period(value.FetchedAt) != HistoryRanges.Period(DateTimeOffset.Now)) return Array.Empty<string>();
        if (p.WarningMode == "amount") return value.Total >= p.WarningAmount ? new[] { "total" } : Array.Empty<string>();
        return value.Accounts.Where(a => a.Limit > 0 && a.Cost / a.Limit.Value * 100 >= p.WarningPercent)
            .Select(a => a.Name + "\n" + a.Login).Distinct().ToArray();
    }
    public bool ShouldNotify(Consumption value, Preferences p)
    {
        var active = Active(value, p);
        string prefix = HistoryRanges.Period(value.FetchedAt) + "|" + p.WarningMode + "|" +
            (p.WarningMode == "amount" ? p.WarningAmount : p.WarningPercent).ToString(CultureInfo.InvariantCulture) + "|";
        // Remember suppression across refreshes within this process; a UTC quota month or changed threshold starts a new key.
        bool fresh = false;
        foreach (string key in active) fresh |= notified.Add(prefix + key);
        return fresh;
    }
}
