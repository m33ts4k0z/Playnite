using System.Runtime.InteropServices;

namespace Playnite.FullscreenApp.Avalonia.Services;

public readonly record struct BatteryStatus(bool IsPresent, int Percentage, bool IsCharging)
{
    public string Format(bool showPercentage)
    {
        var state = IsCharging ? "Charging" : "Battery";
        return showPercentage ? $"{state} {Percentage}%" : state;
    }
}

public static class BatteryStatusService
{
    public static BatteryStatus Read()
    {
        if (OperatingSystem.IsWindows())
        {
            return ReadWindows();
        }

        if (OperatingSystem.IsLinux())
        {
            return ReadLinux();
        }

        return default;
    }

    private static BatteryStatus ReadWindows()
    {
        if (!GetSystemPowerStatus(out var status) || status.BatteryFlag is 128 or 255)
        {
            return default;
        }

        return new BatteryStatus(
            true,
            Math.Clamp(status.BatteryLifePercent, (byte)0, (byte)100),
            status.ACLineStatus == 1 || (status.BatteryFlag & 8) != 0);
    }

    private static BatteryStatus ReadLinux()
    {
        try
        {
            const string powerSupplyRoot = "/sys/class/power_supply";
            if (!Directory.Exists(powerSupplyRoot))
            {
                return default;
            }

            foreach (var directory in Directory.EnumerateDirectories(powerSupplyRoot, "BAT*"))
            {
                var capacityPath = Path.Combine(directory, "capacity");
                if (!int.TryParse(File.ReadAllText(capacityPath).Trim(), out var percentage))
                {
                    continue;
                }

                var statusPath = Path.Combine(directory, "status");
                var charging = File.Exists(statusPath) &&
                    File.ReadAllText(statusPath).Trim().Equals("Charging", StringComparison.OrdinalIgnoreCase);
                return new BatteryStatus(true, Math.Clamp(percentage, 0, 100), charging);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }

        return default;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
}
