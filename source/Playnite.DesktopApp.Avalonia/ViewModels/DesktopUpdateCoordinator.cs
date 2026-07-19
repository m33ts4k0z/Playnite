using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Threading;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopUpdateCoordinator : INotifyPropertyChanged, IDisposable
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly DesktopSettings settings;
    private readonly DesktopLibrarySyncViewModel librarySync;
    private readonly IDesktopAddonUpdateService addonUpdates;
    private readonly IDesktopProgramUpdateService programUpdates;
    private readonly Action settingsChanged;
    private readonly Action<NotificationMessage> addNotification;
    private readonly Action<string, bool> showMessage;
    private readonly Func<string, string, bool> confirm;
    private readonly Action openSettings;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private System.Threading.Timer timer;
    private Action<string> launchProgramUpdater;
    private bool isChecking;
    private bool isInstalling;
    private bool restartRequired;
    private string statusText = string.Empty;
    private int programDownloadProgress;
    private DesktopProgramUpdate availableProgramUpdate;
    private bool started;
    private bool disposed;

    public event PropertyChangedEventHandler PropertyChanged;
    public ObservableCollection<DesktopAddonUpdate> AvailableAddonUpdates { get; } = new();
    public bool ProgramUpdatesSupported => programUpdates.IsSupported;
    public string ProgramUpdateSupportText => ProgramUpdatesSupported
        ? "Windows self-updates are downloaded, checksum-verified, and installed by Playnite's updater."
        : "Program updates are managed by your operating-system package manager on this platform.";
    public bool IsChecking { get => isChecking; private set => SetField(ref isChecking, value); }
    public bool IsInstalling { get => isInstalling; private set => SetField(ref isInstalling, value); }
    public bool RestartRequired { get => restartRequired; private set => SetField(ref restartRequired, value); }
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }
    public int ProgramDownloadProgress
    {
        get => programDownloadProgress;
        private set => SetField(ref programDownloadProgress, value);
    }
    public DesktopProgramUpdate AvailableProgramUpdate
    {
        get => availableProgramUpdate;
        private set
        {
            if (SetField(ref availableProgramUpdate, value))
            {
                OnPropertyChanged(nameof(HasProgramUpdate));
            }
        }
    }
    public bool HasProgramUpdate => AvailableProgramUpdate != null;

    internal DesktopUpdateCoordinator(
        DesktopSettings settings,
        DesktopLibrarySyncViewModel librarySync,
        Action settingsChanged,
        Action<NotificationMessage> addNotification,
        Action<string, bool> showMessage,
        Func<string, string, bool> confirm,
        Action openSettings,
        IDesktopAddonUpdateService addonUpdates = null,
        IDesktopProgramUpdateService programUpdates = null)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.librarySync = librarySync ?? throw new ArgumentNullException(nameof(librarySync));
        this.settingsChanged = settingsChanged ?? (() => { });
        this.addNotification = addNotification ?? (_ => { });
        this.showMessage = showMessage ?? ((_, _) => { });
        this.confirm = confirm ?? ((_, _) => false);
        this.openSettings = openSettings ?? (() => { });
        this.addonUpdates = addonUpdates ?? new DesktopAddonUpdateService();
        this.programUpdates = programUpdates ?? new DesktopProgramUpdateService();
    }

    public void ConfigureProgramInstaller(Action<string> launch) =>
        launchProgramUpdater = launch ?? throw new ArgumentNullException(nameof(launch));

    public async Task StartAsync()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (started)
        {
            return;
        }

        started = true;
        await Task.Delay(TimeSpan.FromSeconds(5), lifetimeCancellation.Token);
        await RunScheduledChecksAsync(true, lifetimeCancellation.Token);
        timer = new System.Threading.Timer(
            _ => Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    await RunScheduledChecksAsync(false, lifetimeCancellation.Token);
                }
                catch (OperationCanceledException) when (lifetimeCancellation.IsCancellationRequested)
                {
                }
                catch (Exception exception)
                {
                    logger.Error(exception, "Periodic Avalonia update checks failed.");
                    StatusText = $"Periodic update checks failed: {exception.Message}";
                }
            }),
            null,
            TimeSpan.FromHours(1),
            TimeSpan.FromHours(1));
    }

    internal async Task RunScheduledChecksAsync(bool startup, CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var libraries = startup && UpdateSchedule.ShouldRunOnStartup(
            settings.CheckForLibraryUpdates,
            settings.LastLibraryUpdateCheck,
            now);
        var emulated = startup && UpdateSchedule.ShouldRunOnStartup(
            settings.CheckForEmulatedLibraryUpdates,
            settings.LastEmulatedLibraryUpdateCheck,
            now);
        if ((libraries || emulated) && !librarySync.IsRunning)
        {
            await librarySync.StartScheduledSyncAsync(libraries, emulated, cancellationToken);
        }

        var addons = startup
            ? UpdateSchedule.ShouldRunOnStartup(settings.CheckForAddonUpdates, settings.LastAddonUpdateCheck, now)
            : UpdateSchedule.ShouldRunPeriodically(settings.CheckForAddonUpdates, settings.LastAddonUpdateCheck, now);
        var program = startup
            ? UpdateSchedule.ShouldRunOnStartup(settings.CheckForProgramUpdates, settings.LastProgramUpdateCheck, now)
            : UpdateSchedule.ShouldRunPeriodically(settings.CheckForProgramUpdates, settings.LastProgramUpdateCheck, now);
        if (addons)
        {
            await CheckAddonsAsync(false, cancellationToken);
        }
        if (program && ProgramUpdatesSupported)
        {
            await CheckProgramAsync(false, cancellationToken);
        }
    }

    public async Task CheckLibrariesAsync(CancellationToken cancellationToken = default)
    {
        if (librarySync.IsRunning)
        {
            StatusText = "A library update is already running.";
            return;
        }

        StatusText = "Updating library integrations and emulated folders…";
        await librarySync.StartScheduledSyncAsync(true, true, cancellationToken);
        StatusText = librarySync.ProgressText;
    }

    public async Task CheckAddonsAsync(
        bool reportNoUpdates = true,
        CancellationToken cancellationToken = default)
    {
        if (IsChecking)
        {
            return;
        }

        IsChecking = true;
        StatusText = "Checking add-ons and Avalonia themes for updates…";
        try
        {
            var result = await addonUpdates.CheckAsync(cancellationToken);
            settings.LastAddonUpdateCheck = DateTime.Now;
            settingsChanged();
            AvailableAddonUpdates.Clear();
            foreach (var update in result.Updates.OrderBy(update => update.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                AvailableAddonUpdates.Add(update);
            }

            if (result.Failures.Count > 0)
            {
                var message = $"Add-on update checks failed for {result.Failures.Count} item(s): " +
                    string.Join("; ", result.Failures);
                StatusText = message;
                addNotification(new NotificationMessage(
                    "AvaloniaAddonUpdateCheckFailed",
                    message,
                    NotificationType.Error,
                    openSettings));
            }
            else if (AvailableAddonUpdates.Count > 0)
            {
                StatusText = $"{AvailableAddonUpdates.Count} add-on update(s) available.";
                addNotification(new NotificationMessage(
                    "AvaloniaAddonUpdatesAvailable",
                    StatusText,
                    NotificationType.Info,
                    openSettings));
            }
            else
            {
                StatusText = "All installed add-ons and Avalonia themes are up to date.";
                if (reportNoUpdates)
                {
                    showMessage(StatusText, false);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Avalonia add-on update check failed.");
            StatusText = $"Add-on update check failed: {exception.Message}";
            showMessage(StatusText, true);
        }
        finally
        {
            IsChecking = false;
        }
    }

    public async Task QueueSelectedAddonsAsync(CancellationToken cancellationToken = default)
    {
        var selected = AvailableAddonUpdates.Where(update => update.IsSelected).ToList();
        if (selected.Count == 0 || IsInstalling)
        {
            return;
        }

        IsInstalling = true;
        try
        {
            foreach (var update in selected)
            {
                StatusText = $"Downloading {update.Name}…";
                try
                {
                    await addonUpdates.QueueAsync(
                        update,
                        (name, license) => Task.FromResult(confirm(
                            $"{name} license",
                            $"{license}{Environment.NewLine}{Environment.NewLine}Accept this license?")),
                        cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    update.Status = $"Failed: {exception.Message}";
                    logger.Error(exception, $"Failed to queue add-on update {update.Id}.");
                }
            }

            RestartRequired = AvailableAddonUpdates.Any(update => update.Status == "Queued for restart");
            StatusText = RestartRequired
                ? "Selected add-on updates are queued and will be installed after Playnite restarts."
                : "No add-on updates were queued.";
            showMessage(StatusText, !RestartRequired);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    public async Task CheckProgramAsync(
        bool reportNoUpdates = true,
        CancellationToken cancellationToken = default)
    {
        if (!ProgramUpdatesSupported)
        {
            StatusText = ProgramUpdateSupportText;
            if (reportNoUpdates)
            {
                showMessage(StatusText, false);
            }
            return;
        }
        if (IsChecking)
        {
            return;
        }

        IsChecking = true;
        StatusText = "Checking for Playnite program updates…";
        try
        {
            var update = await programUpdates.CheckAsync(cancellationToken);
            settings.LastProgramUpdateCheck = DateTime.Now;
            settingsChanged();
            if (update != null && settings.UpdateNotificationOnPatchesOnly &&
                update.AvailableVersion.Major != update.CurrentVersion.Major)
            {
                update = null;
            }
            AvailableProgramUpdate = update;
            if (update == null)
            {
                StatusText = "Playnite is up to date.";
                if (reportNoUpdates)
                {
                    showMessage(StatusText, false);
                }
            }
            else
            {
                StatusText = $"Playnite {update.AvailableVersion} is available.";
                addNotification(new NotificationMessage(
                    "AvaloniaProgramUpdateAvailable",
                    StatusText,
                    NotificationType.Info,
                    openSettings));
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Avalonia program update check failed.");
            StatusText = $"Program update check failed: {exception.Message}";
            showMessage(StatusText, true);
        }
        finally
        {
            IsChecking = false;
        }
    }

    public async Task InstallProgramUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (AvailableProgramUpdate == null || IsInstalling)
        {
            return;
        }
        if (launchProgramUpdater == null)
        {
            showMessage("The program updater cannot start before the Desktop window is ready.", true);
            return;
        }
        if (!confirm(
                "Install Playnite update",
                $"Download and install Playnite {AvailableProgramUpdate.AvailableVersion}?"))
        {
            return;
        }

        IsInstalling = true;
        ProgramDownloadProgress = 0;
        StatusText = $"Downloading Playnite {AvailableProgramUpdate.AvailableVersion}…";
        try
        {
            var progress = new Progress<int>(value => ProgramDownloadProgress = value);
            var updaterPath = await programUpdates.DownloadAsync(
                AvailableProgramUpdate,
                progress,
                cancellationToken);
            StatusText = "Starting the verified Playnite updater…";
            launchProgramUpdater(updaterPath);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.Error(exception, "Avalonia program update installation failed.");
            StatusText = $"Program update installation failed: {exception.Message}";
            showMessage(StatusText, true);
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        lifetimeCancellation.Cancel();
        timer?.Dispose();
        lifetimeCancellation.Dispose();
    }
}
