using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CopilotPulse;

public sealed class Account
{
    public string Name { get; set; } = "";
    public string Login { get; set; } = "";
    public string Kind { get; set; } = "";
    public decimal Cost { get; set; }
    public decimal? Limit { get; set; }
    public string LimitLabel { get; set; } = "";
    public double Percent { get; set; }
}

public sealed class Consumption
{
    public bool Ok { get; set; }
    public string Reason { get; set; } = "";
    public decimal Total { get; set; }
    public List<Account> Accounts { get; set; } = new();
    public DateTimeOffset FetchedAt { get; set; }
}

public sealed class Preferences
{
    public bool Topmost { get; set; } = true;
    public bool Compact { get; set; }
    public string? DisplayMode { get; set; }
    public bool ReduceMotion { get; set; }
    public double EffectIntensity { get; set; } = 1;
    public double EffectSpeed { get; set; } = 1;
    public bool PhotoBackground { get; set; } = true;
    public string? SkinId { get; set; }
    public string CustomBackgroundPath { get; set; } = "";
    public double PhotoDim { get; set; } = 0.65;
    public double BayiFocusX { get; set; } = 0.755;
    public double BayiFocusY { get; set; } = 0.29;
    public double CustomFocusX { get; set; } = 0.5;
    public double CustomFocusY { get; set; } = 0.35;
    public string HistoryRange { get; set; } = "month";
    public bool HistoryChanges { get; set; }
    public int RefreshSeconds { get; set; } = 60;
    public double Left { get; set; } = -1;
    public double Top { get; set; } = -1;
}

public static class Store
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static T? Read<T>(string name)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(App.DataDirectory, name)), Json); } catch { return default; }
    }
    public static void Write<T>(string name, T value)
    {
        try
        {
            string file = Path.Combine(App.DataDirectory, name);
            File.WriteAllText(file + ".tmp", JsonSerializer.Serialize(value, Json));
            File.Move(file + ".tmp", file, true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
