namespace CopilotPulse;

public static class DisplayModes
{
    public static string Normalize(string? value, bool legacyCompact) => value is "full" or "compact" or "mini" ? value : legacyCompact ? "compact" : "full";
    public static string Label(string value) => value == "mini" ? "迷你数字" : value == "compact" ? "紧凑" : "完整";
}
