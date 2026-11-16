using System;
using System.IO;
using Microsoft.Win32;

namespace CopilotPulse;

internal static class StartupRegistration
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static string Command(string executable) => "\"" + Path.GetFullPath(executable) + "\" --startup";
    internal static bool Enabled
    {
        get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("CopilotPulse") is string value && value == Command(Environment.ProcessPath!); }
    }
    internal static void Set(bool enabled)
    {
        // Test and screenshot processes must never register their temporary binaries.
        if (App.Demo || App.Smoke) throw new InvalidOperationException("Preview cannot change Windows startup.");
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("CopilotPulse", Command(Environment.ProcessPath!), RegistryValueKind.String);
        else key.DeleteValue("CopilotPulse", false);
    }
}
