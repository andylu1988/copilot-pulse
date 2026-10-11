using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CopilotPulse;

internal sealed record UpdateTicket(int ParentId, DateTime ParentStartedUtc, string Application, string Installer, string Digest, string[] RestartArguments);

internal static class UpdateHandoff
{
    // Run a copy of the current client outside its install directory so setup can replace all files.
    internal static async Task LaunchAsync(string installer, UpdateRelease release, string[]? reviewArguments = null)
    {
        await UpdatePolicy.VerifyAsync(installer, release.Digest);
        string application = Environment.ProcessPath ?? throw new IOException("Application path unavailable.");
        string stage = Path.Combine(Path.GetTempPath(), "CopilotPulse-update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        await Task.Run(() => CopyRuntime(AppContext.BaseDirectory, stage));
        using var parent = Process.GetCurrentProcess();
        string ticketPath = Path.Combine(stage, "update-ticket.json");
        File.WriteAllText(ticketPath, JsonSerializer.Serialize(new UpdateTicket(parent.Id, parent.StartTime.ToUniversalTime(), application, Path.GetFullPath(installer), release.Digest, reviewArguments ?? Array.Empty<string>())));
        var start = new ProcessStartInfo(Path.Combine(stage, "CopilotPulse.exe")) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = stage };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(ticketPath);
        using var helper = Process.Start(start) ?? throw new IOException("Update helper unavailable.");
        for (int n = 0; n < 150; n++)
        {
            if (File.Exists(ticketPath + ".ready")) return;
            if (helper.HasExited) throw new IOException("Update helper failed to start.");
            await Task.Delay(100);
        }
        if (!helper.HasExited) helper.Kill();
        throw new IOException("Update helper did not become ready.");
    }
    private static void CopyRuntime(string source, string target)
    {
        foreach (string file in Directory.GetFiles(source))
        {
            string name = Path.GetFileName(file);
            if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase) || name is "preferences.json" or "consumption.json" or "connection.json" or "instance.lock") continue;
            File.Copy(file, Path.Combine(target, name));
        }
        foreach (string directory in Directory.GetDirectories(source))
        {
            string name = Path.GetFileName(directory);
            if (name is "history" or "WebView2" or "migration-backups") continue;
            string destination = Path.Combine(target, name); Directory.CreateDirectory(destination); CopyRuntime(directory, destination);
        }
    }
    internal static async Task<int> ApplyAsync(string ticketPath)
    {
        UpdateTicket? ticket = null; bool parentExited = false;
        try
        {
            ticketPath = Path.GetFullPath(ticketPath);
            if (!string.Equals(Path.GetDirectoryName(ticketPath), Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid helper ticket.");
            ticket = JsonSerializer.Deserialize<UpdateTicket>(File.ReadAllText(ticketPath)) ?? throw new IOException("Missing update ticket.");
            if (Path.GetFileName(ticket.Application) != "CopilotPulse.exe" || !File.Exists(ticket.Application) || !Path.IsPathFullyQualified(ticket.Application)) throw new IOException("Invalid application path.");
            string updateRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "CopilotPulse-update")) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(ticket.Installer).StartsWith(updateRoot, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(ticket.Installer) != ".exe") throw new IOException("Invalid installer path.");
            await UpdatePolicy.VerifyAsync(ticket.Installer, ticket.Digest);
            using (var parent = Process.GetProcessById(ticket.ParentId))
            {
                if (parent.StartTime.ToUniversalTime() != ticket.ParentStartedUtc || !string.Equals(parent.MainModule?.FileName, ticket.Application, StringComparison.OrdinalIgnoreCase)) throw new IOException("Parent process changed.");
                File.WriteAllText(ticketPath + ".ready", "ready");
                using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(2));
                await parent.WaitForExitAsync(timeout.Token);
                parentExited = true;
            }
            // Recheck immediately before execution, after the old client has released its files and locks.
            await UpdatePolicy.VerifyAsync(ticket.Installer, ticket.Digest);
            var start = new ProcessStartInfo(ticket.Installer) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS", "/NORESTARTAPPLICATIONS", "/DIR=" + Path.GetDirectoryName(ticket.Application), "/LOG=" + ticketPath + ".setup.log" }) start.ArgumentList.Add(argument);
            using var setup = Process.Start(start) ?? throw new IOException("Installer unavailable.");
            // Never kill a running installer or launch a client while files are being replaced.
            await setup.WaitForExitAsync();
            File.WriteAllText(ticketPath + ".result", "setup-exit=" + setup.ExitCode);
            Restart(ticket.Application, ticket.RestartArguments);
            return setup.ExitCode;
        }
        catch (Exception exception)
        {
            try { File.WriteAllText(ticketPath + ".result", "update-failed=" + exception.GetType().Name); } catch { }
            if (parentExited && ticket != null) { try { Restart(ticket.Application, ticket.RestartArguments); } catch { } }
            return 1;
        }
    }
    private static void Restart(string path, string[] arguments)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path)! };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        Process.Start(start);
    }
}
