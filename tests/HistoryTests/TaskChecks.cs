using System.Text.Json;
using CopilotPulse;

internal static class TaskChecks
{
    internal static void Run(string directory, Action<string, bool> check)
    {
        string path = Path.Combine(directory, "tasks.json");
        var now = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.FromHours(8));
        var tasks = new TaskRepository(path);
        var first = tasks.Start("Fix login bug", now, 15);
        check("task stores UTC and 15 second cadence", first.StartUtc.Offset == TimeSpan.Zero && first.Cadence == 15);
        try { tasks.Start("second", now, 60); check("concurrent task rejected", false); } catch (InvalidOperationException) { check("concurrent task rejected", true); }
        tasks.Rename(first.Id, "Customer proposal");
        check("active task survives restart with name", new TaskRepository(path).Tasks.Single() is { Name: "Customer proposal", EndUtc: null });
        tasks.End(first.Id, now.AddMinutes(4));
        check("ended task remains after restart", new TaskRepository(path).Tasks.Single().EndUtc == now.AddMinutes(4));
        tasks.Start("Code review", now.AddMinutes(5), 60);
        check("completed and new active task coexist", new TaskRepository(path).Tasks.Count == 2);
        tasks.SyncBaseline("", 60, now.AddMinutes(6));
        check("clearing legacy baseline archives task", new TaskRepository(path).Tasks.All(t => t.EndUtc != null));
        tasks.SyncBaseline(now.AddMinutes(8).ToString("O"), 60, now.AddMinutes(9));
        check("settings custom baseline creates task", tasks.Tasks.Last().StartUtc == now.AddMinutes(8));
        tasks.SyncBaseline(now.AddMinutes(8).ToString("O"), 60, now.AddMinutes(10));
        check("unchanged baseline does not duplicate task", tasks.Tasks.Count == 3);
        check("task names cannot contain controls", TaskRepository.CleanName("  A\r\nB\t ") == "AB");
        check("long names bounded", TaskRepository.CleanName(new string('x', 500)).Length == 100);
        string corrupt = Path.Combine(directory, "corrupt-tasks.json"); File.WriteAllText(corrupt, "[null]");
        try { _ = new TaskRepository(corrupt); check("invalid task metadata rejected", false); } catch (InvalidDataException) { check("invalid task metadata rejected", File.ReadAllText(corrupt) == "[null]"); }
        string locked = Path.Combine(directory, "locked-tasks.json");
        var blocked = new TaskRepository(locked);
        Directory.CreateDirectory(locked);
        try { blocked.Start("Should not persist", now, 60); check("failed write does not mutate task memory", false); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { check("failed write does not mutate task memory", blocked.Tasks.Count == 0); }

        string contendedPath = Path.Combine(directory, "contended-tasks.json");
        var contended = new TaskRepository(contendedPath);
        var saved = contended.Start("Before", now, 60);
        using (var held = new FileStream(contendedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var release = new Thread(() => { Thread.Sleep(70); held.Dispose(); });
            release.Start();
            try { contended.Rename(saved.Id, "After"); }
            finally { release.Join(); }
        }
        check("task save recovers after temporary destination lock", new TaskRepository(contendedPath).Tasks.Single().Name == "After");
        byte[] prior = File.ReadAllBytes(contendedPath);
        using (var held = new FileStream(contendedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            try { contended.Rename(saved.Id, "Must not persist"); check("persistent lock preserves task state", false); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            { check("persistent lock preserves task state", contended.Tasks.Single().Name == "After" && prior.SequenceEqual(File.ReadAllBytes(contendedPath))); }
        }

        HistoryFrame Frame(int seconds, decimal amount) => HistoryFrame.From(new Consumption { Ok = true, FetchedAt = now.AddSeconds(seconds), Total = amount, Accounts = new() { new Account { Name = "Private Enterprise", Login = "secret-corp-account", Cost = amount } } }, 60);
        var frames = new[] { Frame(-60, 90), Frame(0, 100), Frame(60, 101), Frame(240, 107), Frame(300, 109) };
        var task = new MeasurementTask { Name = "Private task name", StartUtc = now, EndUtc = now.AddMinutes(4), Cadence = 60 };
        var events = new[] { new SamplingEvent(now.AddSeconds(-1), "network", 60), new SamplingEvent(now.AddSeconds(90), "network", 60), new SamplingEvent(now.AddSeconds(100), "syncing", 60), new SamplingEvent(now.AddSeconds(110), "syncing", 60), new SamplingEvent(now.AddSeconds(240), "live", 60), new SamplingEvent(now.AddSeconds(241), "network", 60) };
        var metrics = SamplingHealth.Measure(task, frames, events, now.AddMinutes(20));
        check("task endpoints inclusive and outside readings excluded", metrics.Samples == 3 && metrics.Delta == 7);
        check("missing sampling marked without inventing readings", metrics.Gaps.Count == 1 && metrics.Gaps[0].EstimatedMissing == 2 && metrics.Samples == 3);
        check("failures and retries scoped to task", metrics.FailedAttempts == 1 && metrics.RetryAttempts == 1);
        check("last successful sample is source timestamp", metrics.LastSuccess == now.AddMinutes(4));
        check("completed metrics do not change with wall clock", metrics == SamplingHealth.Measure(task, frames, events, now.AddDays(1)) || SamplingHealth.Measure(task, frames, events, now.AddDays(1)).Delta == metrics.Delta);
        var none = SamplingHealth.Measure(task, Array.Empty<HistoryFrame>(), events, now);
        check("no samples means unknown delta with missing markers", none.Delta == null && none.Samples == 0 && none.Gaps.Count == 1);
        var flat = new[] { Frame(0, 100), Frame(60, 100) };
        check("unchanged successful readings count as valid", SamplingHealth.Measure(task, flat, events, now).Samples == 2);
        check("fresh status uses last success age", SamplingHealth.State("live", false, now.AddSeconds(-30), now, 60) == "fresh");
        check("old successful read becomes delayed", SamplingHealth.State("live", false, now.AddMinutes(-5), now, 60) == "delayed");
        check("network failure does not claim fresh", SamplingHealth.State("network", false, now, now, 60) == "delayed");
        check("login requires page action", SamplingHealth.State("login", false, now, now, 60) == "page");
        check("sync reports sampling", SamplingHealth.State("syncing", false, now, now, 60) == "sampling");
        check("paused takes precedence", SamplingHealth.State("syncing", true, now, now, 60) == "paused");
        var early = SamplingHealth.Measure(task, new[] { Frame(180, 104) }, events, now);
        check("leading sampling gaps are visible", early.Gaps[0].From == now && early.Gaps[0].EstimatedMissing == 2);
        var trailing = SamplingHealth.Measure(task, new[] { Frame(0, 100) }, events, now);
        check("trailing sampling gaps are visible", trailing.Gaps[0].Until == task.EndUtc);
        var offsetTask = new MeasurementTask { StartUtc = now.ToOffset(TimeSpan.FromHours(-7)), EndUtc = now.AddMinutes(4).ToOffset(TimeSpan.FromHours(-7)), Cadence = 60 };
        check("task measurement independent of timezone display", SamplingHealth.Measure(offsetTask, frames, events, now).Delta == metrics.Delta);

        string journalDir = Path.Combine(directory, "sampling");
        var journal = new SamplingRepository(journalDir); journal.Append(events[1]); journal.Append(events[2]);
        check("sampling journal survives restart", new SamplingRepository(journalDir).ReadAll().Count == 2);
        string journalFile = Directory.GetFiles(journalDir).Single(); File.AppendAllText(journalFile, "{partial"); journal.Append(events[4]);
        check("interrupted line does not lose next sample event", new SamplingRepository(journalDir).ReadAll().Count == 3);
        var demoTasks = new TaskRepository(null); demoTasks.Start("Demo", now, 60);
        check("demo repository does not touch disk task file", new TaskRepository(path).Tasks.Count == 3);
        string anonymous = TaskExport.Json(new[] { task }, frames, events, now, true);
        check("anonymous JSON excludes account and task names", !anonymous.Contains("secret-corp-account") && !anonymous.Contains("Private task name") && !anonymous.Contains("Private Enterprise"));
        check("anonymous JSON retains numeric samples", JsonDocument.Parse(anonymous).RootElement.GetProperty("samples").GetArrayLength() == 5);
        string named = TaskExport.Json(new[] { task }, frames, events, now, false);
        check("explicit full export includes task and account", named.Contains("secret-corp-account") && named.Contains("Private task name"));
        task.Name = "=HYPERLINK(\"bad\")";
        string csv = TaskExport.Csv(new[] { task }, frames, events, now, false);
        check("CSV neutralizes formulas and quotes", csv.Contains("' =") == false && csv.Contains("'=HYPERLINK(\"\"bad\"\")"));
        check("CSV omits account identities", !csv.Contains("secret-corp-account"));
        string anonCsv = TaskExport.Csv(new[] { task }, frames, events, now, true);
        check("anonymous CSV replaces task name", !anonCsv.Contains("HYPERLINK") && anonCsv.Contains("Task 1"));
        check("summary reports observed increment and samples", TaskExport.Summary(task, metrics).Contains("7.00") && TaskExport.Summary(task, metrics).Contains("3 valid samples"));
    }
}
