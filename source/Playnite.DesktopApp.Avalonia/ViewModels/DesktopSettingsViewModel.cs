using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

// The application settings overlay. This is the foundation of the settings port:
// a working copy of the shared DesktopSettings edited behind Save/Cancel, a
// section navigation, and restart tracking. Sections are added incrementally;
// General is the first. Save writes the working copy back into the shared
// settings and raises the onSaved callback — the shell persists and re-applies
// on that signal (DesktopAppViewModel.SettingsChanged → MainWindow), so no
// store is threaded through here.
public sealed class DesktopSettingsViewModel : INotifyPropertyChanged
{
    private readonly DesktopSettings settings;
    private readonly Action onSaved;
    private readonly Action<string, bool> showMessage;

    private bool isVisible;
    private DesktopSettingsSection selectedSection;
    private bool restartRequired;

    // General section working copy.
    private LanguageOption selectedLanguage;
    private string originalLanguageId;
    private bool enableTray;
    private bool minimizeToTray;
    private bool closeToTray;
    private bool startOnBoot;
    private bool startOnBootClosedToTray;
    private bool startMinimized;
    private bool originalStartOnBoot;
    private bool originalStartOnBootClosedToTray;
    private bool downloadMetadataOnImport;
    private PlaytimeImportMode playtimeImportMode;
    private bool useAvaloniaShell;

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<DesktopSettingsSection> Sections { get; } = new()
    {
        new DesktopSettingsSection("General", "General")
    };

    public IReadOnlyList<PlaytimeImportMode> PlaytimeImportModes { get; } =
        Enum.GetValues<PlaytimeImportMode>();

    public IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
        LanguageCatalog.Discover(Path.Combine(AppContext.BaseDirectory, "Localization"));

    // The shell switch is offered only when both the WPF and Avalonia executables
    // are present next to each other (a packaged install), so a dev or partial
    // layout does not show a toggle that cannot take effect.
    public bool CanSwitchShells => global::Playnite.PlaynitePaths.CanSwitchShells;

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public DesktopSettingsViewModel(
        DesktopSettings settings,
        Action onSaved,
        Action<string, bool> showMessage)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.onSaved = onSaved ?? (() => { });
        this.showMessage = showMessage ?? ((_, _) => { });
        selectedSection = Sections[0];
        SaveCommand = new AppRelayCommand(Save, () => IsVisible);
        CancelCommand = new AppRelayCommand(Close, () => IsVisible);
    }

    public bool IsVisible
    {
        get => isVisible;
        private set
        {
            if (SetField(ref isVisible, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public DesktopSettingsSection SelectedSection
    {
        get => selectedSection;
        set
        {
            if (SetField(ref selectedSection, value))
            {
                OnPropertyChanged(nameof(IsGeneralSelected));
            }
        }
    }

    public bool IsGeneralSelected => SelectedSection?.Key == "General";

    public bool RestartRequired
    {
        get => restartRequired;
        private set => SetField(ref restartRequired, value);
    }

    public LanguageOption SelectedLanguage
    {
        get => selectedLanguage;
        set => SetField(ref selectedLanguage, value);
    }

    public bool EnableTray
    {
        get => enableTray;
        set
        {
            if (SetField(ref enableTray, value))
            {
                OnPropertyChanged(nameof(TrayOptionsEnabled));
            }
        }
    }

    public bool TrayOptionsEnabled => EnableTray;
    public bool MinimizeToTray { get => minimizeToTray; set => SetField(ref minimizeToTray, value); }
    public bool CloseToTray { get => closeToTray; set => SetField(ref closeToTray, value); }

    public bool StartOnBoot
    {
        get => startOnBoot;
        set
        {
            if (SetField(ref startOnBoot, value))
            {
                OnPropertyChanged(nameof(StartOnBootOptionsEnabled));
            }
        }
    }

    public bool StartOnBootOptionsEnabled => StartOnBoot;
    public bool StartOnBootClosedToTray { get => startOnBootClosedToTray; set => SetField(ref startOnBootClosedToTray, value); }
    public bool StartMinimized { get => startMinimized; set => SetField(ref startMinimized, value); }
    public bool DownloadMetadataOnImport { get => downloadMetadataOnImport; set => SetField(ref downloadMetadataOnImport, value); }
    public PlaytimeImportMode PlaytimeImportMode { get => playtimeImportMode; set => SetField(ref playtimeImportMode, value); }
    public bool UseAvaloniaShell { get => useAvaloniaShell; set => SetField(ref useAvaloniaShell, value); }

    public bool Open()
    {
        if (IsVisible)
        {
            return false;
        }

        // Snapshot the live settings into the working copy.
        originalLanguageId = string.IsNullOrWhiteSpace(settings.Language)
            ? LanguageCatalog.SourceLanguageId
            : settings.Language;
        selectedLanguage =
            AvailableLanguages.FirstOrDefault(option =>
                string.Equals(option.Id, originalLanguageId, StringComparison.OrdinalIgnoreCase))
            ?? AvailableLanguages.FirstOrDefault();
        enableTray = settings.EnableTray;
        minimizeToTray = settings.MinimizeToTray;
        closeToTray = settings.CloseToTray;
        startOnBoot = settings.StartOnBoot;
        startOnBootClosedToTray = settings.StartOnBootClosedToTray;
        startMinimized = settings.StartMinimized;
        originalStartOnBoot = settings.StartOnBoot;
        originalStartOnBootClosedToTray = settings.StartOnBootClosedToTray;
        downloadMetadataOnImport = settings.DownloadMetadataOnImport;
        playtimeImportMode = settings.LibraryPlaytimeImportMode;
        useAvaloniaShell = global::Playnite.PlaynitePaths.IsAvaloniaShellPreferred;
        RestartRequired = false;
        RaiseAllFieldChanges();
        SelectedSection = Sections[0];
        IsVisible = true;
        return true;
    }

    public void Save()
    {
        if (!IsVisible)
        {
            return;
        }

        settings.EnableTray = EnableTray;
        settings.MinimizeToTray = MinimizeToTray;
        settings.CloseToTray = CloseToTray;
        settings.StartOnBoot = StartOnBoot;
        settings.StartOnBootClosedToTray = StartOnBootClosedToTray;
        settings.StartMinimized = StartMinimized;
        settings.DownloadMetadataOnImport = DownloadMetadataOnImport;
        settings.LibraryPlaytimeImportMode = PlaytimeImportMode;

        // Run-on-boot registers a Startup shortcut immediately (no restart). Only
        // touch it when it changed so an unrelated Save does not rewrite it.
        if (StartOnBoot != originalStartOnBoot ||
            StartOnBootClosedToTray != originalStartOnBootClosedToTray)
        {
            try
            {
                global::Playnite.SystemIntegration.SetBootupStateRegistration(
                    StartOnBoot, StartOnBootClosedToTray);
                originalStartOnBoot = StartOnBoot;
                originalStartOnBootClosedToTray = StartOnBootClosedToTray;
            }
            catch (Exception exception)
            {
                showMessage($"The run-on-startup setting could not be applied: {exception.Message}", true);
            }
        }

        // Language is applied on the next launch, so a change flags a restart.
        var newLanguageId = SelectedLanguage?.Id ?? LanguageCatalog.SourceLanguageId;
        if (!string.Equals(newLanguageId, originalLanguageId, StringComparison.OrdinalIgnoreCase))
        {
            settings.Language = newLanguageId;
            RestartRequired = true;
        }

        // The shell preference is a marker next to the executables, not part of the
        // settings file, so apply it directly and flag a restart when it changes.
        if (CanSwitchShells && UseAvaloniaShell != global::Playnite.PlaynitePaths.IsAvaloniaShellPreferred)
        {
            if (global::Playnite.PlaynitePaths.SetAvaloniaShellPreferred(UseAvaloniaShell))
            {
                RestartRequired = true;
            }
            else
            {
                showMessage(
                    "The interface preference could not be saved. Run Playnite from a writable location and try again.",
                    true);
            }
        }

        // The shell persists the settings file and re-applies live state (e.g. the
        // tray) in response to this callback.
        onSaved();
        showMessage(
            RestartRequired
                ? "Settings saved. Restart Playnite to apply all changes."
                : "Settings saved.",
            false);
        IsVisible = false;
    }

    public void Close()
    {
        if (IsVisible)
        {
            IsVisible = false;
        }
    }

    private void RaiseAllFieldChanges()
    {
        OnPropertyChanged(nameof(SelectedLanguage));
        OnPropertyChanged(nameof(EnableTray));
        OnPropertyChanged(nameof(TrayOptionsEnabled));
        OnPropertyChanged(nameof(MinimizeToTray));
        OnPropertyChanged(nameof(CloseToTray));
        OnPropertyChanged(nameof(StartOnBoot));
        OnPropertyChanged(nameof(StartOnBootOptionsEnabled));
        OnPropertyChanged(nameof(StartOnBootClosedToTray));
        OnPropertyChanged(nameof(StartMinimized));
        OnPropertyChanged(nameof(DownloadMetadataOnImport));
        OnPropertyChanged(nameof(PlaytimeImportMode));
        OnPropertyChanged(nameof(UseAvaloniaShell));
        OnPropertyChanged(nameof(CanSwitchShells));
    }

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)CancelCommand).RaiseCanExecuteChanged();
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
}

public sealed class DesktopSettingsSection
{
    public string Key { get; }
    public string Title { get; }

    public DesktopSettingsSection(string key, string title)
    {
        Key = key;
        Title = title;
    }
}
