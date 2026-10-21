using System;
using System.Collections.Generic;
using System.Linq;

namespace CopilotPulse;

public enum ForecastState { Ready, InvalidReading, Stale, WrongMonth, Insufficient, MissingAccount, Correction }
public sealed record QuotaForecast
{
    public ForecastState State { get; init; }
    public UsageRate? Rate { get; init; }
    public DateTimeOffset AsOf { get; init; }
    public DateTimeOffset Reset { get; init; }
    public bool PercentEstimate { get; init; }
    public bool HasGaps { get; init; }
    public decimal? Remaining { get; init; }
    public decimal? RemainingPercent { get; init; }
    public decimal? DailyAllowance { get; init; }
    public decimal? DailyPercentAllowance { get; init; }
    public decimal? ProjectedCost { get; init; }
    public decimal? ProjectedPercent { get; init; }
    public bool? DepletesBeforeReset { get; init; }
    public DateTimeOffset? Depletion { get; init; }
    public bool Exhausted { get; init; }
}

public static class QuotaForecasting
{
    // Predictions are anchored to the actual source timestamp, never to a repaint.
    // Standard plans expose rounded percentages, not an authoritative monetary cap.
    public static QuotaForecast Build(IReadOnlyList<HistoryFrame> history, Account account,
        DateTimeOffset readingAt, DateTimeOffset from, DateTimeOffset now, int cadence = 60)
    {
        var month = HistoryRanges.Start("month", now);
        var result = new QuotaForecast { AsOf = readingAt, Reset = month.AddMonths(1) };
        if (readingAt > now || readingAt == DateTimeOffset.MinValue || account.Cost < 0 ||
            account.Limit < 0 || !double.IsFinite(account.Percent) || account.Percent < 0)
            return result with { State = ForecastState.InvalidReading };
        if (HistoryRanges.Period(readingAt) != HistoryRanges.Period(now))
            return result with { State = ForecastState.WrongMonth };
        if (now - readingAt > TimeSpan.FromSeconds(Math.Max(120, 2 * Math.Clamp(cadence, 30, 900) + 30)))
            return result with { State = ForecastState.Stale };

        bool numeric = account.Limit.HasValue;
        decimal? percent = numeric ? account.Limit > 0 ? account.Cost / account.Limit.Value * 100 : 100 : (decimal)Math.Min(account.Percent, 1000000);
        // At very small rounded percentages the relative error is too large to extrapolate.
        bool estimated = !numeric && account.Cost > 0 && account.Percent >= .1;
        decimal? remainingPercent = percent.HasValue ? Math.Max(0, 100 - percent.Value) : null;
        decimal? remaining = numeric ? Math.Max(0, account.Limit!.Value - account.Cost) : null;
        bool exhausted = numeric ? remaining == 0 : account.Percent >= 100;
        decimal days = (decimal)(result.Reset - readingAt).TotalDays;
        result = result with { Remaining = remaining, RemainingPercent = remainingPercent,
            PercentEstimate = estimated, Exhausted = exhausted,
            DailyAllowance = remaining / days,
            DailyPercentAllowance = numeric || estimated || exhausted ? remainingPercent / days : null };

        from = from < month ? month : from;
        string key = account.Name + "\n" + account.Login;
        // The current source reading is a real endpoint even when disk persistence is delayed.
        var frames = history.Where(f => f.Time >= from && f.Time < readingAt)
            .GroupBy(f => f.Time).Select(g => g.Last()).OrderBy(f => f.Time).ToList();
        frames.Add(HistoryFrame.From(new Consumption { Ok = true, FetchedAt = readingAt,
            Accounts = new() { account }, Total = account.Cost }, cadence));
        if (frames.Count < 2 || (readingAt - frames[0].Time).TotalMinutes < 1)
            return result with { State = ForecastState.Insufficient };
        if (frames.Any(f => !f.Accounts.Any(a => a.Key == key)))
            return result with { State = ForecastState.MissingAccount };
        var points = HistoryMath.Series(frames, key);
        if (points.Zip(points.Skip(1), (a,b) => b.Value < a.Value).Any(v => v))
            return result with { State = ForecastState.Correction };
        var rate = UsageInsights.Rate(frames, key, from, readingAt)!;
        decimal hours = (decimal)(result.Reset - readingAt).TotalHours;
        decimal projected = account.Cost + rate.PerHour * hours;
        decimal? percentRate = numeric ? account.Limit > 0 ? rate.PerHour / account.Limit.Value * 100 : null
            : estimated ? rate.PerHour / account.Cost * percent : null;
        decimal? projectedPercent = exhausted && !percentRate.HasValue ? percent : percentRate.HasValue ? percent + percentRate * hours : null;
        // Compare only through the next reset: avoid overflowing dates for tiny rates.
        bool? depletes = exhausted ? true : percentRate.HasValue ? percentRate * hours > remainingPercent : null;
        DateTimeOffset? depletedAt = exhausted ? readingAt : depletes == true && percentRate > 0
            ? readingAt.AddHours((double)(remainingPercent!.Value / percentRate.Value)) : null;
        return result with { State = ForecastState.Ready, Rate = rate,
            HasGaps = points.Skip(1).Any(p => p.BreakBefore), ProjectedCost = projected,
            ProjectedPercent = projectedPercent, DepletesBeforeReset = depletes, Depletion = depletedAt };
    }
}
