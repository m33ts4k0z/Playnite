using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Playnite.FullscreenApp.Avalonia.Services;

public enum SystemPowerAction
{
    Shutdown,
    Restart,
    Suspend,
    Hibernate,
    Lock,
    Logout
}

public readonly record struct SystemPowerResult(bool Success, string Message);

public sealed class SystemPowerService
{
    public bool IsSupported(SystemPowerAction action)
    {
        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        return action != SystemPowerAction.Logout ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XDG_SESSION_ID"));
    }

    public SystemPowerResult Execute(SystemPowerAction action)
    {
        if (!IsSupported(action))
        {
            return new SystemPowerResult(false, $"{action} is not available on this platform or session.");
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                return ExecuteWindows(action);
            }

            return StartLinuxCommand(action);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new SystemPowerResult(false, $"{action} failed: {exception.Message}");
        }
    }

    internal static ProcessStartInfo CreateLinuxStartInfo(
        SystemPowerAction action,
        string sessionId)
    {
        var (fileName, arguments) = action switch
        {
            SystemPowerAction.Shutdown => ("systemctl", new[] { "--no-block", "poweroff" }),
            SystemPowerAction.Restart => ("systemctl", new[] { "--no-block", "reboot" }),
            SystemPowerAction.Suspend => ("systemctl", new[] { "--no-block", "suspend" }),
            SystemPowerAction.Hibernate => ("systemctl", new[] { "--no-block", "hibernate" }),
            SystemPowerAction.Lock => ("loginctl", new[] { "lock-session", sessionId ?? string.Empty }),
            SystemPowerAction.Logout => ("loginctl", new[] { "terminate-session", sessionId ?? string.Empty }),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
        var startInfo = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static SystemPowerResult StartLinuxCommand(SystemPowerAction action)
    {
        var sessionId = Environment.GetEnvironmentVariable("XDG_SESSION_ID");
        return RunCommand(CreateLinuxStartInfo(action, sessionId), action);
    }

    private static SystemPowerResult ExecuteWindows(SystemPowerAction action)
    {
        return action switch
        {
            SystemPowerAction.Shutdown => StartWindowsShutdown("/s"),
            SystemPowerAction.Restart => StartWindowsShutdown("/r"),
            SystemPowerAction.Suspend => SetSuspendState(false, false, false)
                ? new SystemPowerResult(true, "Suspend was requested.")
                : WindowsFailure("Suspend"),
            SystemPowerAction.Hibernate => SetSuspendState(true, false, false)
                ? new SystemPowerResult(true, "Hibernate was requested.")
                : WindowsFailure("Hibernate"),
            SystemPowerAction.Lock => LockWorkStation()
                ? new SystemPowerResult(true, "The workstation was locked.")
                : WindowsFailure("Lock"),
            SystemPowerAction.Logout => ExitWindowsEx(0, 0)
                ? new SystemPowerResult(true, "Logout was requested.")
                : WindowsFailure("Logout"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    private static SystemPowerResult StartWindowsShutdown(string operation)
    {
        var startInfo = new ProcessStartInfo("shutdown.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(operation);
        startInfo.ArgumentList.Add("/t");
        startInfo.ArgumentList.Add("0");
        return RunCommand(startInfo, operation == "/r" ? SystemPowerAction.Restart : SystemPowerAction.Shutdown);
    }

    private static SystemPowerResult RunCommand(ProcessStartInfo startInfo, SystemPowerAction action)
    {
        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return new SystemPowerResult(false, $"The {action} command could not be started.");
        }

        if (!process.WaitForExit(5_000))
        {
            return new SystemPowerResult(false, $"The {action} command did not acknowledge the request in time.");
        }

        return process.ExitCode == 0
            ? new SystemPowerResult(true, $"{action} was requested.")
            : new SystemPowerResult(false, $"The {action} command exited with code {process.ExitCode}.");
    }

    private static SystemPowerResult WindowsFailure(string action) =>
        new(false, $"{action} failed with Windows error {Marshal.GetLastWin32Error()}.");

    [DllImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.Bool)] bool hibernate,
        [MarshalAs(UnmanagedType.Bool)] bool forceCritical,
        [MarshalAs(UnmanagedType.Bool)] bool disableWakeEvent);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ExitWindowsEx(uint flags, uint reason);
}
