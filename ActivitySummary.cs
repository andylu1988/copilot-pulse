using System;
using System.Collections.Generic;
using System.Linq;

namespace CopilotPulse;

public sealed record DailyUsage(DateTime Date, decimal? Delta, int Samples, double CoveredSeconds);
public sealed record MonthlyArchive(string Month, decimal? LastObserved, DateTimeOffset? LastSampleUtc, double CoveragePercent, DateTime? MostActiveDate, int SavedTasks, IReadOnlyList<DailyUsage> Days);
public static class ActivitySummary
{
    public static IReadOnlyList<DailyUsage> Days(IReadOnlyList<HistoryFrame> frames, DateTime first, DateTime last, TimeZoneInfo zone)
    {
        var ordered = frames.OrderBy(f => f.Time).ToArray();
        var groups = ordered.GroupBy(f => TimeZoneInfo.ConvertTime(f.Time, zone).Date).ToDictionary(g => g.Key, g => g.ToArray());
        var result = new List<DailyUsage>();
        for (var date = first.Date; date <= last.Date; date = date.AddDays(1))
        {
            var day = groups.GetValueOrDefault(date) ?? Array.Empty<HistoryFrame>();
            decimal sum = 0; int pairs = 0; double coverage = 0;
            for (int i = 1; i < day.Length; i++)
            {
                var a = day[i - 1]; var b = day[i];
                double seconds = (b.Time - a.Time).TotalSeconds;
                if (a.Period != b.Period || seconds <= 0 || seconds > Math.Max(90, a.Cadence * 2.0)) continue;
                var delta = HistoryMath.BaselineNetChange(new[] { a, b }, null);
                if (!delta.HasValue || delta < 0) continue;
                sum += delta.Value; coverage += seconds; pairs++;
            }
            result.Add(new(date, pairs > 0 ? sum : null, day.Length, coverage));
        }
        return result;
    }
    public static MonthlyArchive Month(string month, IReadOnlyList<HistoryFrame> frames, IReadOnlyList<MeasurementTask> tasks, TimeZoneInfo zone)
    {
        var start = DateTimeOffset.ParseExact(month + "-01 +00:00", "yyyy-MM-dd zzz", System.Globalization.CultureInfo.InvariantCulture);
        var end = start.AddMonths(1);
        var selected = frames.Where(f => f.Time >= start && f.Time < end).OrderBy(f => f.Time).ToArray();
        var days = Days(selected, TimeZoneInfo.ConvertTime(start, zone).Date, TimeZoneInfo.ConvertTime(end.AddTicks(-1), zone).Date, zone);
        var active = days.Where(d => d.Delta.HasValue).OrderByDescending(d => d.Delta).FirstOrDefault();
        var last = selected.LastOrDefault();
        return new(month, last?.Total, last?.Time, Math.Clamp(days.Sum(d => d.CoveredSeconds) / (end - start).TotalSeconds * 100, 0, 100), active?.Date,
            tasks.Count(t => t.EndUtc.HasValue && t.StartUtc < end && t.EndUtc >= start), days);
    }
}
