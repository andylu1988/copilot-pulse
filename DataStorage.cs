using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CopilotPulse;

public sealed record MigrationResult(int Imported, int Total, IReadOnlyList<string> Errors);

public static class DataStorage
{
    // Ordinary Documents storage is shared by packaged launches and Explorer launches.
    // AppData file-system redirection caused those launches to see different histories.
    public static string SharedDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Codex", "CopilotPulse-data");
    public static string LegacyDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CopilotPulse");

    public static IReadOnlyList<string> LegacySources()
    {
        var paths = new List<string> { LegacyDirectory };
        string packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Packages");
        if (Directory.Exists(packages))
            foreach (string package in Directory.EnumerateDirectories(packages, "OpenAI.Codex*"))
                paths.Add(Path.Combine(package, "LocalCache", "Local", "CopilotPulse"));
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static MigrationResult Migrate(string destination, IEnumerable<string> sources)
    {
        Directory.CreateDirectory(destination);
        string targetHistory = Path.Combine(destination, "history");
        var target = new HistoryRepository(targetHistory);
        var known = target.ReadAll().ToDictionary(f => f.Time);
        int before = known.Count;
        var changedMonths = new HashSet<string>(StringComparer.Ordinal);
        var errors = new List<string>();
        var sourceList = sources.Select(Path.GetFullPath).Where(s => !string.Equals(s, Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).ToList();
        foreach (string source in sourceList)
        {
            try
            {
                foreach (var frame in new HistoryRepository(Path.Combine(source, "history")).ReadAll())
                {
                    if (known.ContainsKey(frame.Time)) continue;
                    // File names derive only from validated timestamps, never from file contents.
                    string month = frame.Time.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                    known.Add(frame.Time, frame);
                    changedMonths.Add(month);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { errors.Add("部分旧历史暂时无法读取；原文件保留，下次启动会重试。"); }
        }
        if (changedMonths.Count > 0)
        {
            Directory.CreateDirectory(targetHistory);
            string backup = Path.Combine(destination, "migration-backups", DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            Directory.CreateDirectory(backup);
            var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            foreach (string month in changedMonths.OrderBy(m => m))
            {
                string file = Path.Combine(targetHistory, month + ".jsonl");
                if (File.Exists(file)) File.Copy(file, Path.Combine(backup, month + ".jsonl"));
                string staging = file + ".merge-" + Guid.NewGuid().ToString("N");
                using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                        foreach (var frame in known.Values.Where(f => f.Time.ToString("yyyy-MM", CultureInfo.InvariantCulture) == month).OrderBy(f => f.Time)) writer.WriteLine(JsonSerializer.Serialize(frame, json));
                    stream.Flush(true);
                }
                File.Move(staging, file, true);
            }
        }
        // Prefer an existing canonical configuration; migration never overwrites it.
        foreach (string name in new[] { "preferences.json", "consumption.json" })
        {
            string targetFile = Path.Combine(destination, name);
            if (File.Exists(targetFile)) continue;
            foreach (string sourceFile in sourceList.Select(s => Path.Combine(s, name)).Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc))
            {
                try
                {
                    string text = File.ReadAllText(sourceFile);
                    if (name == "preferences.json")
                    {
                        var preferences = JsonSerializer.Deserialize<Preferences>(text, Store.Json);
                        if (preferences == null) continue;
                        if (!string.IsNullOrEmpty(preferences.CustomBackgroundPath))
                        {
                            string managed = Path.Combine(Path.GetDirectoryName(sourceFile)!, "skins", Path.GetFileName(preferences.CustomBackgroundPath));
                            if (File.Exists(managed))
                            {
                                string folder = Path.Combine(destination, "skins"); Directory.CreateDirectory(folder);
                                string copy = Path.Combine(folder, Path.GetFileName(managed));
                                if (!File.Exists(copy)) File.Copy(managed, copy);
                                preferences.CustomBackgroundPath = copy;
                            }
                        }
                        text = JsonSerializer.Serialize(preferences, Store.Json);
                    }
                    else if (JsonSerializer.Deserialize<Consumption>(text, Store.Json)?.Ok != true) continue;
                    string temporary = targetFile + ".migrate";
                    File.WriteAllText(temporary, text, new UTF8Encoding(false)); File.Move(temporary, targetFile, false);
                    break;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { errors.Add("部分旧设置未能迁移；原文件保留。"); }
            }
        }
        var result = new MigrationResult(known.Count - before, known.Count, errors.Distinct().ToList());
        File.WriteAllText(Path.Combine(destination, "migration.json"), JsonSerializer.Serialize(new { checkedAt = DateTimeOffset.Now, result.Imported, result.Total, result.Errors }, Store.Json));
        return result;
    }
}

public static class HistoryRanges
{
    public static string Period(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    public static bool IsMonth(string? range) => DateTime.TryParseExact(range, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _);
    public static string Normalize(string? range) => range is "today" or "24h" or "7d" or "month" || IsMonth(range) ? range! : "month";
    public static DateTimeOffset Start(string? range, DateTimeOffset now, TimeZoneInfo? localTimeZone = null)
    {
        string value = Normalize(range);
        if (value == "today") return LocalMidnight(now, localTimeZone ?? TimeZoneInfo.Local);
        if (value == "24h") return now.AddHours(-24);
        if (value == "7d") return now.AddDays(-7);
        string month = IsMonth(value) ? value : Period(now);
        var date = DateTime.ParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        return new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, TimeSpan.Zero);
    }
    public static DateTimeOffset End(string? range, DateTimeOffset now)
    {
        if (!IsMonth(range)) return now;
        var next = Start(range, now).AddMonths(1);
        return next <= now ? next.AddTicks(-1) : now;
    }
    private static DateTimeOffset LocalMidnight(DateTimeOffset now, TimeZoneInfo zone)
    {
        // Resolve the offset at midnight, which can differ from now on a DST transition day.
        DateTime midnight = TimeZoneInfo.ConvertTime(now, zone).Date;
        return new DateTimeOffset(midnight, zone.GetUtcOffset(midnight));
    }
    public static string Label(string? range) => Normalize(range) switch { "today" => "今天", "24h" => "24 小时", "7d" => "7 天", _ => IsMonth(range) ? range! : "本月" };
}
