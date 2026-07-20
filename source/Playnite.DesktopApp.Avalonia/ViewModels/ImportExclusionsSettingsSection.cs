using System.Collections.ObjectModel;
using System.Windows.Input;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class ImportExclusionsSettingsSection : SettingsSectionBase
{
    private readonly GameDatabase database;
    private readonly List<Guid> pendingRemovalIds = new();
    private ImportExclusionRow selectedExclusion;
    private ScannerPathExclusionRow selectedScannerPath;
    private ScannerExclusionTarget selectedScannerTarget;
    private string newScannerPath = string.Empty;
    private bool newScannerPathIsDirectory;

    public override string Key => "ImportExclusions";
    public override string Title => "Library — Import exclusions";
    public override global::Avalonia.Controls.Control Content { get; }
    public ObservableCollection<ImportExclusionRow> Exclusions { get; } = new();
    public ObservableCollection<ScannerPathExclusionRow> ScannerPaths { get; } = new();
    public ObservableCollection<ScannerExclusionTarget> ScannerTargets { get; } = new();
    public ICommand RemoveCommand { get; }
    public ICommand AddScannerPathCommand { get; }
    public ICommand RemoveScannerPathCommand { get; }
    public ImportExclusionRow SelectedExclusion
    {
        get => selectedExclusion;
        set
        {
            if (SetField(ref selectedExclusion, value))
            {
                ((AppRelayCommand)RemoveCommand).RaiseCanExecuteChanged();
            }
        }
    }
    public ScannerPathExclusionRow SelectedScannerPath
    {
        get => selectedScannerPath;
        set
        {
            if (SetField(ref selectedScannerPath, value))
            {
                ((AppRelayCommand)RemoveScannerPathCommand).RaiseCanExecuteChanged();
            }
        }
    }
    public ScannerExclusionTarget SelectedScannerTarget
    {
        get => selectedScannerTarget;
        set
        {
            if (SetField(ref selectedScannerTarget, value))
            {
                RaiseAddScannerPathState();
            }
        }
    }
    public string NewScannerPath
    {
        get => newScannerPath;
        set
        {
            if (SetField(ref newScannerPath, value ?? string.Empty))
            {
                RaiseAddScannerPathState();
            }
        }
    }
    public bool NewScannerPathIsDirectory
    {
        get => newScannerPathIsDirectory;
        set => SetField(ref newScannerPathIsDirectory, value);
    }

    public ImportExclusionsSettingsSection(GameDatabase database)
    {
        this.database = database;
        RemoveCommand = new AppRelayCommand(RemoveSelected, () => SelectedExclusion != null);
        AddScannerPathCommand = new AppRelayCommand(AddScannerPath, () =>
            SelectedScannerTarget != null && !string.IsNullOrWhiteSpace(NewScannerPath));
        RemoveScannerPathCommand = new AppRelayCommand(RemoveSelectedScannerPath, () => SelectedScannerPath != null);
        Content = new ImportExclusionsSettingsView { DataContext = this };
    }

    public override void Open()
    {
        pendingRemovalIds.Clear();
        Exclusions.Clear();
        if (database != null)
        {
            foreach (var exclusion in database.ImportExclusions
                         .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Exclusions.Add(new ImportExclusionRow(
                    exclusion.Id,
                    exclusion.Name,
                    exclusion.GameId,
                    exclusion.LibraryName,
                    exclusion.LibraryId));
            }
        }

        SelectedExclusion = null;
        ScannerPaths.Clear();
        ScannerTargets.Clear();
        if (database != null)
        {
            foreach (var scanner in database.GameScanners
                         .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                ScannerTargets.Add(new ScannerExclusionTarget(scanner.Id, scanner.Name, scanner.Directory));
                foreach (var path in scanner.ExcludedFiles ?? new List<string>())
                {
                    ScannerPaths.Add(new ScannerPathExclusionRow(scanner.Id, scanner.Name, path, false));
                }
                foreach (var path in scanner.ExcludedDirectories ?? new List<string>())
                {
                    ScannerPaths.Add(new ScannerPathExclusionRow(scanner.Id, scanner.Name, path, true));
                }
            }
        }
        SelectedScannerPath = null;
        SelectedScannerTarget = ScannerTargets.FirstOrDefault();
        NewScannerPath = string.Empty;
    }

    public override SettingsSectionSaveResult Save()
    {
        if (database != null && pendingRemovalIds.Count > 0)
        {
            var removals = pendingRemovalIds
                .Select(id => database.ImportExclusions[id])
                .Where(item => item != null)
                .ToList();
            if (removals.Count > 0)
            {
                database.ImportExclusions.Remove(removals);
            }
        }

        pendingRemovalIds.Clear();
        if (database != null)
        {
            foreach (var scanner in database.GameScanners)
            {
                var files = ScannerPaths.Where(row => row.ScannerId == scanner.Id && !row.IsDirectory)
                    .Select(row => row.Path).ToList();
                var directories = ScannerPaths.Where(row => row.ScannerId == scanner.Id && row.IsDirectory)
                    .Select(row => row.Path).ToList();
                if (!files.IsListEqual(scanner.ExcludedFiles) || !directories.IsListEqual(scanner.ExcludedDirectories))
                {
                    scanner.ExcludedFiles = files;
                    scanner.ExcludedDirectories = directories;
                    database.GameScanners.Update(scanner);
                }
            }
        }
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck() => new(
        Key,
        Content.DataContext == this && (database == null || Exclusions.Count + pendingRemovalIds.Count <= database.ImportExclusions.Count),
        $"{Exclusions.Count} library exclusion(s) and {ScannerPaths.Count} scanner path exclusion(s) are available");

    private void RemoveSelected()
    {
        if (SelectedExclusion == null)
        {
            return;
        }

        pendingRemovalIds.Add(SelectedExclusion.Id);
        Exclusions.Remove(SelectedExclusion);
        SelectedExclusion = null;
    }

    private void AddScannerPath()
    {
        var path = NewScannerPath.Trim();
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (!ScannerPaths.Any(row => row.ScannerId == SelectedScannerTarget.Id &&
            row.IsDirectory == NewScannerPathIsDirectory && comparer.Equals(row.Path, path)))
        {
            ScannerPaths.Add(new ScannerPathExclusionRow(
                SelectedScannerTarget.Id,
                SelectedScannerTarget.Name,
                path,
                NewScannerPathIsDirectory));
        }
        NewScannerPath = string.Empty;
    }

    private void RemoveSelectedScannerPath()
    {
        if (SelectedScannerPath == null)
        {
            return;
        }
        ScannerPaths.Remove(SelectedScannerPath);
        SelectedScannerPath = null;
    }

    private void RaiseAddScannerPathState() =>
        ((AppRelayCommand)AddScannerPathCommand).RaiseCanExecuteChanged();
}

public sealed record ImportExclusionRow(
    Guid Id,
    string Name,
    string GameId,
    string LibraryName,
    Guid LibraryId);

public sealed record ScannerExclusionTarget(Guid Id, string Name, string Directory)
{
    public override string ToString() => Name;
}

public sealed record ScannerPathExclusionRow(
    Guid ScannerId,
    string ScannerName,
    string Path,
    bool IsDirectory)
{
    public string Kind => IsDirectory ? "Folder" : "ROM file";
}
