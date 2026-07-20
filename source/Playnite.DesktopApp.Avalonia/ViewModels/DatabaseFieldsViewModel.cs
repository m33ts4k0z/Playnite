using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Playnite.Common;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Emulators;
using Playnite.SDK;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DatabaseFieldsViewModel : INotifyPropertyChanged
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private readonly GameDatabase database;
    private readonly Func<DesktopDialogService> dialogs;
    private readonly Action synchronizeLibrary;
    private readonly Action<string, bool> showMessage;
    private DatabaseFieldSection selectedSection;
    private DatabaseFieldItem selectedItem;
    private CompletionStatusChoice selectedDefaultStatus;
    private CompletionStatusChoice selectedPlayedStatus;
    private EmulationSpecificationOption selectedPlatformSpecification;
    private EmulationSpecificationOption selectedRegionSpecification;
    private string searchText = string.Empty;
    private string originalState;
    private string statusText = string.Empty;
    private bool isVisible;

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }
    public ObservableCollection<DatabaseFieldSection> Sections { get; } = new();
    public ObservableCollection<CompletionStatusChoice> CompletionStatusChoices { get; } = new();
    public IReadOnlyList<EmulationSpecificationOption> PlatformSpecifications { get; }
    public IReadOnlyList<EmulationSpecificationOption> RegionSpecifications { get; }
    public DatabaseFieldSection SelectedSection
    {
        get => selectedSection;
        set
        {
            if (!SetField(ref selectedSection, value))
            {
                return;
            }
            SearchText = string.Empty;
            SelectedItem = SelectedSection?.Items.FirstOrDefault();
            OnPropertyChanged(nameof(IsPlatformSection));
            OnPropertyChanged(nameof(IsRegionSection));
            OnPropertyChanged(nameof(IsCompletionStatusSection));
            OnPropertyChanged(nameof(IsFilterPresetSection));
            OnPropertyChanged(nameof(IsGenericSection));
            RaiseCommandStates();
        }
    }

    public DatabaseFieldItem SelectedItem
    {
        get => selectedItem;
        set
        {
            if (!SetField(ref selectedItem, value))
            {
                return;
            }
            SynchronizeSpecificationSelections();
            OnPropertyChanged(nameof(SelectedPlatform));
            OnPropertyChanged(nameof(SelectedRegion));
            OnPropertyChanged(nameof(SelectedFilterPreset));
            OnPropertyChanged(nameof(SelectedPlatformIconPreview));
            OnPropertyChanged(nameof(SelectedPlatformCoverPreview));
            OnPropertyChanged(nameof(SelectedPlatformBackgroundPreview));
            RaiseCommandStates();
        }
    }

    public Platform SelectedPlatform => SelectedItem?.Model as Platform;
    public Region SelectedRegion => SelectedItem?.Model as Region;
    public FilterPreset SelectedFilterPreset => SelectedItem?.Model as FilterPreset;
    public bool IsPlatformSection => SelectedSection?.Kind == DatabaseFieldKind.Platforms;
    public bool IsRegionSection => SelectedSection?.Kind == DatabaseFieldKind.Regions;
    public bool IsCompletionStatusSection => SelectedSection?.Kind == DatabaseFieldKind.CompletionStatuses;
    public bool IsFilterPresetSection => SelectedSection?.Kind == DatabaseFieldKind.FilterPresets;
    public bool IsGenericSection => !IsPlatformSection && !IsRegionSection &&
        !IsCompletionStatusSection && !IsFilterPresetSection;
    public string SelectedPlatformIconPreview => ResolveMediaPath(SelectedPlatform?.Icon);
    public string SelectedPlatformCoverPreview => ResolveMediaPath(SelectedPlatform?.Cover);
    public string SelectedPlatformBackgroundPreview => ResolveMediaPath(SelectedPlatform?.Background);
    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetField(ref searchText, value ?? string.Empty))
            {
                ApplySearch();
            }
        }
    }

    public CompletionStatusChoice SelectedDefaultStatus
    {
        get => selectedDefaultStatus;
        set => SetField(ref selectedDefaultStatus, value);
    }

    public CompletionStatusChoice SelectedPlayedStatus
    {
        get => selectedPlayedStatus;
        set => SetField(ref selectedPlayedStatus, value);
    }

    public EmulationSpecificationOption SelectedPlatformSpecification
    {
        get => selectedPlatformSpecification;
        set
        {
            if (SetField(ref selectedPlatformSpecification, value) && SelectedPlatform != null)
            {
                SelectedPlatform.SpecificationId = value?.Id;
            }
        }
    }

    public EmulationSpecificationOption SelectedRegionSpecification
    {
        get => selectedRegionSpecification;
        set
        {
            if (SetField(ref selectedRegionSpecification, value) && SelectedRegion != null)
            {
                SelectedRegion.SpecificationId = value?.Id;
            }
        }
    }

    public ICommand AddCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand RemoveUnusedCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand SelectPlatformMediaCommand { get; }
    public ICommand RemovePlatformMediaCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CloseCommand { get; }

    public DatabaseFieldsViewModel(
        GameDatabase database,
        Func<DesktopDialogService> dialogs,
        Action synchronizeLibrary,
        Action<string, bool> showMessage)
    {
        this.database = database;
        this.dialogs = dialogs ?? (() => null);
        this.synchronizeLibrary = synchronizeLibrary ?? (() => { });
        this.showMessage = showMessage ?? ((_, _) => { });
        PlatformSpecifications = new[] { new EmulationSpecificationOption(null, "None") }
            .Concat(Emulation.Platforms
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new EmulationSpecificationOption(item.Id, item.Name)))
            .ToList();
        RegionSpecifications = new[] { new EmulationSpecificationOption(null, "None") }
            .Concat(Emulation.Regions
                .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new EmulationSpecificationOption(item.Id, item.Name)))
            .ToList();

        AddCommand = new AppRelayCommand(AddItem, () => SelectedSection?.CanAdd == true);
        RenameCommand = new AppRelayCommand(RenameItem, () => SelectedItem != null);
        RemoveCommand = new AppRelayCommand(RemoveItem, () => SelectedItem != null);
        RemoveUnusedCommand = new AppRelayCommand(RemoveUnused, () => SelectedSection?.CanRemoveUnused == true);
        MoveUpCommand = new AppRelayCommand(() => MoveSelected(-1), () => CanMoveSelected(-1));
        MoveDownCommand = new AppRelayCommand(() => MoveSelected(1), () => CanMoveSelected(1));
        SelectPlatformMediaCommand = new AppRelayCommand(parameter => SelectPlatformMedia(parameter as string),
            () => SelectedPlatform != null);
        RemovePlatformMediaCommand = new AppRelayCommand(parameter => RemovePlatformMedia(parameter as string),
            () => SelectedPlatform != null);
        SaveCommand = new AppRelayCommand(() => Save());
        CloseCommand = new AppRelayCommand(Close);
    }

    public bool Open(string kind = null)
    {
        if (database == null || IsVisible)
        {
            return false;
        }

        LoadSections();
        LoadCompletionStatusChoices();
        SelectedSection = Sections.FirstOrDefault(section =>
            string.Equals(section.Kind.ToString(), kind, StringComparison.OrdinalIgnoreCase)) ?? Sections.FirstOrDefault();
        originalState = CaptureState();
        StatusText = $"{Sections.Sum(section => section.Items.Count):N0} library field value(s) loaded.";
        IsVisible = true;
        return true;
    }

    public void Close()
    {
        if (!IsVisible)
        {
            return;
        }
        if (HasChanges())
        {
            var result = dialogs()?.ShowMessage(
                "Save library field changes before closing?",
                "Unsaved library fields",
                new[] { "Save", "Discard", "Cancel" },
                0,
                2);
            if (result == "Cancel")
            {
                return;
            }
            if (result == "Save")
            {
                Save();
                return;
            }
        }
        IsVisible = false;
    }

    public bool Save()
    {
        if (!Validate(out var error))
        {
            StatusText = error;
            showMessage(error, true);
            return false;
        }
        try
        {
            PreparePlatformMedia();
            using (database.BufferedUpdate())
            {
                SynchronizeCollection(database.Platforms, Models<Platform>(DatabaseFieldKind.Platforms));
                SynchronizeCollection(database.Categories, Models<Category>(DatabaseFieldKind.Categories));
                SynchronizeCollection(database.Genres, Models<Genre>(DatabaseFieldKind.Genres));
                SynchronizeCollection(database.Companies, Models<Company>(DatabaseFieldKind.Companies));
                SynchronizeCollection(database.Features, Models<GameFeature>(DatabaseFieldKind.Features));
                SynchronizeCollection(database.Tags, Models<Tag>(DatabaseFieldKind.Tags));
                SynchronizeCollection(database.Series, Models<Series>(DatabaseFieldKind.Series));
                SynchronizeCollection(database.AgeRatings, Models<AgeRating>(DatabaseFieldKind.AgeRatings));
                SynchronizeCollection(database.Regions, Models<Region>(DatabaseFieldKind.Regions));
                SynchronizeCollection(database.Sources, Models<GameSource>(DatabaseFieldKind.Sources));
                SynchronizeCollection(database.CompletionStatuses,
                    Models<CompletionStatus>(DatabaseFieldKind.CompletionStatuses));
                SynchronizeCollection(database.FilterPresets, Models<FilterPreset>(DatabaseFieldKind.FilterPresets));
            }
            database.SetCompletionStatusSettings(new CompletionStatusSettings
            {
                DefaultStatus = SelectedDefaultStatus?.Id ?? Guid.Empty,
                PlayedStatus = SelectedPlayedStatus?.Id ?? Guid.Empty
            });
            var filterSettings = database.GetFilterPresetsSettings();
            filterSettings.SortingOrder = Models<FilterPreset>(DatabaseFieldKind.FilterPresets)
                .Select(item => item.Id).ToList();
            database.SetFilterPresetsSettings(filterSettings);
            synchronizeLibrary();
            originalState = CaptureState();
            StatusText = "Library fields saved.";
            showMessage(StatusText, false);
            IsVisible = false;
            return true;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Failed to save Avalonia database fields.");
            StatusText = $"Library fields could not be saved: {exception.Message}";
            showMessage(StatusText, true);
            return false;
        }
    }

    internal DatabaseObject AddItemForTest(DatabaseFieldKind kind, string name)
    {
        var section = Sections.FirstOrDefault(item => item.Kind == kind);
        return AddItem(section, name, false)?.Model;
    }

    internal int RemoveUnusedForTest(DatabaseFieldKind kind)
    {
        var section = Sections.FirstOrDefault(item => item.Kind == kind);
        return RemoveUnused(section, false);
    }

    private void LoadSections()
    {
        Sections.Clear();
        Sections.Add(CreateSection(DatabaseFieldKind.Platforms, "Platforms", database.Platforms.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Categories, "Categories", database.Categories.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Genres, "Genres", database.Genres.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Companies, "Companies", database.Companies.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Features, "Features", database.Features.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Tags, "Tags", database.Tags.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Series, "Series", database.Series.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.AgeRatings, "Age ratings", database.AgeRatings.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Regions, "Regions", database.Regions.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.Sources, "Sources", database.Sources.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.CompletionStatuses, "Completion statuses",
            database.CompletionStatuses.GetClone(), true, true));
        Sections.Add(CreateSection(DatabaseFieldKind.FilterPresets, "Filter presets",
            database.GetSortedFilterPresets().GetClone(), false, false));
    }

    private static DatabaseFieldSection CreateSection<T>(
        DatabaseFieldKind kind,
        string title,
        IEnumerable<T> items,
        bool canAdd,
        bool canRemoveUnused)
        where T : DatabaseObject => new(
            kind,
            title,
            new ObservableCollection<DatabaseFieldItem>((items ?? Array.Empty<T>())
                .OrderBy(item => kind == DatabaseFieldKind.FilterPresets ? string.Empty : item.Name,
                    StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new DatabaseFieldItem(item))),
            canAdd,
            canRemoveUnused);

    private void LoadCompletionStatusChoices(Guid? defaultStatus = null, Guid? playedStatus = null)
    {
        var settings = database.GetCompletionStatusSettings();
        defaultStatus ??= settings.DefaultStatus;
        playedStatus ??= settings.PlayedStatus;
        CompletionStatusChoices.Clear();
        CompletionStatusChoices.Add(new CompletionStatusChoice(Guid.Empty, "No status"));
        foreach (var status in Models<CompletionStatus>(DatabaseFieldKind.CompletionStatuses)
                     .OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            CompletionStatusChoices.Add(new CompletionStatusChoice(status.Id, status.Name));
        }
        SelectedDefaultStatus = CompletionStatusChoices.FirstOrDefault(item => item.Id == defaultStatus) ??
            CompletionStatusChoices[0];
        SelectedPlayedStatus = CompletionStatusChoices.FirstOrDefault(item => item.Id == playedStatus) ??
            CompletionStatusChoices[0];
    }

    private void AddItem()
    {
        if (SelectedSection == null)
        {
            return;
        }
        var result = dialogs()?.ShowInput("Enter a name for the new field value.", "Add library field", string.Empty);
        if (result?.Result == true)
        {
            AddItem(SelectedSection, result.SelectedString, true);
        }
    }

    private DatabaseFieldItem AddItem(DatabaseFieldSection section, string name, bool reportDuplicate)
    {
        name = name?.Trim();
        if (section?.CanAdd != true || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        var existing = section.Items.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing != null)
        {
            SelectedSection = section;
            SelectedItem = existing;
            if (reportDuplicate)
            {
                showMessage($"'{name}' already exists in {section.Title}.", true);
            }
            return existing;
        }
        var model = CreateModel(section.Kind, name);
        var row = new DatabaseFieldItem(model);
        var insertAt = section.Items.TakeWhile(item =>
            StringComparer.CurrentCultureIgnoreCase.Compare(item.Name, name) < 0).Count();
        section.Items.Insert(insertAt, row);
        SelectedSection = section;
        SelectedItem = row;
        if (section.Kind == DatabaseFieldKind.CompletionStatuses)
        {
            LoadCompletionStatusChoices(SelectedDefaultStatus?.Id, SelectedPlayedStatus?.Id);
        }
        ApplySearch();
        StatusText = $"Added '{name}' to {section.Title}.";
        return row;
    }

    private void RenameItem()
    {
        if (SelectedSection == null || SelectedItem == null)
        {
            return;
        }
        var result = dialogs()?.ShowInput("Enter the new field value name.", "Rename library field", SelectedItem.Name);
        if (result?.Result != true)
        {
            return;
        }
        var name = result.SelectedString?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }
        if (SelectedSection.Items.Any(item => item != SelectedItem &&
            string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            showMessage($"'{name}' already exists in {SelectedSection.Title}.", true);
            return;
        }
        SelectedItem.Name = name;
        if (IsCompletionStatusSection)
        {
            LoadCompletionStatusChoices(SelectedDefaultStatus?.Id, SelectedPlayedStatus?.Id);
        }
        StatusText = $"Renamed the field value to '{name}'.";
    }

    private void RemoveItem()
    {
        if (SelectedSection == null || SelectedItem == null)
        {
            return;
        }
        var index = SelectedSection.Items.IndexOf(SelectedItem);
        var removedName = SelectedItem.Name;
        SelectedSection.Items.Remove(SelectedItem);
        SelectedItem = SelectedSection.Items.Count == 0
            ? null
            : SelectedSection.Items[Math.Min(index, SelectedSection.Items.Count - 1)];
        if (IsCompletionStatusSection)
        {
            LoadCompletionStatusChoices(SelectedDefaultStatus?.Id, SelectedPlayedStatus?.Id);
        }
        StatusText = $"Removed '{removedName}' from the pending configuration.";
    }

    private void RemoveUnused()
    {
        if (SelectedSection == null)
        {
            return;
        }
        var unused = GetUnused(SelectedSection);
        if (unused.Count == 0)
        {
            showMessage("No unused field values were found.", false);
            return;
        }
        var result = dialogs()?.ShowMessage(
            $"Remove {unused.Count:N0} unused value(s) from {SelectedSection.Title}?",
            "Remove unused library fields",
            new[] { "Remove", "Cancel" },
            1,
            1);
        if (result == "Remove")
        {
            RemoveUnused(SelectedSection, true);
        }
    }

    private int RemoveUnused(DatabaseFieldSection section, bool updateStatus)
    {
        if (section?.CanRemoveUnused != true)
        {
            return 0;
        }
        var unused = GetUnused(section);
        foreach (var item in unused)
        {
            section.Items.Remove(item);
        }
        if (section.Kind == DatabaseFieldKind.CompletionStatuses)
        {
            LoadCompletionStatusChoices(SelectedDefaultStatus?.Id, SelectedPlayedStatus?.Id);
        }
        if (updateStatus)
        {
            StatusText = $"Removed {unused.Count:N0} unused value(s) from {section.Title}.";
        }
        SelectedItem = section.Items.FirstOrDefault();
        return unused.Count;
    }

    private List<DatabaseFieldItem> GetUnused(DatabaseFieldSection section)
    {
        var used = new HashSet<Guid>();
        foreach (var game in database.Games)
        {
            foreach (var id in GetGameFieldIds(game, section.Kind))
            {
                if (id != Guid.Empty)
                {
                    used.Add(id);
                }
            }
        }
        if (section.Kind == DatabaseFieldKind.Platforms)
        {
            foreach (var profile in database.Emulators.SelectMany(emulator =>
                         emulator.CustomProfiles ?? new ObservableCollection<CustomEmulatorProfile>()))
            {
                foreach (var id in profile.Platforms ?? new List<Guid>())
                {
                    used.Add(id);
                }
            }
        }
        return section.Items.Where(item => !used.Contains(item.Model.Id)).ToList();
    }

    private static IEnumerable<Guid> GetGameFieldIds(Game game, DatabaseFieldKind kind) => kind switch
    {
        DatabaseFieldKind.Platforms => game.PlatformIds ?? new List<Guid>(),
        DatabaseFieldKind.Categories => game.CategoryIds ?? new List<Guid>(),
        DatabaseFieldKind.Genres => game.GenreIds ?? new List<Guid>(),
        DatabaseFieldKind.Companies => (game.DeveloperIds ?? new List<Guid>())
            .Concat(game.PublisherIds ?? new List<Guid>()),
        DatabaseFieldKind.Features => game.FeatureIds ?? new List<Guid>(),
        DatabaseFieldKind.Tags => game.TagIds ?? new List<Guid>(),
        DatabaseFieldKind.Series => game.SeriesIds ?? new List<Guid>(),
        DatabaseFieldKind.AgeRatings => game.AgeRatingIds ?? new List<Guid>(),
        DatabaseFieldKind.Regions => game.RegionIds ?? new List<Guid>(),
        DatabaseFieldKind.Sources => game.SourceId == Guid.Empty ? Array.Empty<Guid>() : new[] { game.SourceId },
        DatabaseFieldKind.CompletionStatuses => game.CompletionStatusId == Guid.Empty
            ? Array.Empty<Guid>()
            : new[] { game.CompletionStatusId },
        _ => Array.Empty<Guid>()
    };

    private void MoveSelected(int offset)
    {
        if (!CanMoveSelected(offset))
        {
            return;
        }
        var index = SelectedSection.Items.IndexOf(SelectedItem);
        SelectedSection.Items.Move(index, index + offset);
        RaiseCommandStates();
    }

    private bool CanMoveSelected(int offset)
    {
        if (!IsFilterPresetSection || SelectedItem == null)
        {
            return false;
        }
        var index = SelectedSection.Items.IndexOf(SelectedItem);
        return index >= 0 && index + offset >= 0 && index + offset < SelectedSection.Items.Count;
    }

    private void SelectPlatformMedia(string field)
    {
        if (SelectedPlatform == null)
        {
            return;
        }
        var selected = dialogs()?.SelectFiles(
            "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp;*.gif;*.ico|All files|*.*",
            false).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(selected))
        {
            return;
        }
        SetPlatformMedia(field, selected);
    }

    private void RemovePlatformMedia(string field) => SetPlatformMedia(field, null);

    private void SetPlatformMedia(string field, string value)
    {
        if (SelectedPlatform == null)
        {
            return;
        }
        switch (field)
        {
            case "Icon":
                SelectedPlatform.Icon = value;
                OnPropertyChanged(nameof(SelectedPlatformIconPreview));
                break;
            case "Cover":
                SelectedPlatform.Cover = value;
                OnPropertyChanged(nameof(SelectedPlatformCoverPreview));
                break;
            case "Background":
                SelectedPlatform.Background = value;
                OnPropertyChanged(nameof(SelectedPlatformBackgroundPreview));
                break;
        }
    }

    private void PreparePlatformMedia()
    {
        foreach (var platform in Models<Platform>(DatabaseFieldKind.Platforms))
        {
            platform.Icon = ImportMedia(platform.Icon, platform.Id);
            platform.Cover = ImportMedia(platform.Cover, platform.Id);
            platform.Background = ImportMedia(platform.Background, platform.Id);
        }
    }

    private string ImportMedia(string path, Guid parentId) =>
        !string.IsNullOrWhiteSpace(path) && File.Exists(path)
            ? database.AddFile(path, parentId, true, CancellationToken.None)
            : path;

    private string ResolveMediaPath(string path) => string.IsNullOrWhiteSpace(path)
        ? null
        : Path.IsPathFullyQualified(path) ? path : database.GetFullFilePath(path);

    private void SynchronizeSpecificationSelections()
    {
        selectedPlatformSpecification = PlatformSpecifications.FirstOrDefault(item =>
            string.Equals(item.Id, SelectedPlatform?.SpecificationId, StringComparison.Ordinal));
        selectedRegionSpecification = RegionSpecifications.FirstOrDefault(item =>
            string.Equals(item.Id, SelectedRegion?.SpecificationId, StringComparison.Ordinal));
        OnPropertyChanged(nameof(SelectedPlatformSpecification));
        OnPropertyChanged(nameof(SelectedRegionSpecification));
    }

    private void ApplySearch()
    {
        foreach (var item in SelectedSection?.Items ?? new ObservableCollection<DatabaseFieldItem>())
        {
            item.IsVisible = string.IsNullOrWhiteSpace(SearchText) ||
                (item.Name?.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase) ?? false);
        }
    }

    private bool Validate(out string error)
    {
        foreach (var section in Sections)
        {
            if (section.Items.Any(item => string.IsNullOrWhiteSpace(item.Name)))
            {
                error = $"Every value in {section.Title} must have a name.";
                return false;
            }
        }
        error = null;
        return true;
    }

    private static DatabaseObject CreateModel(DatabaseFieldKind kind, string name) => kind switch
    {
        DatabaseFieldKind.Platforms => new Platform(name),
        DatabaseFieldKind.Categories => new Category(name),
        DatabaseFieldKind.Genres => new Genre(name),
        DatabaseFieldKind.Companies => new Company(name),
        DatabaseFieldKind.Features => new GameFeature(name),
        DatabaseFieldKind.Tags => new Tag(name),
        DatabaseFieldKind.Series => new Series(name),
        DatabaseFieldKind.AgeRatings => new AgeRating(name),
        DatabaseFieldKind.Regions => new Region(name),
        DatabaseFieldKind.Sources => new GameSource(name),
        DatabaseFieldKind.CompletionStatuses => new CompletionStatus(name),
        _ => throw new NotSupportedException($"New values are not supported for {kind}.")
    };

    private List<T> Models<T>(DatabaseFieldKind kind) where T : DatabaseObject => Sections
        .First(section => section.Kind == kind).Items.Select(item => (T)item.Model).ToList();

    private static void SynchronizeCollection<T>(IItemCollection<T> destination, IReadOnlyList<T> source)
        where T : DatabaseObject
    {
        var sourceIds = source.Select(item => item.Id).ToHashSet();
        var removed = destination.Where(item => !sourceIds.Contains(item.Id)).ToList();
        if (removed.Count > 0)
        {
            destination.Remove(removed);
        }
        var added = source.Where(item => destination[item.Id] == null).ToList();
        if (added.Count > 0)
        {
            destination.Add(added);
        }
        foreach (var item in source)
        {
            var saved = destination[item.Id];
            if (saved != null && !item.IsEqualJson(saved))
            {
                destination.Update(item);
            }
        }
    }

    private string CaptureState() => string.Join("\n", Sections.Select(section =>
        $"{section.Kind}:{Serialization.ToJson(section.Items.Select(item => item.Model).ToList())}")) +
        $"\nDefault:{SelectedDefaultStatus?.Id}\nPlayed:{SelectedPlayedStatus?.Id}";

    private bool HasChanges() => originalState != null &&
        !string.Equals(originalState, CaptureState(), StringComparison.Ordinal);

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)AddCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)RenameCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)RemoveCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)RemoveUnusedCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)MoveUpCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)MoveDownCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)SelectPlatformMediaCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)RemovePlatformMediaCommand).RaiseCanExecuteChanged();
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

public enum DatabaseFieldKind
{
    Platforms,
    Categories,
    Genres,
    Companies,
    Features,
    Tags,
    Series,
    AgeRatings,
    Regions,
    Sources,
    CompletionStatuses,
    FilterPresets
}

public sealed record DatabaseFieldSection(
    DatabaseFieldKind Kind,
    string Title,
    ObservableCollection<DatabaseFieldItem> Items,
    bool CanAdd,
    bool CanRemoveUnused);

public sealed class DatabaseFieldItem : INotifyPropertyChanged
{
    private bool isVisible = true;

    public event PropertyChangedEventHandler PropertyChanged;
    public DatabaseObject Model { get; }
    public string Name
    {
        get => Model.Name;
        set
        {
            if (string.Equals(Model.Name, value, StringComparison.Ordinal))
            {
                return;
            }
            Model.Name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }
    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible == value)
            {
                return;
            }
            isVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public DatabaseFieldItem(DatabaseObject model) => Model = model;
    public override string ToString() => Name;
}

public sealed record CompletionStatusChoice(Guid Id, string Name)
{
    public override string ToString() => Name;
}

public sealed record EmulationSpecificationOption(string Id, string Name)
{
    public override string ToString() => Name;
}
