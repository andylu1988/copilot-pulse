using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CopilotPulse;

public sealed class MeasurementTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public int Cadence { get; set; } = 60;
}

public sealed record SamplingEvent(DateTimeOffset Time, string State, int Cadence);
public sealed record SamplingGap(DateTimeOffset From, DateTimeOffset Until, int EstimatedMissing);
public sealed record TaskMetrics(decimal? Delta, int Samples, IReadOnlyList<SamplingGap> Gaps, int FailedAttempts, int RetryAttempts, DateTimeOffset? LastSuccess);

/// <summary>Atomic task metadata. History remains append-only and independent of task lifetime.</summary>
public sealed class TaskRepository
{
    private readonly string? file;
    private readonly List<MeasurementTask> tasks;
    public IReadOnlyList<MeasurementTask> Tasks => tasks.AsReadOnly();
    public TaskRepository(string? file)
    {
        this.file = file;
        // A damaged file must not silently become an empty list and overwrite prior tasks.
        tasks = file != null && File.Exists(file)
            ? JsonSerializer.Deserialize<List<MeasurementTask>>(File.ReadAllText(file), Store.Json) ?? throw new InvalidDataException("Invalid task file")
            : new();
        if (tasks.Any(t => t == null || t.StartUtc == default || t.Name == null || string.IsNullOrWhiteSpace(t.Id) || t.Cadence < 15 || t.Cadence > 900 || t.EndUtc < t.StartUtc) || tasks.Select(t => t.Id).Distinct().Count() != tasks.Count || tasks.Count(t => t.EndUtc == null) > 1)
            throw new InvalidDataException("Invalid task metadata");
    }
    private void Commit(List<MeasurementTask> updated)
    {
        if (file != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            string temp = file + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, updated, Store.Json);
                stream.Flush(true);
            }
            File.Move(temp, file, true);
        }
        tasks.Clear(); tasks.AddRange(updated);
    }
    public MeasurementTask Start(string name, DateTimeOffset start, int cadence)
    {
        if (tasks.Any(t => t.EndUtc == null)) throw new InvalidOperationException("A task is already recording");
        var task = new MeasurementTask { Name = CleanName(name), StartUtc = start.ToUniversalTime(), Cadence = Math.Clamp(cadence, 15, 900) };
        Commit(tasks.Append(task).ToList()); return task;
    }
    public void End(string id, DateTimeOffset end)
    {
        var task = tasks.Single(t => t.Id == id);
        if (task.EndUtc != null) return;
        if (end < task.StartUtc) throw new InvalidOperationException("End before start; check the system clock");
        Commit(tasks.Select(t => t.Id == id ? new MeasurementTask { Id = t.Id, Name = t.Name, StartUtc = t.StartUtc, EndUtc = end.ToUniversalTime(), Cadence = t.Cadence } : t).ToList());
    }
    public void Rename(string id, string name) => Commit(tasks.Select(t => t.Id == id ? new MeasurementTask { Id = t.Id, Name = CleanName(name), StartUtc = t.StartUtc, EndUtc = t.EndUtc, Cadence = t.Cadence } : t).ToList());
    public MeasurementTask? SyncBaseline(string? baseline, int cadence, DateTimeOffset now)
    {
        bool valid = DateTimeOffset.TryParse(baseline, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var start);
        var current = tasks.SingleOrDefault(t => t.EndUtc == null);
        if (current != null && (!valid || current.StartUtc != start)) End(current.Id, now < current.StartUtc ? current.StartUtc : now);
        if (!valid) return null;
        return tasks.SingleOrDefault(t => t.EndUtc == null) ?? Start("Recorded period", start, cadence);
    }
    public static string CleanName(string name)
    {
        string result = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return result.Length == 0 ? "Untitled task" : result[..Math.Min(100, result.Length)];
    }
}

public sealed class SamplingRepository
{
    private readonly string? directory;
    private readonly List<SamplingEvent> memory = new();
    public SamplingRepository(string? directory) => this.directory = directory;
    public void Append(SamplingEvent sample)
    {
        if (directory == null) { memory.Add(sample); return; }
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, sample.Time.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture) + ".jsonl");
        // Only fixed states, cadence and timestamps. Never store account IDs, page text or URLs.
        using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
        if (stream.Length > 0) { stream.Seek(-1, SeekOrigin.End); if (stream.ReadByte() != '\n') { stream.Seek(0, SeekOrigin.End); stream.WriteByte((byte)'\n'); } }
        stream.Seek(0, SeekOrigin.End);
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sample) + "\n");
        stream.Write(bytes); stream.Flush(true);
    }
    public IReadOnlyList<SamplingEvent> ReadAll()
    {
        if (directory == null) return memory.ToList();
        var result = new List<SamplingEvent>();
        if (!Directory.Exists(directory)) return result;
        foreach (string file in Directory.EnumerateFiles(directory, "????-??.jsonl").OrderBy(p => p))
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                try { var item = JsonSerializer.Deserialize<SamplingEvent>(line); if (item != null && item.Time != default && SamplingHealth.KnownStates.Contains(item.State)) result.Add(item); }
                catch (JsonException) { /* Ignore an interrupted final journal line. */ }
            }
        }
        return result.OrderBy(e => e.Time).ToList();
    }
}

public static class SamplingHealth
{
    public static readonly string[] KnownStates = { "syncing", "live", "network", "layout", "login", "runtime", "paused", "resumed" };
    public static bool Failed(string state) => state is "network" or "layout" or "login" or "runtime";
    public static string State(string source, bool paused, DateTimeOffset? last, DateTimeOffset now, int cadence)
    {
        if (paused) return "paused";
        if (source == "syncing") return "sampling";
        if (source is "layout" or "login" or "runtime") return "page";
        if (!last.HasValue || source == "network" || (now - last.Value).TotalSeconds > Math.Max(120, cadence * 2 + 30)) return "delayed";
        return "fresh";
    }
    public static TaskMetrics Measure(MeasurementTask task, IReadOnlyList<HistoryFrame> history, IReadOnlyList<SamplingEvent> events, DateTimeOffset now)
    {
        var end = task.EndUtc ?? now;
        var frames = history.Where(f => f.Time >= task.StartUtc && f.Time <= end).OrderBy(f => f.Time).ToList();
        var attempts = events.Where(e => e.Time >= task.StartUtc && e.Time <= end).OrderBy(e => e.Time).ToList();
        var gaps = new List<SamplingGap>();
        DateTimeOffset previous = task.StartUtc;
        int cadence = Math.Max(15, task.Cadence);
        foreach (var frame in frames)
        {
            AddGap(previous, frame.Time, cadence, gaps);
            previous = frame.Time; cadence = Math.Max(15, frame.Cadence);
        }
        AddGap(previous, end, cadence, gaps);
        bool retryPending = false;
        int retries = 0;
        foreach (var attempt in attempts)
        {
            if (Failed(attempt.State)) retryPending = true;
            else if (attempt.State == "syncing" && retryPending) { retries++; retryPending = false; }
            else if (attempt.State == "live") retryPending = false;
        }
        return new TaskMetrics(HistoryMath.BaselineNetChange(frames, null), frames.Count, gaps, attempts.Count(e => Failed(e.State)), retries, frames.Count > 0 ? frames[^1].Time : null);
    }
    private static void AddGap(DateTimeOffset from, DateTimeOffset until, int cadence, List<SamplingGap> gaps)
    {
        // Allow half a cadence (at least 30 seconds) for navigation and scheduling jitter.
        double elapsed = (until - from).TotalSeconds;
        if (elapsed > cadence + Math.Max(30, cadence * .5))
            gaps.Add(new SamplingGap(from, until, Math.Max(1, (int)Math.Min(int.MaxValue, Math.Floor(elapsed / cadence) - 1))));
    }
}

public static class TaskExport
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public static string Summary(MeasurementTask task, TaskMetrics metrics, bool anonymous = false)
        => $"{(anonymous ? "Task" : task.Name)} | {task.StartUtc.ToLocalTime():yyyy-MM-dd HH:mm}–{(task.EndUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "…")} | Δ USD {metrics.Delta?.ToString("0.00", Invariant) ?? "—"} | {metrics.Samples} valid samples | ~{metrics.Gaps.Sum(g => (long)g.EstimatedMissing)} missing | {metrics.FailedAttempts} failed refreshes | {metrics.RetryAttempts} retries";
    private static string Cell(string? value)
    {
        string safe = value ?? "";
        // Prevent spreadsheet formula execution, including leading whitespace/control prefixes.
        if (safe.TrimStart().StartsWith('=') || safe.TrimStart().StartsWith('+') || safe.TrimStart().StartsWith('-') || safe.TrimStart().StartsWith('@')) safe = "'" + safe;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }
    public static string Csv(IReadOnlyList<MeasurementTask> tasks, IReadOnlyList<HistoryFrame> frames, IReadOnlyList<SamplingEvent> events, DateTimeOffset now, bool anonymous)
    {
        var rows = new List<string> { "record_type,task_id,task_name,start_utc,end_utc,time_utc,amount_usd,samples,estimated_missing,failed_refreshes,last_success_utc,state,cadence_seconds,retry_attempts" };
        foreach (var task in tasks)
        {
            var m = SamplingHealth.Measure(task, frames, events, now);
            rows.Add(string.Join(",", new[] { "task", task.Id, anonymous ? "Task " + (tasks.ToList().IndexOf(task) + 1) : task.Name, task.StartUtc.ToString("O"), task.EndUtc?.ToString("O"), "", m.Delta?.ToString(Invariant), m.Samples.ToString(Invariant), m.Gaps.Sum(g => (long)g.EstimatedMissing).ToString(Invariant), m.FailedAttempts.ToString(Invariant), m.LastSuccess?.ToString("O"), "", task.Cadence.ToString(Invariant), m.RetryAttempts.ToString(Invariant) }.Select(Cell)));
        }
        foreach (var f in frames) rows.Add(string.Join(",", new[] { "sample", "", "", "", "", f.Time.ToUniversalTime().ToString("O"), f.Total.ToString(Invariant), "", "", "", "", "live", f.Cadence.ToString(Invariant), "" }.Select(Cell)));
        foreach (var e in events) rows.Add(string.Join(",", new[] { "attempt", "", "", "", "", e.Time.ToUniversalTime().ToString("O"), "", "", "", "", "", e.State, e.Cadence.ToString(Invariant), "" }.Select(Cell)));
        return string.Join("\r\n", rows) + "\r\n";
    }
    public static string Json(IReadOnlyList<MeasurementTask> tasks, IReadOnlyList<HistoryFrame> frames, IReadOnlyList<SamplingEvent> events, DateTimeOffset now, bool anonymous)
        => JsonSerializer.Serialize(new {
            schemaVersion = 1, exportedAtUtc = now.ToUniversalTime(), anonymous,
            note = "Observed source-page readings, not exact per-task billing. Missing counts are estimated. Timestamps and amounts remain in anonymous exports.",
            tasks = tasks.Select((t, i) => new { t.Id, Name = anonymous ? "Task " + (i + 1) : t.Name, t.StartUtc, t.EndUtc, Metrics = SamplingHealth.Measure(t, frames, events, now) }),
            samples = frames.Select(f => new { TimeUtc = f.Time.ToUniversalTime(), f.Total, f.Cadence, Accounts = anonymous ? null : f.Accounts }),
            attempts = events
        }, Store.Json);
}
