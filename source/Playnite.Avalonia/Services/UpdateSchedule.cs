namespace Playnite.Avalonia.App.Services;

public static class UpdateSchedule
{
    public static bool ShouldRunOnStartup(
        UpdateCheckFrequency frequency,
        DateTime lastCheck,
        DateTime now) => frequency switch
        {
            UpdateCheckFrequency.Manually => false,
            UpdateCheckFrequency.OnceADay => now.Date > lastCheck.Date,
            UpdateCheckFrequency.OnceAWeek => now - lastCheck > TimeSpan.FromDays(6),
            UpdateCheckFrequency.OnEveryStartup => true,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null)
        };

    public static bool ShouldRunPeriodically(
        UpdateCheckFrequency frequency,
        DateTime lastCheck,
        DateTime now) => frequency switch
        {
            UpdateCheckFrequency.Manually or UpdateCheckFrequency.OnEveryStartup => false,
            UpdateCheckFrequency.OnceADay => now.Date > lastCheck.Date,
            UpdateCheckFrequency.OnceAWeek => now - lastCheck > TimeSpan.FromDays(6),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null)
        };

    public static bool ShouldRunOnStartup(
        LibraryUpdateCheckFrequency frequency,
        DateTime lastCheck,
        DateTime now) => frequency switch
        {
            LibraryUpdateCheckFrequency.Manually => false,
            LibraryUpdateCheckFrequency.OnceADay => now.Date > lastCheck.Date,
            LibraryUpdateCheckFrequency.OnceAWeek => now - lastCheck > TimeSpan.FromDays(6),
            LibraryUpdateCheckFrequency.OnEveryStartup => true,
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, null)
        };
}
