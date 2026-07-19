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

    public override string Key => "ImportExclusions";
    public override string Title => "Library — Import exclusions";
    public override global::Avalonia.Controls.Control Content { get; }
    public ObservableCollection<ImportExclusionRow> Exclusions { get; } = new();
    public ICommand RemoveCommand { get; }
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

    public ImportExclusionsSettingsSection(GameDatabase database)
    {
        this.database = database;
        RemoveCommand = new AppRelayCommand(RemoveSelected, () => SelectedExclusion != null);
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
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck() => new(
        Key,
        Content.DataContext == this && (database == null || Exclusions.Count + pendingRemovalIds.Count <= database.ImportExclusions.Count),
        $"{Exclusions.Count} import exclusion(s) are available with deferred removal");

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
}

public sealed record ImportExclusionRow(
    Guid Id,
    string Name,
    string GameId,
    string LibraryName,
    Guid LibraryId);
