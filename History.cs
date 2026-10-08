using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CopilotPulse;

public sealed class HistoryAccount
{
    public string Name { get; set; } = "";
    public string Login { get; set; } = "";
    public decimal Cost { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string Key => Name + "\n" + Login;
}

public sealed class HistoryFrame
{
    public int Version { get; set; } = 1;
    public DateTimeOffset Time { get; set; }
    public string Period { get; set; } = "";
    public decimal Total { get; set; }
    public int Cadence { get; set; } = 60;
    public List<HistoryAccount> Accounts { get; set; } = new();
    public static HistoryFrame From(Consumption data, int cadence) => new()
    {
        Time = data.FetchedAt, Period = data.FetchedAt.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        Total = data.Total, Cadence = cadence,
        Accounts = data.Accounts.Select(a => new HistoryAccount { Name = a.Name, Login = a.Login, Cost = a.Cost }).ToList()
    };
}

/// <summary>Append-only monthly journals: failures never replace or erase older history.</summary>
public sealed class HistoryRepository
{
    private readonly string directory;
    private readonly object gate = new();
    private DateTimeOffset latest;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
    public string? LastError { get; private set; }
    public event Action? Changed;

    public HistoryRepository(string directory)
    {
        this.directory = directory;
        try { latest = ReadLatest()?.Time ?? DateTimeOffset.MinValue; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LastError = "无法读取本机历史文件。"; }
    }

    private static HistoryFrame? Parse(string line)
    {
        try
        {
            var f = JsonSerializer.Deserialize<HistoryFrame>(line, Json);
            return f != null && f.Version == 1 && f.Time > DateTimeOffset.MinValue && f.Total >= 0 && f.Accounts != null && f.Accounts.Count > 0 && f.Accounts.All(a => a != null && a.Name != null && a.Login != null && a.Cost >= 0) ? f : null;
        }
        catch (JsonException) { return null; }
    }

    private IEnumerable<string> Files() => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "????-??.jsonl").OrderBy(f => f, StringComparer.Ordinal)
        : Enumerable.Empty<string>();

    public HistoryFrame? ReadLatest()
    {
        foreach (string file in Files().Reverse())
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(Math.Max(0, stream.Length - 131072), SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            foreach (string line in reader.ReadToEnd().Split('\n').Reverse())
            {
                var frame = Parse(line);
                if (frame != null) return frame;
            }
        }
        return null;
    }

    public bool Record(Consumption data, int cadence)
    {
        if (!data.Ok || data.Accounts == null || data.Accounts.Count == 0 || data.Total < 0 || data.Accounts.Any(a => a.Cost < 0)) return false;
        bool saved = false;
        lock (gate)
        {
            if (data.FetchedAt <= latest) return false;
            var frame = HistoryFrame.From(data, Math.Clamp(cadence, 30, 900));
            byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(frame, Json) + "\n");
            try
            {
                Directory.CreateDirectory(directory);
                string file = Path.Combine(directory, frame.Period + ".jsonl");
                using var stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    if (stream.ReadByte() != '\n') stream.WriteByte((byte)'\n');
                }
                stream.Seek(0, SeekOrigin.End);
                stream.Write(bytes);
                stream.Flush(true);
                latest = data.FetchedAt;
                LastError = null;
                saved = true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { LastError = "历史记录暂时无法保存，请检查磁盘或文件权限。"; }
        }
        if (saved) Changed?.Invoke();
        return saved;
    }

    public string DirectoryPath => directory;
    public IReadOnlyList<HistoryFrame> ReadAll() => ReadRange(DateTimeOffset.MinValue, DateTimeOffset.MaxValue);

    public IReadOnlyList<HistoryFrame> ReadRange(DateTimeOffset from, DateTimeOffset until)
    {
        // Two days of margin also cover local-offset changes around month boundaries.
        string firstMonth = (from > DateTimeOffset.MinValue.AddDays(2) ? from.AddDays(-2) : DateTimeOffset.MinValue).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        string lastMonth = (until < DateTimeOffset.MaxValue.AddDays(-2) ? until.AddDays(2) : DateTimeOffset.MaxValue).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var result = new SortedDictionary<DateTimeOffset, HistoryFrame>();
        foreach (string file in Files())
        {
            string month = Path.GetFileNameWithoutExtension(file);
            if (string.CompareOrdinal(month, firstMonth) < 0 || string.CompareOrdinal(month, lastMonth) > 0) continue;
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            while (reader.ReadLine() is { } line)
            {
                var frame = Parse(line);
                if (frame != null && frame.Time >= from && frame.Time <= until) result[frame.Time] = frame;
            }
        }
        return result.Values.ToList();
    }
}

public sealed record HistoryPoint(DateTimeOffset Time, decimal Value, decimal? Delta, bool BreakBefore, string Reason, string Period, double IntervalSeconds);

public static class HistoryMath
{
    private static string AccountSet(HistoryFrame f) => string.Join("|", f.Accounts.Select(a => a.Key).OrderBy(s => s, StringComparer.Ordinal));

    public static IReadOnlyList<HistoryPoint> Series(IReadOnlyList<HistoryFrame> frames, string? key)
    {
        var result = new List<HistoryPoint>();
        HistoryFrame? previous = null;
        decimal previousValue = 0;
        foreach (var frame in frames.OrderBy(f => f.Time))
        {
            var account = key == null ? null : frame.Accounts.FirstOrDefault(a => a.Key == key);
            if (key != null && account == null) { previous = null; continue; }
            decimal value = key == null ? frame.Total : account!.Cost;
            double elapsed = previous == null ? 0 : (frame.Time - previous.Time).TotalSeconds;
            string reason = previous == null ? "起始记录" : frame.Period != previous.Period ? "新月份 · 累计值重新开始" :
                key == null && AccountSet(frame) != AccountSet(previous) ? "企业账户集合变化" :
                elapsed > Math.Max(180, Math.Max(frame.Cadence, previous.Cadence) * 3 + 30) ? "采样中断 · 不连接缺失区间" : "";
            result.Add(new HistoryPoint(frame.Time, value, reason.Length == 0 ? value - previousValue : null, reason.Length > 0, reason, frame.Period, elapsed));
            previous = frame;
            previousValue = value;
        }
        return result;
    }

    public static decimal? NetChange(IReadOnlyList<HistoryPoint> points)
    {
        if (points.Count < 2 || points.Select(p => p.Period).Distinct().Count() > 1 || points.Any(p => p.Reason == "企业账户集合变化")) return null;
        return points[^1].Value - points[0].Value;
    }
}
