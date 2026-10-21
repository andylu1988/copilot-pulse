using System;
using System.Collections.Generic;
using System.Linq;

namespace CopilotPulse;

public sealed record IntervalInsight(decimal? Delta, TimeSpan Duration, decimal? PeakChange, int Samples);
public sealed record UsageRate(decimal PerHour, DateTimeOffset From, DateTimeOffset Until, int Samples);
public static class UsageInsights
{
    public static IntervalInsight Interval(MeasurementTask task, IReadOnlyList<HistoryFrame> frames, DateTimeOffset now)
    {
        var end = task.EndUtc ?? now;
        var subset = frames.Where(f => f.Time >= task.StartUtc && f.Time <= end).ToArray();
        var deltas = HistoryMath.Series(subset, null).Where(p => p.Delta.HasValue).Select(p => p.Delta!.Value).ToArray();
        return new(HistoryMath.BaselineNetChange(subset, null), end > task.StartUtc ? end - task.StartUtc : TimeSpan.Zero,
            deltas.Length > 0 ? deltas.Max() : null, subset.Length);
    }

    // Use the current UTC quota month only. Endpoint deltas survive gaps; missing accounts do not.
    // Negative corrections make a consumption forecast unreliable, rather than a negative burn rate.
    public static UsageRate? Rate(IReadOnlyList<HistoryFrame> history, string key, DateTimeOffset from, DateTimeOffset now)
    {
        var month = HistoryRanges.Start("month", now);
        if (from < month) from = month;
        var frames = history.Where(f => f.Time >= from && f.Time <= now).OrderBy(f => f.Time).ToArray();
        if (frames.Length < 2 || frames.Any(f => !f.Accounts.Any(a => a.Key == key))) return null;
        var points = HistoryMath.Series(frames, key);
        if (points.Count < 2 || (points[^1].Time - points[0].Time).TotalMinutes < 1) return null;
        for (int i = 1; i < points.Count; i++) if (points[i].Value < points[i - 1].Value) return null;
        decimal hours = (decimal)(points[^1].Time - points[0].Time).TotalHours;
        return new((points[^1].Value - points[0].Value) / hours, points[0].Time, points[^1].Time, points.Count);
    }
    public static DateTimeOffset? Depletion(decimal remaining, UsageRate? rate, DateTimeOffset now)
    {
        if (remaining <= 0) return now;
        if (rate == null || rate.PerHour <= 0) return null;
        decimal hours;
        try { hours = remaining / rate.PerHour; } catch (OverflowException) { return null; }
        return hours > 24m * 366 * 100 || hours > (decimal)(DateTimeOffset.MaxValue - now).TotalHours ? null : now.AddHours((double)hours);
    }
}

public sealed class ReminderState
{
    public Dictionary<string, DateTimeOffset> Sent { get; set; } = new();
    public bool ThresholdActive { get; set; }
    public bool SamplingInterrupted { get; set; }
    public string ThresholdKey { get; set; } = "";
}

public static class ReminderPolicy
{
    public static bool Muted(Preferences p, DateTimeOffset now)
    {
        if (p.NotificationsSilent) return true;
        if (!p.QuietHoursEnabled) return false;
        int hour = now.ToLocalTime().Hour;
        return p.QuietStartHour == p.QuietEndHour || (p.QuietStartHour < p.QuietEndHour
            ? hour >= p.QuietStartHour && hour < p.QuietEndHour : hour >= p.QuietStartHour || hour < p.QuietEndHour);
    }
    public static bool Due(ReminderState state, string key, Preferences p, DateTimeOffset now, bool once = false)
    {
        if (Muted(p, now)) return false;
        if (state.Sent.TryGetValue(key, out var last) && (once || p.ReminderRepeatMinutes == 0 || now - last < TimeSpan.FromMinutes(p.ReminderRepeatMinutes))) return false;
        state.Sent[key] = now;
        foreach (var expired in state.Sent.Where(v => now - v.Value > TimeSpan.FromDays(65)).Select(v => v.Key).ToArray()) state.Sent.Remove(expired);
        return true;
    }
}
