using System.Windows.Input;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class GeneralAdvancedSettingsSection : SettingsSectionBase
{
    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private readonly DesktopSettings settings;
    private readonly Func<string> selectFolder;
    private string originalDatabasePath;
    private bool discordPresenceEnabled;
    private bool showElevatedRightsWarning;
    private bool installSizeScanUseSizeOnDisk;
    private string directoryOpenCommand;
    private string databasePath;
    private bool clearWebCacheOnNextStartup;
    private string statusText = "Web cache is unchanged.";

    public override string Key => "GeneralAdvanced";
    public override string Title => "System — Advanced";
    public override global::Avalonia.Controls.Control Content { get; }
    public ICommand SelectDatabaseFolderCommand { get; }
    public ICommand ClearWebCacheCommand { get; }
    public ICommand SetDefaultsCommand { get; }
    public bool DiscordPresenceSupported => OperatingSystem.IsWindows();
    public string DiscordPresenceSupportText => DiscordPresenceSupported
        ? "Discord Rich Presence is available."
        : "Discord Rich Presence is disabled on this platform.";

    public bool DiscordPresenceEnabled
    {
        get => discordPresenceEnabled;
        set => SetField(ref discordPresenceEnabled, value);
    }
    public bool ShowElevatedRightsWarning
    {
        get => showElevatedRightsWarning;
        set => SetField(ref showElevatedRightsWarning, value);
    }
    public bool InstallSizeScanUseSizeOnDisk
    {
        get => installSizeScanUseSizeOnDisk;
        set => SetField(ref installSizeScanUseSizeOnDisk, value);
    }
    public string DirectoryOpenCommand
    {
        get => directoryOpenCommand;
        set => SetField(ref directoryOpenCommand, value);
    }
    public string DatabasePath { get => databasePath; set => SetField(ref databasePath, value); }
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }

    public GeneralAdvancedSettingsSection(DesktopSettings settings, Func<string> selectFolder)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.selectFolder = selectFolder ?? (() => null);
        Content = new GeneralAdvancedSettingsView { DataContext = this };
        SelectDatabaseFolderCommand = new AppRelayCommand(() =>
        {
            var selected = this.selectFolder();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                DatabasePath = Path.GetFullPath(selected);
                StatusText = "The new library database folder will be used after restart. Existing data is not moved automatically.";
            }
        });
        ClearWebCacheCommand = new AppRelayCommand(() =>
        {
            clearWebCacheOnNextStartup = true;
            StatusText = "The native web-view cache will be cleared before web views initialize on next startup.";
        });
        SetDefaultsCommand = new AppRelayCommand(SetDefaults);
    }

    public override void Open()
    {
        DiscordPresenceEnabled = settings.DiscordPresenceEnabled;
        ShowElevatedRightsWarning = settings.ShowElevatedRightsWarning;
        InstallSizeScanUseSizeOnDisk = settings.InstallSizeScanUseSizeOnDisk;
        DirectoryOpenCommand = settings.DirectoryOpenCommand;
        DatabasePath = settings.DatabasePath;
        originalDatabasePath = settings.DatabasePath;
        clearWebCacheOnNextStartup = settings.ClearWebCacheOnNextStartup;
        StatusText = clearWebCacheOnNextStartup
            ? "The native web-view cache is queued to clear on next startup."
            : "Web cache is unchanged.";
    }

    public override SettingsSectionValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            return new(false, "The library database folder cannot be empty.");
        }
        try
        {
            _ = Path.GetFullPath(DatabasePath);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return new(false, $"The library database folder is invalid: {exception.Message}");
        }

        if (!string.IsNullOrWhiteSpace(DirectoryOpenCommand) &&
            !DirectoryOpenCommand.Contains("{Dir}", StringComparison.Ordinal))
        {
            return new(false, "A custom directory command must contain the {Dir} placeholder.");
        }
        return SettingsSectionValidationResult.Valid;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.DiscordPresenceEnabled = DiscordPresenceSupported && DiscordPresenceEnabled;
        settings.ShowElevatedRightsWarning = ShowElevatedRightsWarning;
        settings.InstallSizeScanUseSizeOnDisk = InstallSizeScanUseSizeOnDisk;
        settings.DirectoryOpenCommand = string.IsNullOrWhiteSpace(DirectoryOpenCommand)
            ? null
            : DirectoryOpenCommand.Trim();
        settings.DatabasePath = Path.GetFullPath(DatabasePath);
        settings.ClearWebCacheOnNextStartup = clearWebCacheOnNextStartup;
        var restart = clearWebCacheOnNextStartup ||
            !string.Equals(originalDatabasePath, settings.DatabasePath, PathComparison);
        return restart ? SettingsSectionSaveResult.SavedWithRestart : SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this &&
            SelectDatabaseFolderCommand != null && ClearWebCacheCommand != null && SetDefaultsCommand != null;
        return new(Key, valid, valid
            ? "Discord, elevation, size-on-disk, directory command, database, cache, and reset controls are available"
            : "advanced system controls are incomplete");
    }

    private void SetDefaults()
    {
        var defaults = new DesktopSettings();
        DiscordPresenceEnabled = defaults.DiscordPresenceEnabled;
        ShowElevatedRightsWarning = defaults.ShowElevatedRightsWarning;
        InstallSizeScanUseSizeOnDisk = defaults.InstallSizeScanUseSizeOnDisk;
        DirectoryOpenCommand = defaults.DirectoryOpenCommand;
        clearWebCacheOnNextStartup = false;
        StatusText = "Advanced options were reset. Save to apply them.";
    }
}
