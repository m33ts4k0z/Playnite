using System.ComponentModel;
using System.Runtime.CompilerServices;
using Playnite.Avalonia.App.Services;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed class DesktopBackupCoordinator : INotifyPropertyChanged
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly DesktopSettings settings;
    private readonly string dataDirectory;
    private readonly string libraryDirectory;
    private readonly Action settingsChanged;
    private readonly Action<BackupOptions, CancellationToken> backupData;
    private readonly SemaphoreSlim executionLock = new(1, 1);
    private bool isRunning;
    private string statusText = "Automatic backup has not run in this session.";

    public event PropertyChangedEventHandler PropertyChanged;
    public bool IsRunning { get => isRunning; private set => SetField(ref isRunning, value); }
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }

    public DesktopBackupCoordinator(
        DesktopSettings settings,
        string dataDirectory,
        string libraryDirectory,
        Action settingsChanged,
        Action<BackupOptions, CancellationToken> backupData = null)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.dataDirectory = Path.GetFullPath(
            dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory)));
        this.libraryDirectory = Path.GetFullPath(
            libraryDirectory ?? throw new ArgumentNullException(nameof(libraryDirectory)));
        this.settingsChanged = settingsChanged ?? (() => { });
        this.backupData = backupData ?? Backup.BackupData;
    }

    public static bool ShouldRun(DesktopSettings settings, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!settings.AutoBackupEnabled || string.IsNullOrWhiteSpace(settings.AutoBackupDir))
        {
            return false;
        }

        return settings.AutoBackupFrequency switch
        {
            AutoBackupFrequency.OnceADay => settings.LastAutoBackup.Date < now.Date,
            AutoBackupFrequency.OnceAWeek => now - settings.LastAutoBackup >= TimeSpan.FromDays(7),
            _ => throw new ArgumentOutOfRangeException(
                nameof(settings.AutoBackupFrequency),
                settings.AutoBackupFrequency,
                "Unsupported automatic-backup frequency.")
        };
    }

    public async Task<bool> RunIfDueAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        if (!ShouldRun(settings, now))
        {
            StatusText = settings.AutoBackupEnabled
                ? "The next automatic backup is not due yet."
                : "Automatic backup is disabled.";
            return false;
        }

        await executionLock.WaitAsync(cancellationToken);
        try
        {
            if (!ShouldRun(settings, now))
            {
                return false;
            }

            IsRunning = true;
            StatusText = "Creating automatic backup before the library opens…";
            var outputDirectory = Path.GetFullPath(settings.AutoBackupDir);
            Directory.CreateDirectory(outputDirectory);
            var options = Backup.GetAutoBackupOptions(settings, dataDirectory, libraryDirectory);
            options.OutputDir = outputDirectory;
            options.RotatingBackups = Math.Clamp(options.RotatingBackups, 0, 10);
            await Task.Run(() => backupData(options, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            settings.LastAutoBackup = now;
            settingsChanged();
            StatusText = $"Automatic backup completed at {now:g}.";
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Avalonia automatic backup failed.");
            StatusText = $"Automatic backup failed: {exception.Message}";
            throw;
        }
        finally
        {
            IsRunning = false;
            executionLock.Release();
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
