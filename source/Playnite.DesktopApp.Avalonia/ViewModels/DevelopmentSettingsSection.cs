using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DevelopmentSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly Func<string> selectFolder;
    private List<(string Path, bool Enabled)> originalExtensions = new();
    private bool traceLogEnabled;

    public override string Key => "Development";
    public override string Title => "System — Development";
    public override global::Avalonia.Controls.Control Content { get; }
    public ObservableCollection<DevelopmentExtensionOption> Extensions { get; } = new();
    public bool TraceLogEnabled { get => traceLogEnabled; set => SetField(ref traceLogEnabled, value); }
    public ICommand AddExtensionCommand { get; }
    public ICommand RemoveExtensionCommand { get; }

    public DevelopmentSettingsSection(DesktopSettings settings, Func<string> selectFolder)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.selectFolder = selectFolder ?? (() => null);
        Content = new DevelopmentSettingsView { DataContext = this };
        AddExtensionCommand = new AppRelayCommand(AddExtension);
        RemoveExtensionCommand = new AppRelayCommand(parameter =>
        {
            if (parameter is DevelopmentExtensionOption option)
            {
                Extensions.Remove(option);
            }
        });
    }

    public override void Open()
    {
        TraceLogEnabled = settings.TraceLogEnabled;
        Extensions.Clear();
        foreach (var extension in (settings.DevelopmentExtensions ?? new List<DevelopmentExtensionPath>())
            .Where(extension => extension != null))
        {
            Extensions.Add(new DevelopmentExtensionOption(extension.Path, extension.IsEnabled));
        }
        originalExtensions = Snapshot();
    }

    public override SettingsSectionValidationResult Validate()
    {
        var normalized = new List<(DevelopmentExtensionOption Extension, string Path)>();
        foreach (var extension in Extensions.Where(extension => !string.IsNullOrWhiteSpace(extension.Path)))
        {
            try
            {
                normalized.Add((extension, Path.GetFullPath(extension.Path)));
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return new(false, $"Developer extension folder is invalid: {exception.Message}");
            }
        }

        var duplicate = normalized
            .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            return new(false, $"The developer extension folder '{duplicate.Key}' is listed more than once.");
        }

        foreach (var extension in Extensions.Where(extension => extension.IsEnabled))
        {
            if (string.IsNullOrWhiteSpace(extension.Path) || !Directory.Exists(extension.Path))
            {
                return new(false, $"Developer extension folder not found: {extension.Path ?? "(empty)"}");
            }
        }
        return SettingsSectionValidationResult.Valid;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.TraceLogEnabled = TraceLogEnabled;
        settings.DevelopmentExtensions = Extensions
            .Where(extension => !string.IsNullOrWhiteSpace(extension.Path))
            .Select(extension => new DevelopmentExtensionPath
            {
                Path = Path.GetFullPath(extension.Path),
                IsEnabled = extension.IsEnabled
            })
            .ToList();
        var changed = !originalExtensions.SequenceEqual(Snapshot());
        return changed ? SettingsSectionSaveResult.SavedWithRestart : SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this && AddExtensionCommand != null && RemoveExtensionCommand != null;
        return new(Key, valid, valid
            ? $"trace logging and {Extensions.Count} external extension folder(s) are configurable"
            : "development controls are incomplete");
    }

    private void AddExtension()
    {
        var path = selectFolder();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        path = Path.GetFullPath(path);
        if (Extensions.Any(extension => string.Equals(extension.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        Extensions.Add(new DevelopmentExtensionOption(path, true));
    }

    private List<(string Path, bool Enabled)> Snapshot() => Extensions
        .Where(extension => !string.IsNullOrWhiteSpace(extension.Path))
        .Select(extension => (NormalizePathForComparison(extension.Path), extension.IsEnabled))
        .OrderBy(extension => extension.Item1, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static string NormalizePathForComparison(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path.Trim();
        }
    }
}

public sealed class DevelopmentExtensionOption : INotifyPropertyChanged
{
    private string path;
    private bool isEnabled;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Path { get => path; set => SetField(ref path, value); }
    public bool IsEnabled { get => isEnabled; set => SetField(ref isEnabled, value); }

    public DevelopmentExtensionOption(string path, bool isEnabled)
    {
        this.path = path;
        this.isEnabled = isEnabled;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
