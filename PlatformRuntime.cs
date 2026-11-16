using System;
using System.Runtime.InteropServices;

namespace CopilotPulse;

internal readonly record struct PowerSnapshot(bool OnBattery, bool EnergySaver);

internal static class PlatformRuntime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    private static long cachedTick = long.MinValue;
    private static PowerSnapshot cachedPower;

    internal static Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;
    internal static Architecture OsArchitecture => RuntimeInformation.OSArchitecture;
    internal static bool IsArm64Process => ProcessArchitecture == Architecture.Arm64;
    internal static bool IsArm64Os => OsArchitecture == Architecture.Arm64;
    internal static bool IsEmulatedOnArm64 => IsArm64Os && !IsArm64Process;

    internal static string InstallerArch(Architecture osArchitecture) => osArchitecture == Architecture.Arm64 ? "arm64" : "x64";

    internal static PowerSnapshot CurrentPower()
    {
        long now = Environment.TickCount64;
        if (cachedTick != long.MinValue && now - cachedTick < 5000) return cachedPower;
        if (OperatingSystem.IsWindows() && GetSystemPowerStatus(out var status))
            cachedPower = new PowerSnapshot(status.ACLineStatus == 0, status.SystemStatusFlag == 1);
        else
            cachedPower = new PowerSnapshot(false, false);
        cachedTick = now;
        return cachedPower;
    }

    internal static string RuntimeBadge(bool? english = null) =>
        RuntimeBadge(ProcessArchitecture, OsArchitecture, english ?? UiText.English);

    internal static string RuntimeBadge(Architecture processArchitecture, Architecture osArchitecture, bool english)
    {
        if (processArchitecture == Architecture.Arm64)
            return english ? "Windows ARM64 · Native Optimized" : "Windows ARM64 · 原生性能优化版";
        if (osArchitecture == Architecture.Arm64)
            return english ? "Windows x64 (Emulated on ARM64 · Native available)" : "Windows x64（ARM64 仿真运行 · 可升级原生版）";
        return "Windows x64";
    }

    internal static string OptimizationSummary(bool? english = null)
    {
        bool en = english ?? UiText.English;
        var power = CurrentPower();
        string powerText = power.EnergySaver
            ? (en ? "Energy Saver active (adaptive FPS cap)" : "系统节电模式开启（自适应降帧）")
            : power.OnBattery
                ? (en ? "Battery power (efficiency pacing)" : "电池供电（能效优先调度）")
                : (en ? "AC power" : "外接电源");
        if (IsArm64Process)
            return en
                ? $"{RuntimeBadge(en)} · TieredPGO · Frozen render cache · Adaptive Matrix buffer · WebView2 idle low-memory · {powerText}"
                : $"{RuntimeBadge(en)} · TieredPGO · 零分配冻结渲染缓存 · 矩阵雨自适应缓冲 · WebView2 闲置低内存 · {powerText}";
        if (IsEmulatedOnArm64)
            return en
                ? $"{RuntimeBadge(en)} · Online update automatically switches to the native ARM64 installer · {powerText}"
                : $"{RuntimeBadge(en)} · 在线更新会自动切换为原生 ARM64 安装包 · {powerText}";
        return en
            ? $"Windows x64 · TieredPGO · Frozen render cache · WebView2 idle low-memory · {powerText}"
            : $"Windows x64 · TieredPGO · 冻结渲染缓存 · WebView2 闲置低内存 · {powerText}";
    }

    internal static int EffectiveFps(int requestedFps, bool utilityBackdrop, bool windowActive, bool windowMinimized)
    {
        var power = CurrentPower();
        return EffectiveFps(requestedFps, utilityBackdrop, windowActive, windowMinimized, ProcessArchitecture, power.OnBattery, power.EnergySaver);
    }

    internal static int EffectiveFps(int requestedFps, bool utilityBackdrop, bool windowActive, bool windowMinimized, Architecture processArchitecture, bool onBattery, bool energySaver)
    {
        int fps = Math.Clamp(requestedFps, 10, 60);
        if (windowMinimized || (utilityBackdrop && !windowActive)) return 0;
        if (energySaver) return Math.Min(fps, 15);
        if (processArchitecture == Architecture.Arm64)
        {
            if (utilityBackdrop) return Math.Min(fps, onBattery ? 15 : 20);
            if (onBattery) return Math.Min(fps, 24);
            return fps;
        }
        if (onBattery && utilityBackdrop) return Math.Min(fps, 20);
        return utilityBackdrop ? Math.Min(fps, 30) : fps;
    }

    internal static double MatrixScale(double width, double height, bool small)
    {
        var power = CurrentPower();
        return MatrixScale(width, height, small, ProcessArchitecture, power.OnBattery, power.EnergySaver);
    }

    internal static double MatrixScale(double width, double height, bool small, Architecture processArchitecture, bool onBattery, bool energySaver)
    {
        double area = Math.Max(1, width) * Math.Max(1, height);
        if (small || area <= 180_000) return energySaver ? 0.8 : 1.0;
        if (energySaver) return 0.6;
        if (processArchitecture == Architecture.Arm64) return onBattery ? 0.65 : 0.75;
        return onBattery ? 0.75 : 1.0;
    }
}
