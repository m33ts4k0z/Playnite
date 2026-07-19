using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class UpdatesSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private UpdateCheckFrequency checkForProgramUpdates;
    private UpdateCheckFrequency checkForAddonUpdates;
    private LibraryUpdateCheckFrequency checkForLibraryUpdates;
    private LibraryUpdateCheckFrequency checkForEmulatedLibraryUpdates;
    private bool updateNotificationOnPatchesOnly;

    public override string Key => "Updates";
    public override string Title => "Library — Updates";
    public override global::Avalonia.Controls.Control Content { get; }
    public DesktopUpdateCoordinator Coordinator { get; }
    public IReadOnlyList<UpdateCheckFrequency> UpdateFrequencies { get; } =
        Enum.GetValues<UpdateCheckFrequency>();
    public IReadOnlyList<LibraryUpdateCheckFrequency> LibraryFrequencies { get; } =
        Enum.GetValues<LibraryUpdateCheckFrequency>();
    public ICommand CheckLibrariesCommand { get; }
    public ICommand CheckAddonsCommand { get; }
    public ICommand QueueAddonUpdatesCommand { get; }
    public ICommand CheckProgramCommand { get; }
    public ICommand InstallProgramUpdateCommand { get; }

    public UpdateCheckFrequency CheckForProgramUpdates
    {
        get => checkForProgramUpdates;
        set => SetField(ref checkForProgramUpdates, value);
    }
    public UpdateCheckFrequency CheckForAddonUpdates
    {
        get => checkForAddonUpdates;
        set => SetField(ref checkForAddonUpdates, value);
    }
    public LibraryUpdateCheckFrequency CheckForLibraryUpdates
    {
        get => checkForLibraryUpdates;
        set => SetField(ref checkForLibraryUpdates, value);
    }
    public LibraryUpdateCheckFrequency CheckForEmulatedLibraryUpdates
    {
        get => checkForEmulatedLibraryUpdates;
        set => SetField(ref checkForEmulatedLibraryUpdates, value);
    }
    public bool UpdateNotificationOnPatchesOnly
    {
        get => updateNotificationOnPatchesOnly;
        set => SetField(ref updateNotificationOnPatchesOnly, value);
    }

    public UpdatesSettingsSection(DesktopSettings settings, DesktopUpdateCoordinator coordinator)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        Content = new UpdatesSettingsView { DataContext = this };
        CheckLibrariesCommand = new AppRelayCommand(async () => await Coordinator.CheckLibrariesAsync());
        CheckAddonsCommand = new AppRelayCommand(async () => await Coordinator.CheckAddonsAsync());
        QueueAddonUpdatesCommand = new AppRelayCommand(async () => await Coordinator.QueueSelectedAddonsAsync());
        CheckProgramCommand = new AppRelayCommand(async () => await Coordinator.CheckProgramAsync());
        InstallProgramUpdateCommand = new AppRelayCommand(async () => await Coordinator.InstallProgramUpdateAsync());
    }

    public override void Open()
    {
        CheckForProgramUpdates = settings.CheckForProgramUpdates;
        CheckForAddonUpdates = settings.CheckForAddonUpdates;
        CheckForLibraryUpdates = settings.CheckForLibraryUpdates;
        CheckForEmulatedLibraryUpdates = settings.CheckForEmulatedLibraryUpdates;
        UpdateNotificationOnPatchesOnly = settings.UpdateNotificationOnPatchesOnly;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.CheckForProgramUpdates = CheckForProgramUpdates;
        settings.CheckForAddonUpdates = CheckForAddonUpdates;
        settings.CheckForLibraryUpdates = CheckForLibraryUpdates;
        settings.CheckForEmulatedLibraryUpdates = CheckForEmulatedLibraryUpdates;
        settings.UpdateNotificationOnPatchesOnly = UpdateNotificationOnPatchesOnly;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this &&
            UpdateFrequencies.Count == 4 &&
            LibraryFrequencies.Count == 4 &&
            !string.IsNullOrWhiteSpace(Coordinator.ProgramUpdateSupportText);
        return new(Key, valid, valid
            ? "library, emulated-library, add-on, and platform-aware program update schedules are available"
            : "update schedule controls or platform status are incomplete");
    }
}
