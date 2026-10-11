using System;

namespace CopilotPulse;

public sealed class AutoUpdateState
{
    public string Repository { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTimeOffset? FirstSeenUtc { get; set; }
    public bool Acknowledged { get; set; }
    public DateTimeOffset? LastAttemptUtc { get; set; }
}

public static class AutoUpdatePolicy
{
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);
    public static readonly TimeSpan GracePeriod = TimeSpan.FromHours(48);
    public static bool CheckDue(AutoUpdateState state, DateTimeOffset now) =>
        !state.LastAttemptUtc.HasValue || now < state.LastAttemptUtc.Value || now - state.LastAttemptUtc.Value >= CheckInterval;
    public static void Observe(AutoUpdateState state, UpdateRelease? release, string repository, DateTimeOffset now)
    {
        repository = UpdatePolicy.Repository(repository);
        string version = release?.Version.ToString() ?? "";
        if (state.Repository != repository || state.Version != version)
        {
            state.Repository = repository; state.Version = version;
            state.FirstSeenUtc = release == null ? null : now.ToUniversalTime();
            state.Acknowledged = false;
        }
        // Repair incomplete/forward-dated state without shortening the user's grace period.
        if (release != null && (!state.FirstSeenUtc.HasValue || state.FirstSeenUtc > now)) state.FirstSeenUtc = now.ToUniversalTime();
    }
    public static bool InstallDue(AutoUpdateState state, UpdateRelease? release, DateTimeOffset now, bool freshCheckSucceeded) =>
        freshCheckSucceeded && release != null && state.Repository == release.Repository && state.Version == release.Version.ToString()
        && !state.Acknowledged && state.FirstSeenUtc.HasValue && now >= state.FirstSeenUtc.Value + GracePeriod;
}
