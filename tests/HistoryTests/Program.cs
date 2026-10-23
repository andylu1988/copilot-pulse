using CopilotPulse;

try
{
if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: HistoryTests <output-directory>");
    Environment.ExitCode = 2;
    return;
}

string directory = Path.Combine(Path.GetFullPath(args[0]), "run-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
int passed = 0;
void Check(string name, bool value) { if (!value) throw new Exception(name); passed++; Console.WriteLine("PASS " + name); }
var start = new DateTimeOffset(2026, 9, 17, 10, 0, 0, TimeSpan.FromHours(8));
Consumption Sample(DateTimeOffset time, decimal amount, bool ok = true) => new() { Ok = ok, FetchedAt = time, Total = amount, Accounts = new() { new Account { Name = "Enterprise", Login = "test-user", Cost = amount, Limit = 1000 } } };
var repository = new HistoryRepository(directory);
Check("failed refresh is not history", !repository.Record(Sample(start, 10, false), 60));
Check("cached baseline retains original timestamp", repository.Record(Sample(start, 10), 60) && repository.ReadLatest()!.Time == start);
Check("same cached snapshot is not appended twice", !repository.Record(Sample(start, 10), 60));
Check("unchanged successful refresh is still a real sample", repository.Record(Sample(start.AddMinutes(1), 10), 60));
Check("restart remembers the last recorded timestamp", !new HistoryRepository(directory).Record(Sample(start.AddMinutes(1), 10), 60));
repository.Record(Sample(start.AddMinutes(2), 10.25m), 60);
repository.Record(Sample(start.AddMinutes(3), 10.14m), 60);
var samples = repository.ReadRange(start, start.AddHours(1));
var series = HistoryMath.Series(samples, null);
Check("stored decimals round-trip without drift", series[2].Value == 10.25m);
Check("increase is exact", series[2].Delta == 0.25m);
Check("cost corrections remain negative", series[3].Delta == -0.11m);
Check("flat samples have zero change", series[1].Delta == 0);
Check("net change is endpoint change", HistoryMath.NetChange(series) == 0.14m);
Check("time window excludes earlier and later samples", repository.ReadRange(start.AddSeconds(30), start.AddSeconds(90)).Count == 1);
Check("first point does not invent a change", series[0].Delta == null);
var spaced = new[] { HistoryFrame.From(Sample(start, 10), 60), HistoryFrame.From(Sample(start.AddHours(1), 20), 60) };
Check("offline interval breaks the curve and delta", HistoryMath.Series(spaced, null)[1] is { BreakBefore: true, Delta: null });
var slow = new[] { HistoryFrame.From(Sample(start, 10), 300), HistoryFrame.From(Sample(start.AddMinutes(5), 11), 300) };
Check("5-minute refresh is not an offline gap", HistoryMath.Series(slow, null)[1].Delta == 1);
var rollover = new[] { HistoryFrame.From(Sample(new DateTimeOffset(2026, 9, 30, 23, 59, 0, TimeSpan.Zero), 999), 60), HistoryFrame.From(Sample(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), 1), 60) };
Check("new month does not create a huge negative change", HistoryMath.Series(rollover, null)[1].Delta == null);
Check("cross-month endpoint subtraction is suppressed", HistoryMath.NetChange(HistoryMath.Series(rollover, null)) == null);
var missing = samples.Take(3).Select(f => HistoryFrame.From(Sample(f.Time, f.Total), 60)).ToArray();
missing[1].Accounts[0].Login = "different-user";
var accountSeries = HistoryMath.Series(missing, "Enterprise\ntest-user");
Check("missing account does not become zero", accountSeries.Count == 2 && accountSeries[1].Delta == null);
Check("changed enterprise membership breaks total comparison", HistoryMath.Series(missing, null)[1].Delta == null);
File.AppendAllText(Path.Combine(directory, "2026-09.jsonl"), "{unfinished-tail");
Check("append recovers after an incomplete journal tail", repository.Record(Sample(start.AddMinutes(4), 10.15m), 60));
Check("corrupt line does not discard good history", new HistoryRepository(directory).ReadRange(start, start.AddHours(1)).Count == 5);
Check("latest survives corrupt journal tail", new HistoryRepository(directory).ReadLatest()!.Total == 10.15m);
var nextMonth = Sample(new DateTimeOffset(2026, 10, 1, 8, 0, 0, start.Offset), 2);
repository.Record(nextMonth, 60);
Check("monthly files preserve older history", Directory.GetFiles(directory, "*.jsonl").Length == 2 && repository.ReadRange(start, nextMonth.FetchedAt).Count == 6);
string blocked = Path.Combine(directory, "not-a-directory"); File.WriteAllText(blocked, "test");
var unavailable = new HistoryRepository(blocked);
Check("disk error reports failure without crashing live collection", !unavailable.Record(Sample(start, 1), 60) && unavailable.LastError != null);

Check("read-all includes multiple months without date overflow", repository.ReadAll().Count == 6);
string legacyOne = Path.Combine(directory, "legacy-one"), legacyTwo = Path.Combine(directory, "legacy-two"), shared = Path.Combine(directory, "shared");
var oldStore = new HistoryRepository(Path.Combine(legacyOne, "history"));
oldStore.Record(Sample(start, 1), 60); oldStore.Record(Sample(start.AddMinutes(1), 2), 60);
var newStore = new HistoryRepository(Path.Combine(legacyTwo, "history"));
newStore.Record(Sample(start.AddMinutes(1), 2), 60); newStore.Record(Sample(start.AddMinutes(2), 3), 60);
string originalJournal = File.ReadAllText(Path.Combine(legacyOne, "history", "2026-09.jsonl"));
File.WriteAllText(Path.Combine(legacyOne, "preferences.json"), System.Text.Json.JsonSerializer.Serialize(new Preferences { Compact = true, SkinId = "forest", HistoryRange = "7d" }));
var merged = DataStorage.Migrate(shared, new[] { legacyOne, legacyTwo });
Check("two launch profiles merge with exact timestamp deduplication", merged.Imported == 3 && merged.Total == 3);
Check("old journals remain untouched", File.ReadAllText(Path.Combine(legacyOne, "history", "2026-09.jsonl")) == originalJournal);
Check("preferences migrate with history", System.Text.Json.JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(shared, "preferences.json")), Store.Json)!.SkinId == "forest");
string sharedJournal = File.ReadAllText(Path.Combine(shared, "history", "2026-09.jsonl"));
var again = DataStorage.Migrate(shared, new[] { legacyTwo, legacyOne });
Check("restarting does not duplicate or rewrite existing history", again.Imported == 0 && again.Total == 3 && File.ReadAllText(Path.Combine(shared, "history", "2026-09.jsonl")) == sharedJournal);
oldStore.Record(Sample(start.AddMinutes(4), 5), 60);
var third = DataStorage.Migrate(shared, new[] { legacyOne, legacyTwo });
Check("new legacy samples are imported on a later launch", third.Imported == 1 && third.Total == 4);
Check("merged output is chronological", new HistoryRepository(Path.Combine(shared, "history")).ReadAll().Select(x => x.Time).SequenceEqual(new[] { start, start.AddMinutes(1), start.AddMinutes(2), start.AddMinutes(4) }));
Check("rewritten target is backed up before merging", Directory.GetFiles(Path.Combine(shared, "migration-backups"), "2026-09.jsonl", SearchOption.AllDirectories).Length == 1);
Check("mini record delta defaults off", !new Preferences().MiniShowRecordDelta);
var miniRecordPreferences = new Preferences {
    MeasurementBaselineUtc = new DateTimeOffset(2026, 10, 20, 16, 30, 0, TimeSpan.Zero).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
    MiniShowRecordDelta = true
};
SettingsPolicy.Normalize(miniRecordPreferences);
var restoredMiniRecordPreferences = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(miniRecordPreferences), Store.Json)!;
Check("mini record delta preference survives preference restart", restoredMiniRecordPreferences.MiniShowRecordDelta && DateTimeOffset.TryParse(restoredMiniRecordPreferences.MeasurementBaselineUtc, out _));
Check("mini record delta remains opt-in and independent of baseline", !new Preferences().MiniShowRecordDelta && restoredMiniRecordPreferences.MiniShowRecordDelta);
foreach (string miniRange in new[] { "today", "24h", "7d", "month", "2026-09" })
{
    var selection = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(new Preferences { MiniHistoryRange = miniRange, MiniDualMetrics = true }))!;
    SettingsPolicy.Normalize(selection);
    Check("mini range and dual metric persist " + miniRange, selection.MiniHistoryRange == miniRange && selection.MiniDualMetrics);
}
var taskPrimary = new Preferences { MiniShowRecordDelta = true, MiniHistoryRange = "7d" };
SettingsPolicy.Normalize(taskPrimary);
Check("task primary clears conflicting history metric", taskPrimary.MiniHistoryRange == "");
Check("available history months exclude unrelated files", repository.AvailableMonths().SequenceEqual(new[] { "2026-10", "2026-09" }));

Check("default history range retains this month after restart", new Preferences().HistoryRange == "month");
var restoredView = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(new Preferences { HistoryRange = "2026-09", HistoryChanges = true }))!;
Check("selected history range and mode survive serialization", restoredView.HistoryRange == "2026-09" && restoredView.HistoryChanges);
Check("legacy all selection becomes current month", HistoryRanges.Normalize("all") == "month");

var baselineUtcText = new DateTimeOffset(2026, 10, 20, 16, 30, 0, TimeSpan.Zero).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
Check("baseline stores absolute UTC round trip", DateTimeOffset.TryParse(baselineUtcText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var baselineRoundTrip) && baselineRoundTrip == new DateTimeOffset(2026, 10, 20, 16, 30, 0, TimeSpan.Zero));
var baselineRepo = new HistoryRepository(Path.Combine(directory, "baseline-boundaries"));
baselineRepo.Record(Sample(new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero).AddMinutes(-1), 4), 60);
baselineRepo.Record(Sample(new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.FromHours(8)), 5), 60);
baselineRepo.Record(Sample(new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.FromHours(8)), 6), 60);
var baselineUtc = new DateTimeOffset(2026, 10, 20, 0, 30, 0, TimeSpan.Zero);
var baselineSamples = baselineRepo.ReadRange(baselineUtc, new DateTimeOffset(2026, 10, 20, 2, 0, 0, TimeSpan.Zero));
Check("baseline range reads UTC samples without timezone reinterpretation", baselineSamples.Count == 2 && baselineSamples[0].Total == 5 && baselineSamples[1].Total == 6);
Check("baseline delta uses existing series and account rules", HistoryMath.NetChange(HistoryMath.Series(baselineSamples, null)) == 1);
Check("local minute converts to absolute UTC", SystemClock.TryParseLocalMinute("2026-10-20", "16:30", out var localBaselineUtc) && SystemClock.Local(localBaselineUtc).ToString("yyyy-MM-dd HH:mm") == "2026-10-20 16:30");
var crossMonthBaseline = new[] {
    HistoryFrame.From(Sample(new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero), 100), 60),
    HistoryFrame.From(Sample(new DateTimeOffset(2026, 9, 30, 23, 45, 0, TimeSpan.Zero), 110), 60),
    HistoryFrame.From(Sample(new DateTimeOffset(2026, 10, 1, 0, 15, 0, TimeSpan.Zero), 20), 60),
    HistoryFrame.From(Sample(new DateTimeOffset(2026, 10, 1, 0, 30, 0, TimeSpan.Zero), 35), 60)
};
Check("baseline cross-month delta sums each UTC month", HistoryMath.BaselineNetChange(crossMonthBaseline, null) == 25);
var missingMonth = crossMonthBaseline.Select(f => HistoryFrame.From(Sample(f.Time.Month == 10 ? f.Time.AddMonths(1) : f.Time, f.Total), 60)).ToArray();
Check("baseline never silently skips an unrecorded month", HistoryMath.BaselineNetChange(missingMonth, null) == null);
Check("first baseline sample starts at zero", HistoryMath.BaselineNetChange(crossMonthBaseline.Take(1).ToArray(), null) == 0);
Check("empty baseline has no reading", HistoryMath.BaselineNetChange(Array.Empty<HistoryFrame>(), null) == null);
var recordedCurve = HistoryMath.RecordedSeries(crossMonthBaseline, null);
Check("recorded curve starts at zero", recordedCurve[0].Value == 0);
Check("recorded curve ends at observed cross-month delta", recordedCurve[^1].Value == 25);
Check("UTC reset carries previous observed delta", recordedCurve[2].Value == 10 && recordedCurve[2].BreakBefore);
Check("recorded curve preserves negative corrections", HistoryMath.RecordedSeries(samples, null)[3].Value == .14m);
var recordPreferences = new Preferences { MeasurementBaselineUtc = baselineUtcText };
var restoredRecord = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(recordPreferences))!;
Check("record point survives preference restart", restoredRecord.MeasurementBaselineUtc == baselineUtcText);
var journalBeforeDelete = File.ReadAllBytes(Path.Combine(baselineRepo.DirectoryPath, "2026-10.jsonl"));
restoredRecord.MeasurementBaselineUtc = "";
SettingsPolicy.Normalize(restoredRecord);
Check("deleting record point preserves saved samples", journalBeforeDelete.SequenceEqual(File.ReadAllBytes(Path.Combine(baselineRepo.DirectoryPath, "2026-10.jsonl"))) && new HistoryRepository(baselineRepo.DirectoryPath).ReadAll().Count == 3);
Check("baseline samples survive repository restart", new HistoryRepository(Path.Combine(directory, "baseline-boundaries")).ReadRange(baselineUtc, baselineUtc.AddHours(2)).Count == 2);
var miniLegacy = new Preferences { DisplayMode = "mini", Compact = true };
SettingsPolicy.Normalize(miniLegacy);
Check("legacy mini preserves compact return", miniLegacy.MiniReturnMode == "compact");
miniLegacy.MiniReturnMode = "full"; SettingsPolicy.Normalize(miniLegacy);
Check("explicit full return survives normalization", miniLegacy.MiniReturnMode == "full");
Check("invalid local dates rejected", !SystemClock.TryParseLocalMinute("2026-02-30", "12:00", out _));
Check("invalid local clock rejected", !SystemClock.TryParseLocalMinute("2026-10-20", "24:00", out _));
Check("baseline cumulative endpoints survive offline gaps", HistoryMath.BaselineNetChange(new[] {
    HistoryFrame.From(Sample(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), 1), 60),
    HistoryFrame.From(Sample(new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero), 2), 60)
}, null) == 1);

var china = TimeZoneInfo.CreateCustomTimeZone("Test UTC+8", TimeSpan.FromHours(8), "Test UTC+8", "Test UTC+8");
var todayNow = new DateTimeOffset(2026, 10, 8, 14, 30, 0, TimeSpan.FromHours(8));
var midnight = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.FromHours(8));
Check("today is an accepted range with a distinct label", HistoryRanges.Normalize("today") == "today" && HistoryRanges.Label("today") == "今天");
Check("today starts at local midnight rather than 24 hours ago", HistoryRanges.Start("today", todayNow, china) == midnight);
Check("today resolves the local date even when now is supplied in UTC", HistoryRanges.Start("today", todayNow.ToUniversalTime(), china) == midnight);
Check("today advances at the next local midnight", HistoryRanges.Start("today", midnight.AddDays(1), china) == midnight.AddDays(1));
Check("today includes exactly midnight", HistoryRanges.Start("today", midnight, china) == midnight);
Check("rolling ranges retain their previous behavior", HistoryRanges.Start("24h", todayNow) == todayNow.AddHours(-24) && HistoryRanges.Start("7d", todayNow) == todayNow.AddDays(-7));
var todayPreferences = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(new Preferences { HistoryRange = "today" }))!;
Check("today selection survives restart serialization and normalization", HistoryRanges.Normalize(todayPreferences.HistoryRange) == "today");
var eastern = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
var springNow = new DateTimeOffset(2026, 3, 8, 12, 0, 0, TimeSpan.FromHours(-4));
var springMidnight = HistoryRanges.Start("today", springNow, eastern);
Check("DST spring midnight uses its own offset", springMidnight == new DateTimeOffset(2026, 3, 8, 0, 0, 0, TimeSpan.FromHours(-5)) && springMidnight.Offset == TimeSpan.FromHours(-5));
var fallNow = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(-5));
var fallMidnight = HistoryRanges.Start("today", fallNow, eastern);
Check("DST fall midnight uses its own offset", fallMidnight == new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(-4)) && fallMidnight.Offset == TimeSpan.FromHours(-4));
var todayRepository = new HistoryRepository(Path.Combine(directory, "today-boundaries"));
todayRepository.Record(Sample(midnight.AddSeconds(-1), 1), 60);
todayRepository.Record(Sample(midnight.ToUniversalTime(), 2), 60);
todayRepository.Record(Sample(todayNow, 3), 60);
todayRepository.Record(Sample(todayNow.AddSeconds(1), 4), 60);
var todaySamples = todayRepository.ReadRange(HistoryRanges.Start("today", todayNow, china), todayNow);
Check("today includes midnight and now but excludes yesterday and future samples", todaySamples.Count == 2 && todaySamples[0].Total == 2 && todaySamples[1].Total == 3);
Check("today does not invent a midnight baseline", HistoryMath.Series(todaySamples.TakeLast(1).ToList(), null)[0].Delta == null);

var membership = new[] { HistoryFrame.From(Sample(start, 10), 60), HistoryFrame.From(Sample(start.AddMinutes(1), 11), 60), HistoryFrame.From(Sample(start.AddMinutes(2), 12), 60) };
membership[1].Accounts.Add(new HistoryAccount { Name = "Zero enterprise", Login = "test-user", Cost = 0 });
Check("zero-cost addition with shared login retains continuous delta", HistoryMath.Series(membership, null)[1].Delta == 1);
Check("zero-cost removal retains continuous delta", HistoryMath.Series(membership, null)[2].Delta == 1);
Check("zero-cost membership changes preserve full range net", HistoryMath.NetChange(HistoryMath.Series(membership, null)) == 2);
membership[1].Accounts[1].Cost = 5; membership[1].Total += 5;
Check("nonzero enterprise addition suppresses net", HistoryMath.NetChange(HistoryMath.Series(membership.Take(2).ToArray(), null)) == null);
Check("nonzero enterprise removal suppresses net", HistoryMath.NetChange(HistoryMath.Series(membership.Skip(1).ToArray(), null)) == null);
Check("unavailable net explains nonzero membership change", HistoryMath.NetChangeUnavailableReason(HistoryMath.Series(membership, null))!.Contains("非零金额"));
Check("single sample has explicit unavailable reason", HistoryMath.NetChangeUnavailableReason(series.Take(1).ToArray())!.Contains("记录不足"));
Check("cross month has explicit unavailable reason", HistoryMath.NetChangeUnavailableReason(HistoryMath.Series(rollover, null))!.Contains("跨月"));
Check("real account replacement still suppresses net", HistoryMath.NetChange(HistoryMath.Series(missing, null)) == null);
Check("account ordering does not change comparability", HistoryMath.NetChange(HistoryMath.Series(new[] {membership[1], new HistoryFrame { Time = start.AddMinutes(3), Period = membership[1].Period, Total = membership[1].Total, Accounts = membership[1].Accounts.AsEnumerable().Reverse().ToList() }}, null)) == 0);

var beforeReset = new DateTimeOffset(2026, 10, 1, 7, 59, 59, TimeSpan.FromHours(8));
var reset = beforeReset.AddSeconds(1);
Check("UTC month remains September before China 08:00", HistoryRanges.Period(beforeReset) == "2026-09");
Check("UTC month becomes October at China 08:00", HistoryRanges.Period(reset) == "2026-10");
Check("current month starts at UTC zero with any supplied offset", HistoryRanges.Start("month", reset) == reset && HistoryRanges.Start("month", beforeReset) == new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
Check("selected month bounds end before next UTC reset", HistoryRanges.Start("2026-09", todayNow) == new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) && HistoryRanges.End("2026-09", todayNow) == reset.AddTicks(-1));
Check("current selected month ends now", HistoryRanges.End("2026-10", todayNow) == todayNow);
Check("invalid month preference falls back safely", HistoryRanges.Normalize("2026-13") == "month");
var legacyBoundary = new[] { HistoryFrame.From(Sample(beforeReset, 900), 60), HistoryFrame.From(Sample(reset, 0.1m), 60) };
foreach (var f in legacyBoundary) f.Period = "2026-10";
Check("legacy local period fields do not hide UTC reset", HistoryMath.Series(legacyBoundary, null)[1].Delta == null);
var localBoundary = new[] { HistoryFrame.From(Sample(beforeReset.AddHours(-8), 890), 60), HistoryFrame.From(Sample(beforeReset, 900), 60) };
localBoundary[0].Period = "2026-09"; localBoundary[1].Period = "2026-10";
Check("legacy local midnight does not split UTC month", HistoryMath.NetChange(HistoryMath.Series(localBoundary, null)) == 10);
Check("new history records use UTC quota month", HistoryFrame.From(Sample(beforeReset, 1), 60).Period == "2026-09");

if (args.Length > 1 && !args[1].StartsWith("--"))
{
    var real = new HistoryRepository(args[1]);
    var replayNow = new DateTimeOffset(2026, 10, 10, 16, 23, 0, TimeSpan.FromHours(8));
    foreach (string range in new[] { "today", "24h", "7d", "month", "2026-09" })
    {
        var replay = HistoryMath.Series(real.ReadRange(HistoryRanges.Start(range, replayNow), HistoryRanges.End(range, replayNow)), null);
        var value = HistoryMath.NetChange(replay);
        Check("saved history replay " + range, value == replay[^1].Value - replay[0].Value);
        Console.WriteLine($"REPLAY {range}: {replay.Count} samples, net={value}");
    }
}
Console.WriteLine($"{passed} history checks passed.");



var options = new Preferences { RefreshSeconds = 120, AnimationFps = 60, Language = "en", WarningEnabled = true };
SettingsPolicy.Normalize(options);
Check("new settings cadence FPS language retained", options.RefreshSeconds == 120 && options.AnimationFps == 60 && options.Language == "en");
var badOptions = new Preferences { RefreshSeconds = 1, AnimationFps = 0, Language = "invalid", WarningPercent = 101, WarningAmount = -1 };
SettingsPolicy.Normalize(badOptions);
Check("invalid settings normalized", badOptions.RefreshSeconds == 60 && badOptions.AnimationFps == 20 && badOptions.Language == "zh-CN" && badOptions.WarningPercent == 100 && badOptions.WarningAmount == .01m);
var warns = new UsageWarnings();
var warningSample = new Consumption { Ok = true, Total = 80, FetchedAt = DateTimeOffset.Now, Accounts = new() { new Account { Name = "known", Cost = 80, Limit = 100 }, new Account { Name = "unknown", Cost = 10000, Limit = null, Percent = 99 } } };
Check("known quota threshold inclusive unknown quota ignored", warns.Active(warningSample, options).SequenceEqual(new[] { "known\n" }));
Check("first threshold crossing notifies", warns.ShouldNotify(warningSample, options));
Check("repeat refresh does not notify", !warns.ShouldNotify(warningSample, options));
options.WarningPercent = 90;
Check("below threshold does not notify", !warns.ShouldNotify(warningSample, options));
options.WarningMode = "amount"; options.WarningAmount = 80;
Check("absolute amount threshold includes equality", warns.ShouldNotify(warningSample, options));
options.WarningEnabled = false;
Check("disabled warnings silent", warns.Active(warningSample, options).Count == 0 && !warns.ShouldNotify(warningSample, options));
options.WarningEnabled = true; warningSample.FetchedAt = DateTimeOffset.Now.AddMonths(-1);
Check("stale quota month does not warn", warns.Active(warningSample, options).Count == 0);
var persistedOptions = System.Text.Json.JsonSerializer.Deserialize<Preferences>(System.Text.Json.JsonSerializer.Serialize(options))!;
Check("settings serialize and restore", persistedOptions.Language == "en" && persistedOptions.AnimationFps == 60 && persistedOptions.WarningAmount == 80);
Check("amount threshold percent is independent of quota", SettingsPolicy.QuotaPercent(new Account { Cost = 200, Limit = 1000 }) == 20);
Check("unknown quota remains unknown", SettingsPolicy.QuotaPercent(new Account { Cost = 200, Percent = 50 }) == null);
options.NumberFont = "bogus"; SettingsPolicy.Normalize(options); Check("invalid font safely falls back", options.NumberFont == "skin");
foreach (string font in SettingsPolicy.NumberFonts) { options.NumberFont = font; SettingsPolicy.Normalize(options); Check("font persists " + font, options.NumberFont == font); }
var projectXml = System.Xml.Linq.XDocument.Load(Path.Combine(AppContext.BaseDirectory, "../../../../../CopilotPulse.csproj"));
var declaredVersion = Version.Parse(projectXml.Descendants("Version").Single().Value);
Check("runtime version follows project version", UpdatePolicy.Current == UpdatePolicy.NormalizeVersion(declaredVersion));
Check("four part assembly version normalized", UpdatePolicy.NormalizeVersion(new Version(1,11,2,0)).ToString() == "1.11.2");
Check("missing installed library cannot report success", !UpdatePolicy.InstalledVersionMatches(Path.Combine(directory,"CopilotPulse.exe"), declaredVersion));
string ReleaseJson(string version, string url, string digest, bool preview = false) => System.Text.Json.JsonSerializer.Serialize(new { draft = false, prerelease = preview, tag_name = "v" + version, assets = new[] { new { name = "CopilotPulse-Setup-" + version + "-x64.exe", browser_download_url = url, digest, size = 1024 } } });
string releaseUrl = "https://github.com/andylu_microsoft/copilot-pulse/releases/download/v1.15.0/CopilotPulse-Setup-1.15.0-x64.exe";
string validDigest = "sha256:" + new string('a',64);
Check("new stable installer selected", UpdatePolicy.Parse(ReleaseJson("1.15.0", releaseUrl, validDigest), "andylu_microsoft/copilot-pulse", UpdatePolicy.Current)?.Version == new Version(1,15,0));
Check("equal version ignored", UpdatePolicy.Parse(ReleaseJson("1.15.0", releaseUrl, validDigest), "andylu_microsoft/copilot-pulse", new Version(1,15,0)) == null);
Check("older version ignored", UpdatePolicy.Parse(ReleaseJson("1.15.0", releaseUrl, validDigest), "andylu_microsoft/copilot-pulse", new Version(2,0,0)) == null);
Check("prerelease ignored", UpdatePolicy.Parse(ReleaseJson("1.15.0", releaseUrl, validDigest, true), "andylu_microsoft/copilot-pulse", UpdatePolicy.Current) == null);
try { UpdatePolicy.Parse(ReleaseJson("1.15.0", "https://evil.test/setup.exe", validDigest), "andylu_microsoft/copilot-pulse", UpdatePolicy.Current); Check("unexpected host rejected", false); } catch (InvalidDataException) { Check("unexpected host rejected", true); }
try { UpdatePolicy.Parse(ReleaseJson("1.15.0", releaseUrl, ""), "andylu_microsoft/copilot-pulse", UpdatePolicy.Current); Check("missing digest rejected", false); } catch (InvalidDataException) { Check("missing digest rejected", true); }
string hashFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid()+".bin");
File.WriteAllText(hashFile,"verified installer test");
string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(hashFile)));
await UpdatePolicy.VerifyAsync(hashFile,expectedHash); Check("download hash verified",true);
try { await UpdatePolicy.VerifyAsync(hashFile,new string('0',64)); Check("tampered download rejected",false); } catch (InvalidDataException) { Check("tampered download rejected",true); }
File.Delete(hashFile);
var autoNow = new DateTimeOffset(2026,10,11,12,0,0,TimeSpan.FromHours(8));
var candidate = UpdatePolicy.Parse(ReleaseJson("1.15.0", releaseUrl, validDigest), "andylu_microsoft/copilot-pulse", UpdatePolicy.Current)!;
var autoState = new AutoUpdateState();
Check("initial hourly check due", AutoUpdatePolicy.CheckDue(autoState, autoNow));
autoState.LastAttemptUtc = autoNow;
Check("hourly check does not repeat early", !AutoUpdatePolicy.CheckDue(autoState, autoNow.AddMinutes(59)));
Check("hourly boundary checks", AutoUpdatePolicy.CheckDue(autoState, autoNow.AddHours(1)));
Check("backward system clock rechecks safely", AutoUpdatePolicy.CheckDue(autoState, autoNow.AddHours(-1)));
AutoUpdatePolicy.Observe(autoState, candidate, candidate.Repository, autoNow);
Check("first notice stores UTC", autoState.FirstSeenUtc!.Value.Offset == TimeSpan.Zero);
Check("no install before 48 hours", !AutoUpdatePolicy.InstallDue(autoState,candidate,autoNow.AddHours(48).AddTicks(-1),true));
Check("install at 48 hours", AutoUpdatePolicy.InstallDue(autoState,candidate,autoNow.AddHours(48),true));
Check("failed check prevents unattended install", !AutoUpdatePolicy.InstallDue(autoState,candidate,autoNow.AddDays(3),false));
Check("UTC deadline unaffected by offset", AutoUpdatePolicy.InstallDue(autoState,candidate,autoNow.AddHours(48).ToOffset(TimeSpan.FromHours(-7)),true));
var resumed = System.Text.Json.JsonSerializer.Deserialize<AutoUpdateState>(System.Text.Json.JsonSerializer.Serialize(autoState))!;
AutoUpdatePolicy.Observe(resumed,candidate,candidate.Repository,autoNow.AddDays(1));
Check("restart and repeat check retain original grace", resumed.FirstSeenUtc == autoState.FirstSeenUtc);
resumed.Acknowledged = true;
Check("opening notice cancels automatic installation", !AutoUpdatePolicy.InstallDue(resumed,candidate,autoNow.AddDays(3),true));
var acknowledged = System.Text.Json.JsonSerializer.Deserialize<AutoUpdateState>(System.Text.Json.JsonSerializer.Serialize(resumed))!;
Check("acknowledgement survives restart", acknowledged.Acknowledged);
AutoUpdatePolicy.Observe(acknowledged,candidate with {Version = new Version(1,16,0)},candidate.Repository,autoNow.AddDays(3));
Check("different release gets full grace", !acknowledged.Acknowledged && acknowledged.FirstSeenUtc == autoNow.AddDays(3).ToUniversalTime());
Check("mismatched candidate cannot install", !AutoUpdatePolicy.InstallDue(acknowledged,candidate,autoNow.AddDays(6),true));
AutoUpdatePolicy.Observe(acknowledged,null,candidate.Repository,autoNow.AddDays(4));
Check("withdrawn release clears notice", acknowledged.Version == "" && acknowledged.FirstSeenUtc == null);
TaskChecks.Run(directory, Check);
WorkspaceChecks.Run(directory, Check);
InsightsChecks.Run(Check);
ForecastChecks.Run(Check);
if(args.Length >= 4 && args[1] == "--forecast-replay") ForecastReplay.Run(args[2],args[3],Check);
Console.WriteLine($"FINAL {passed} checks passed");
}
catch (Exception error)
{
    Console.Error.WriteLine("TEST FAILURE: " + error);
    Environment.ExitCode = 1;
}

namespace CopilotPulse { public static class App { public static string DataDirectory => Path.GetTempPath(); } }
