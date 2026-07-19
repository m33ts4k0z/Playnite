using System.Windows.Input;
using Playnite.Avalonia.App.Services;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class BackupSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly Func<string> selectFolder;
    private bool autoBackupEnabled;
    private AutoBackupFrequency autoBackupFrequency;
    private string autoBackupDir;
    private decimal rotatingBackups;
    private bool autoBackupIncludeLibFiles;
    private bool autoBackupIncludeExtensionsData;
    private bool autoBackupIncludeExtensions;
    private bool autoBackupIncludeThemes;

    public override string Key => "Backup";
    public override string Title => "System — Backup";
    public override global::Avalonia.Controls.Control Content { get; }
    public DesktopBackupCoordinator Coordinator { get; }
    public IReadOnlyList<AutoBackupFrequency> Frequencies { get; } =
        Enum.GetValues<AutoBackupFrequency>();
    public ICommand SelectFolderCommand { get; }

    public bool AutoBackupEnabled { get => autoBackupEnabled; set => SetField(ref autoBackupEnabled, value); }
    public AutoBackupFrequency AutoBackupFrequency
    {
        get => autoBackupFrequency;
        set => SetField(ref autoBackupFrequency, value);
    }
    public string AutoBackupDir { get => autoBackupDir; set => SetField(ref autoBackupDir, value); }
    public decimal RotatingBackups
    {
        get => rotatingBackups;
        set => SetField(ref rotatingBackups, Math.Clamp(value, 0, 10));
    }
    public bool AutoBackupIncludeLibFiles
    {
        get => autoBackupIncludeLibFiles;
        set => SetField(ref autoBackupIncludeLibFiles, value);
    }
    public bool AutoBackupIncludeExtensionsData
    {
        get => autoBackupIncludeExtensionsData;
        set => SetField(ref autoBackupIncludeExtensionsData, value);
    }
    public bool AutoBackupIncludeExtensions
    {
        get => autoBackupIncludeExtensions;
        set => SetField(ref autoBackupIncludeExtensions, value);
    }
    public bool AutoBackupIncludeThemes
    {
        get => autoBackupIncludeThemes;
        set => SetField(ref autoBackupIncludeThemes, value);
    }

    public BackupSettingsSection(
        DesktopSettings settings,
        DesktopBackupCoordinator coordinator,
        Func<string> selectFolder)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.selectFolder = selectFolder ?? (() => null);
        Content = new BackupSettingsView { DataContext = this };
        SelectFolderCommand = new AppRelayCommand(() =>
        {
            var selected = this.selectFolder();
            if (!string.IsNullOrWhiteSpace(selected))
            {
                AutoBackupDir = selected;
            }
        });
    }

    public override void Open()
    {
        AutoBackupEnabled = settings.AutoBackupEnabled;
        AutoBackupFrequency = settings.AutoBackupFrequency;
        AutoBackupDir = settings.AutoBackupDir;
        RotatingBackups = settings.RotatingBackups;
        AutoBackupIncludeLibFiles = settings.AutoBackupIncludeLibFiles;
        AutoBackupIncludeExtensionsData = settings.AutoBackupIncludeExtensionsData;
        AutoBackupIncludeExtensions = settings.AutoBackupIncludeExtensions;
        AutoBackupIncludeThemes = settings.AutoBackupIncludeThemes;
    }

    public override SettingsSectionValidationResult Validate()
    {
        if (!AutoBackupEnabled)
        {
            return SettingsSectionValidationResult.Valid;
        }
        if (string.IsNullOrWhiteSpace(AutoBackupDir))
        {
            return new(false, "Choose an automatic-backup folder.");
        }

        try
        {
            _ = Path.GetFullPath(AutoBackupDir);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return new(false, $"The automatic-backup folder is invalid: {exception.Message}");
        }
        return SettingsSectionValidationResult.Valid;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.AutoBackupEnabled = AutoBackupEnabled;
        settings.AutoBackupFrequency = AutoBackupFrequency;
        settings.AutoBackupDir = string.IsNullOrWhiteSpace(AutoBackupDir)
            ? null
            : Path.GetFullPath(AutoBackupDir);
        settings.RotatingBackups = decimal.ToInt32(RotatingBackups);
        settings.AutoBackupIncludeLibFiles = AutoBackupIncludeLibFiles;
        settings.AutoBackupIncludeExtensionsData = AutoBackupIncludeExtensionsData;
        settings.AutoBackupIncludeExtensions = AutoBackupIncludeExtensions;
        settings.AutoBackupIncludeThemes = AutoBackupIncludeThemes;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this && Frequencies.Count == 2 && SelectFolderCommand != null;
        return new(Key, valid, valid
            ? "daily/weekly scheduling, rotation, folder selection, and four optional data groups are available"
            : "automatic-backup controls are incomplete");
    }
}
