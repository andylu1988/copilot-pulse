using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CopilotPulse;

public sealed record UpdateRelease(Version Version, string Tag, string Repository, string AssetName, string DownloadUrl, string Digest);

public static class UpdatePolicy
{
    public static readonly Version Current = NormalizeVersion(typeof(UpdatePolicy).Assembly.GetName().Version ?? throw new InvalidOperationException("Application version unavailable."));
    public static Version NormalizeVersion(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
    public static bool InstalledVersionMatches(string application, Version expected)
    {
        string library = Path.Combine(Path.GetDirectoryName(application)!, "CopilotPulse.dll");
        if (!File.Exists(library)) return false;
        var info = FileVersionInfo.GetVersionInfo(library);
        return NormalizeVersion(new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart)) == NormalizeVersion(expected);
    }
    public static string Repository(string value) => value == "andylu_microsoft/copilot-pulse" ? value : "andylu1988/copilot-pulse";
    public static string ReleasePage(string repository) => "https://github.com/" + Repository(repository) + "/releases/latest";
    public static UpdateRelease? Parse(string json, string repository, Version current)
    {
        repository = Repository(repository);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        string tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v?\d+\.\d+\.\d+$") || !Version.TryParse(tag.TrimStart('v'), out var version) || version <= current) return null;
        string name = $"CopilotPulse-Setup-{version}-x64.exe";
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name) continue;
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!url.StartsWith($"https://github.com/{repository}/releases/download/{tag}/", StringComparison.Ordinal) || !url.EndsWith("/" + name, StringComparison.Ordinal)) throw new InvalidDataException("Unexpected release download URL.");
            string digest = asset.TryGetProperty("digest", out var hash) ? hash.GetString() ?? "" : "";
            if (!Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$")) throw new InvalidDataException("Release has no SHA256 digest. Use the browser download option.");
            if (asset.GetProperty("size").GetInt64() is <= 0 or > 209715200) throw new InvalidDataException("Unexpected installer size.");
            return new UpdateRelease(version, tag, repository, name, url, digest[7..]);
        }
        throw new InvalidDataException("This release has no Windows x64 installer.");
    }
    public static async Task VerifyAsync(string path, string digest)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length is <= 0 or > 209715200) throw new InvalidDataException("Unexpected installer size.");
        string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        if (!string.Equals(actual, digest, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Installer SHA256 verification failed. Download again.");
    }
}

internal sealed class UpdateService
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(3) };
    internal async Task<UpdateRelease?> CheckAsync(string repository, CancellationToken cancel)
    {
        var latest = await LatestAsync(repository, cancel);
        return latest?.Version > UpdatePolicy.Current ? latest : null;
    }
    internal async Task<UpdateRelease?> LatestAsync(string repository, CancellationToken cancel)
    {
        repository = UpdatePolicy.Repository(repository);
        string json;
        bool useCli = repository == "andylu_microsoft/copilot-pulse";
        if (useCli)
            json = await CliAsync(new[] { "api", $"repos/{repository}/releases/latest" }, cancel);
        else
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases/latest");
            request.Headers.UserAgent.ParseAdd("CopilotPulse/" + UpdatePolicy.Current);
            using var response = await http.SendAsync(request, cancel);
            response.EnsureSuccessStatusCode();
            json = await response.Content.ReadAsStringAsync(cancel);
        }
        return UpdatePolicy.Parse(json, repository, new Version(0, 0, 0));
    }
    internal async Task<string> DownloadAsync(UpdateRelease release, CancellationToken cancel)
    {
        string directory = Path.Combine(Path.GetTempPath(), "CopilotPulse-update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, release.AssetName);
        try
        {
            if (release.Repository == "andylu_microsoft/copilot-pulse") await CliAsync(new[] { "release", "download", release.Tag, "--repo", release.Repository, "--pattern", release.AssetName, "--dir", directory }, cancel);
            else
            {
                using var response = await http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancel);
                response.EnsureSuccessStatusCode();
                using var input = await response.Content.ReadAsStreamAsync(cancel);
                using var output = File.Create(path);
                var buffer = new byte[81920]; long total = 0; int read;
                while ((read = await input.ReadAsync(buffer, cancel)) > 0)
                {
                    total += read;
                    if (total > 209715200) throw new InvalidDataException("Installer too large.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancel);
                }
            }
            await UpdatePolicy.VerifyAsync(path, release.Digest);
            return path;
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }
    private static async Task<string> CliAsync(IEnumerable<string> arguments, CancellationToken cancel)
    {
        var start = new ProcessStartInfo("gh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("GitHub CLI unavailable.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            string result = await stdout; await stderr;
            if (process.ExitCode != 0) throw new IOException("GitHub access unavailable. Use an authorized CLI account or browser download.");
            return result;
        }
        catch { if (!process.HasExited) process.Kill(true); throw; }
    }
}
