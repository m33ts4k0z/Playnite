using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Database;
using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using SdkEnumFilter = Playnite.SDK.Models.EnumFilterItemProperties;
using SdkIdFilter = Playnite.SDK.Models.IdItemFilterItemProperties;
using SdkStringFilter = Playnite.SDK.Models.StringFilterItemProperties;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenFilterViewModel : INotifyPropertyChanged
{
    private readonly GameDatabase database;
    private readonly IReadOnlyList<GameItemViewModel> games;
    private readonly FullscreenSettings settings;
    private readonly Action changed;
    private readonly Action settingsChanged;
    private FilterPresetSettings working = new();
    private FullscreenDialogService dialogs;
    private bool suppressChanges;
    private FilterPreset selectedPreset;
    private SortOrder selectedSortOrder;
    private SortOrderDirection selectedSortDirection;

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<FilterPreset> Presets { get; } = new();
    public IReadOnlyList<FullscreenFilterGroupViewModel> Groups { get; private set; } =
        Array.Empty<FullscreenFilterGroupViewModel>();
    public IReadOnlyList<SortOrder> SortOptions { get; } = Enum.GetValues<SortOrder>();
    public IReadOnlyList<SortOrderDirection> SortDirectionOptions { get; } =
        Enum.GetValues<SortOrderDirection>();
    public int FieldCount => 29;
    public FilterPresetSettings Settings => working;

    public bool Matches(Game game, bool includeHidden)
    {
        if (database == null)
        {
            return true;
        }

        if (working.Hidden || !includeHidden)
        {
            return database.GetGameMatchesFilter(game, working, true);
        }

        if (database.GetGameMatchesFilter(game, working, true))
        {
            return true;
        }

        var hiddenFilter = Clone(working);
        hiddenFilter.Hidden = true;
        return database.GetGameMatchesFilter(game, hiddenFilter, true);
    }

    public FilterPreset SelectedPreset
    {
        get => selectedPreset;
        set
        {
            if (ReferenceEquals(selectedPreset, value))
            {
                return;
            }

            selectedPreset = value;
            settings.ActiveFilterPreset = value?.Id ?? Guid.Empty;
            OnPropertyChanged();
            Load(value?.Settings ?? new FilterPresetSettings());
            if (value?.SortingOrder is SortOrder order)
            {
                SelectedSortOrder = order;
            }

            if (value?.SortingOrderDirection is SortOrderDirection direction)
            {
                SelectedSortDirection = direction;
            }

            NotifyChanged();
        }
    }

    public SortOrder SelectedSortOrder
    {
        get => selectedSortOrder;
        set
        {
            if (SetField(ref selectedSortOrder, value))
            {
                settings.SortingOrder = value;
                NotifyChanged();
            }
        }
    }

    public SortOrderDirection SelectedSortDirection
    {
        get => selectedSortDirection;
        set
        {
            if (SetField(ref selectedSortDirection, value))
            {
                settings.SortingDirection = value;
                NotifyChanged();
            }
        }
    }

    public string Name
    {
        get => working.Name ?? string.Empty;
        set => SetText(working.Name, value, resolved => working.Name = resolved);
    }

    public string Version
    {
        get => working.Version ?? string.Empty;
        set => SetText(working.Version, value, resolved => working.Version = resolved);
    }

    public bool UseAndStyle
    {
        get => working.UseAndFilteringStyle;
        set => SetBoolean(working.UseAndFilteringStyle, value, () => working.UseAndFilteringStyle = value);
    }

    public bool Installed
    {
        get => working.IsInstalled;
        set => SetBoolean(working.IsInstalled, value, () => working.IsInstalled = value);
    }

    public bool Uninstalled
    {
        get => working.IsUnInstalled;
        set => SetBoolean(working.IsUnInstalled, value, () => working.IsUnInstalled = value);
    }

    public bool Hidden
    {
        get => working.Hidden;
        set => SetBoolean(working.Hidden, value, () => working.Hidden = value);
    }

    public bool Favorite
    {
        get => working.Favorite;
        set => SetBoolean(working.Favorite, value, () => working.Favorite = value);
    }

    public ICommand ClearCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }

    public FullscreenFilterViewModel(
        GameDatabase database,
        IReadOnlyList<GameItemViewModel> games,
        FullscreenSettings settings,
        Action changed,
        Action settingsChanged)
    {
        this.database = database;
        this.games = games ?? Array.Empty<GameItemViewModel>();
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.changed = changed ?? throw new ArgumentNullException(nameof(changed));
        this.settingsChanged = settingsChanged ?? throw new ArgumentNullException(nameof(settingsChanged));
        selectedSortOrder = settings.SortingOrder;
        selectedSortDirection = settings.SortingDirection;
        BuildGroups();
        RefreshPresets();
        selectedPreset = Presets.FirstOrDefault(item => item.Id == settings.ActiveFilterPreset);
        Load(selectedPreset?.Settings ?? new FilterPresetSettings());
        ClearCommand = new RelayCommand(Clear);
        SaveCommand = new RelayCommand(Save, () => database != null);
        RenameCommand = new RelayCommand(Rename, () => SelectedPreset != null);
        DeleteCommand = new RelayCommand(Delete, () => SelectedPreset != null);
    }

    internal void AttachDialogs(FullscreenDialogService service) => dialogs = service;

    public void CycleQuickPreset(int offset)
    {
        var quick = Presets.Where(item => item.ShowInFullscreeQuickSelection).ToList();
        if (quick.Count == 0)
        {
            return;
        }

        var current = SelectedPreset == null ? -1 : quick.FindIndex(item => item.Id == SelectedPreset.Id);
        var target = current < 0
            ? offset >= 0 ? 0 : quick.Count - 1
            : (current + offset + quick.Count) % quick.Count;
        SelectedPreset = quick[target];
    }

    internal void ApplyForTest(FilterPresetSettings filter)
    {
        selectedPreset = null;
        OnPropertyChanged(nameof(SelectedPreset));
        Load(filter);
        NotifyChanged();
    }

    internal FilterPreset SaveForTest(string name)
    {
        var preset = SavePreset(name, true);
        return preset;
    }

    internal void RenameForTest(FilterPreset preset, string name)
    {
        preset.Name = name;
        database.FilterPresets.Update(preset);
        RefreshPresets(preset.Id);
    }

    internal void DeleteForTest(FilterPreset preset)
    {
        database.FilterPresets.Remove(preset);
        RefreshPresets();
        Clear();
    }

    private void Save()
    {
        if (dialogs == null)
        {
            return;
        }

        var toggles = new List<MessageBoxToggle>
        {
            new("Show in quick selection", true)
        };
        var result = dialogs.ShowInput(
            "Enter a name for this filter preset.",
            "Save filter preset",
            SelectedPreset?.Name ?? string.Empty,
            toggles);
        if (!result.Result || string.IsNullOrWhiteSpace(result.SelectedString))
        {
            return;
        }

        var name = result.SelectedString.Trim();
        var existing = Presets.FirstOrDefault(item =>
            string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase));
        if (existing != null && dialogs.ShowMessage(
            "A preset with this name already exists.",
            "Save filter preset",
            new[] { "Overwrite", "Cancel" },
            1,
            1) != "Overwrite")
        {
            return;
        }

        SavePreset(name, toggles[0].Selected, existing);
    }

    private FilterPreset SavePreset(string name, bool quick, FilterPreset existing = null)
    {
        var preset = existing ?? new FilterPreset();
        preset.Name = name;
        preset.Settings = Clone(working);
        preset.ShowInFullscreeQuickSelection = quick;
        preset.SortingOrder = SelectedSortOrder;
        preset.SortingOrderDirection = SelectedSortDirection;
        if (existing == null)
        {
            database.FilterPresets.Add(preset);
        }
        else
        {
            database.FilterPresets.Update(preset);
        }

        RefreshPresets(preset.Id);
        settingsChanged();
        return preset;
    }

    private void Rename()
    {
        if (SelectedPreset == null || dialogs == null)
        {
            return;
        }

        var toggles = new List<MessageBoxToggle>
        {
            new("Show in quick selection", SelectedPreset.ShowInFullscreeQuickSelection)
        };
        var result = dialogs.ShowInput(
            "Enter a new name for this filter preset.",
            "Rename filter preset",
            SelectedPreset.Name,
            toggles);
        if (!result.Result || string.IsNullOrWhiteSpace(result.SelectedString))
        {
            return;
        }

        var name = result.SelectedString.Trim();
        if (Presets.Any(item => item.Id != SelectedPreset.Id &&
            string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            dialogs.ShowMessage(
                "A preset with this name already exists.",
                "Rename filter preset",
                new[] { "OK" });
            return;
        }

        SelectedPreset.Name = name;
        SelectedPreset.ShowInFullscreeQuickSelection = toggles[0].Selected;
        database.FilterPresets.Update(SelectedPreset);
        RefreshPresets(SelectedPreset.Id);
        settingsChanged();
    }

    private void Delete()
    {
        var preset = SelectedPreset;
        if (preset == null || dialogs == null || dialogs.ShowMessage(
            $"Remove filter preset {preset.Name}?",
            "Remove filter preset",
            new[] { "Yes", "No" },
            1,
            1) != "Yes")
        {
            return;
        }

        database.FilterPresets.Remove(preset);
        RefreshPresets();
        Clear();
    }

    private void Clear()
    {
        selectedPreset = null;
        settings.ActiveFilterPreset = Guid.Empty;
        OnPropertyChanged(nameof(SelectedPreset));
        Load(new FilterPresetSettings());
        NotifyChanged();
    }

    private void RefreshPresets(Guid? selectedId = null)
    {
        Presets.Clear();
        if (database != null)
        {
            foreach (var preset in database.GetSortedFilterPresets())
            {
                Presets.Add(preset);
            }
        }

        if (selectedId.HasValue)
        {
            selectedPreset = Presets.FirstOrDefault(item => item.Id == selectedId.Value);
            settings.ActiveFilterPreset = selectedPreset?.Id ?? Guid.Empty;
            Load(selectedPreset?.Settings ?? new FilterPresetSettings());
            OnPropertyChanged(nameof(SelectedPreset));
            changed();
        }

        (RenameCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void BuildGroups()
    {
        if (database == null)
        {
            return;
        }

        Groups = new List<FullscreenFilterGroupViewModel>
        {
            CreateIdGroup(nameof(FilterPresetSettings.Platform), "Platforms", database.Platforms, game => game.PlatformIds),
            CreateLibraryGroup(),
            CreateIdGroup(nameof(FilterPresetSettings.Genre), "Genres", database.Genres, game => game.GenreIds),
            CreateReleaseYearGroup(),
            CreateIdGroup(nameof(FilterPresetSettings.Developer), "Developers", database.Companies, game => game.DeveloperIds),
            CreateIdGroup(nameof(FilterPresetSettings.Publisher), "Publishers", database.Companies, game => game.PublisherIds),
            CreateIdGroup(nameof(FilterPresetSettings.Category), "Categories", database.Categories, game => game.CategoryIds),
            CreateIdGroup(nameof(FilterPresetSettings.Tag), "Tags", database.Tags, game => game.TagIds),
            CreateIdGroup(nameof(FilterPresetSettings.Feature), "Features", database.Features, game => game.FeatureIds),
            CreateEnumGroup<PlaytimeCategory>(nameof(FilterPresetSettings.PlayTime), "Playtime", game => game.PlaytimeCategory),
            CreateEnumGroup<InstallSizeGroup>(nameof(FilterPresetSettings.InstallSize), "Install size", game => game.InstallSizeGroup),
            CreateIdGroup(nameof(FilterPresetSettings.CompletionStatuses), "Completion status", database.CompletionStatuses,
                game => game.CompletionStatusId == Guid.Empty ? null : new[] { game.CompletionStatusId }),
            CreateIdGroup(nameof(FilterPresetSettings.Series), "Series", database.Series, game => game.SeriesIds),
            CreateIdGroup(nameof(FilterPresetSettings.Region), "Regions", database.Regions, game => game.RegionIds),
            CreateIdGroup(nameof(FilterPresetSettings.Source), "Sources", database.Sources,
                game => game.SourceId == Guid.Empty ? null : new[] { game.SourceId }),
            CreateIdGroup(nameof(FilterPresetSettings.AgeRating), "Age ratings", database.AgeRatings, game => game.AgeRatingIds),
            CreateEnumGroup<ScoreGroup>(nameof(FilterPresetSettings.UserScore), "User score", game => game.UserScoreGroup),
            CreateEnumGroup<ScoreGroup>(nameof(FilterPresetSettings.CommunityScore), "Community score", game => game.CommunityScoreGroup),
            CreateEnumGroup<ScoreGroup>(nameof(FilterPresetSettings.CriticScore), "Critic score", game => game.CriticScoreGroup),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.LastActivity), "Last activity", game => game.LastActivitySegment),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.RecentActivity), "Recent activity", game => game.RecentActivitySegment),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.Added), "Added", game => game.AddedSegment),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.Modified), "Modified", game => game.ModifiedSegment)
        };
        OnPropertyChanged(nameof(Groups));
    }

    private FullscreenFilterGroupViewModel CreateIdGroup(
        string field,
        string title,
        IEnumerable<DatabaseObject> values,
        Func<Game, IEnumerable<Guid>> getIds)
    {
        var group = new FullscreenFilterGroupViewModel(field, title, FullscreenFilterValueKind.Id, GroupChanged);
        var normalized = games.Select(item => getIds(item.Game)?.ToHashSet() ?? new HashSet<Guid>()).ToList();
        group.Add("None", Guid.Empty, normalized.Count(ids => ids.Count == 0));
        foreach (var value in values.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            group.Add(value.Name, value.Id, normalized.Count(ids => ids.Contains(value.Id)));
        }

        return group;
    }

    private FullscreenFilterGroupViewModel CreateLibraryGroup()
    {
        var group = new FullscreenFilterGroupViewModel(
            nameof(FilterPresetSettings.Library), "Libraries", FullscreenFilterValueKind.Id, GroupChanged);
        foreach (var item in games.GroupBy(game => game.Game.PluginId).OrderBy(item => item.Key))
        {
            group.Add(item.Key == Guid.Empty ? "Playnite" : item.Key.ToString(), item.Key, item.Count());
        }

        return group;
    }

    private FullscreenFilterGroupViewModel CreateReleaseYearGroup()
    {
        var group = new FullscreenFilterGroupViewModel(
            nameof(FilterPresetSettings.ReleaseYear), "Release year", FullscreenFilterValueKind.String, GroupChanged);
        group.Add("None", Playnite.FilterSettings.MissingFieldString, games.Count(item => !item.Game.ReleaseDate.HasValue));
        foreach (var item in games.Where(game => game.Game.ReleaseDate.HasValue)
            .GroupBy(game => game.Game.ReleaseYear.ToString()).OrderByDescending(item => item.Key))
        {
            group.Add(item.Key, item.Key, item.Count());
        }

        return group;
    }

    private FullscreenFilterGroupViewModel CreateEnumGroup<TEnum>(
        string field,
        string title,
        Func<Game, TEnum> getValue) where TEnum : struct, Enum
    {
        var group = new FullscreenFilterGroupViewModel(field, title, FullscreenFilterValueKind.Enum, GroupChanged);
        foreach (var value in Enum.GetValues<TEnum>())
        {
            group.Add(GetEnumName(value), Convert.ToInt32(value),
                games.Count(game => EqualityComparer<TEnum>.Default.Equals(getValue(game.Game), value)));
        }

        return group;
    }

    private static string GetEnumName<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var description = typeof(TEnum).GetField(name)?.GetCustomAttribute<DescriptionAttribute>()?.Description;
        return string.IsNullOrWhiteSpace(description) || description.StartsWith("LOC", StringComparison.Ordinal)
            ? name
            : description;
    }

    private void GroupChanged(FullscreenFilterGroupViewModel group)
    {
        if (suppressChanges)
        {
            return;
        }

        var selected = group.Options.Where(option => option.IsSelected).Select(option => option.Value).ToList();
        if (group.ValueKind == FullscreenFilterValueKind.Id)
        {
            SetIdFilter(group.Field, selected.Cast<Guid>().ToList());
        }
        else if (group.ValueKind == FullscreenFilterValueKind.String)
        {
            working.ReleaseYear = selected.Count == 0 ? null : new SdkStringFilter(selected.Cast<string>().ToList());
        }
        else
        {
            SetEnumFilter(group.Field, selected.Cast<int>().ToList());
        }

        NotifyChanged();
    }

    private void SetIdFilter(string field, List<Guid> values)
    {
        var filter = values.Count == 0 ? null : new SdkIdFilter(values);
        switch (field)
        {
            case nameof(FilterPresetSettings.Genre): working.Genre = filter; break;
            case nameof(FilterPresetSettings.Platform): working.Platform = filter; break;
            case nameof(FilterPresetSettings.Publisher): working.Publisher = filter; break;
            case nameof(FilterPresetSettings.Developer): working.Developer = filter; break;
            case nameof(FilterPresetSettings.Category): working.Category = filter; break;
            case nameof(FilterPresetSettings.Tag): working.Tag = filter; break;
            case nameof(FilterPresetSettings.Series): working.Series = filter; break;
            case nameof(FilterPresetSettings.Region): working.Region = filter; break;
            case nameof(FilterPresetSettings.Source): working.Source = filter; break;
            case nameof(FilterPresetSettings.AgeRating): working.AgeRating = filter; break;
            case nameof(FilterPresetSettings.Library): working.Library = filter; break;
            case nameof(FilterPresetSettings.CompletionStatuses): working.CompletionStatuses = filter; break;
            case nameof(FilterPresetSettings.Feature): working.Feature = filter; break;
            default: throw new InvalidOperationException($"Unsupported filter field {field}.");
        }
    }

    private void SetEnumFilter(string field, List<int> values)
    {
        var filter = values.Count == 0 ? null : new SdkEnumFilter(values);
        switch (field)
        {
            case nameof(FilterPresetSettings.UserScore): working.UserScore = filter; break;
            case nameof(FilterPresetSettings.CriticScore): working.CriticScore = filter; break;
            case nameof(FilterPresetSettings.CommunityScore): working.CommunityScore = filter; break;
            case nameof(FilterPresetSettings.LastActivity): working.LastActivity = filter; break;
            case nameof(FilterPresetSettings.RecentActivity): working.RecentActivity = filter; break;
            case nameof(FilterPresetSettings.Added): working.Added = filter; break;
            case nameof(FilterPresetSettings.Modified): working.Modified = filter; break;
            case nameof(FilterPresetSettings.PlayTime): working.PlayTime = filter; break;
            case nameof(FilterPresetSettings.InstallSize): working.InstallSize = filter; break;
            default: throw new InvalidOperationException($"Unsupported filter field {field}.");
        }
    }

    private void Load(FilterPresetSettings source)
    {
        working = Clone(source);
        suppressChanges = true;
        try
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Version));
            OnPropertyChanged(nameof(UseAndStyle));
            OnPropertyChanged(nameof(Installed));
            OnPropertyChanged(nameof(Uninstalled));
            OnPropertyChanged(nameof(Hidden));
            OnPropertyChanged(nameof(Favorite));
            foreach (var group in Groups)
            {
                group.SetSelected(GetSelected(group));
            }
        }
        finally
        {
            suppressChanges = false;
        }
    }

    private IEnumerable<object> GetSelected(FullscreenFilterGroupViewModel group)
    {
        if (group.ValueKind == FullscreenFilterValueKind.String)
        {
            return working.ReleaseYear?.Values?.Cast<object>() ?? Array.Empty<object>();
        }

        if (group.ValueKind == FullscreenFilterValueKind.Enum)
        {
            var filter = group.Field switch
            {
                nameof(FilterPresetSettings.UserScore) => working.UserScore,
                nameof(FilterPresetSettings.CriticScore) => working.CriticScore,
                nameof(FilterPresetSettings.CommunityScore) => working.CommunityScore,
                nameof(FilterPresetSettings.LastActivity) => working.LastActivity,
                nameof(FilterPresetSettings.RecentActivity) => working.RecentActivity,
                nameof(FilterPresetSettings.Added) => working.Added,
                nameof(FilterPresetSettings.Modified) => working.Modified,
                nameof(FilterPresetSettings.PlayTime) => working.PlayTime,
                nameof(FilterPresetSettings.InstallSize) => working.InstallSize,
                _ => null
            };
            return filter?.Values?.Cast<object>() ?? Array.Empty<object>();
        }

        var idFilter = group.Field switch
        {
            nameof(FilterPresetSettings.Genre) => working.Genre,
            nameof(FilterPresetSettings.Platform) => working.Platform,
            nameof(FilterPresetSettings.Publisher) => working.Publisher,
            nameof(FilterPresetSettings.Developer) => working.Developer,
            nameof(FilterPresetSettings.Category) => working.Category,
            nameof(FilterPresetSettings.Tag) => working.Tag,
            nameof(FilterPresetSettings.Series) => working.Series,
            nameof(FilterPresetSettings.Region) => working.Region,
            nameof(FilterPresetSettings.Source) => working.Source,
            nameof(FilterPresetSettings.AgeRating) => working.AgeRating,
            nameof(FilterPresetSettings.Library) => working.Library,
            nameof(FilterPresetSettings.CompletionStatuses) => working.CompletionStatuses,
            nameof(FilterPresetSettings.Feature) => working.Feature,
            _ => null
        };
        return idFilter?.Ids?.Cast<object>() ?? Array.Empty<object>();
    }

    private void SetText(string current, string value, Action<string> apply, [CallerMemberName] string property = null)
    {
        var resolved = string.IsNullOrWhiteSpace(value) ? null : value;
        if (string.Equals(current, resolved, StringComparison.Ordinal))
        {
            return;
        }

        apply(resolved);
        OnPropertyChanged(property);
        NotifyChanged();
    }

    private void SetBoolean(bool current, bool value, Action apply, [CallerMemberName] string property = null)
    {
        if (current == value)
        {
            return;
        }

        apply();
        OnPropertyChanged(property);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (!suppressChanges)
        {
            changed();
            settingsChanged();
        }
    }

    private static FilterPresetSettings Clone(FilterPresetSettings source)
    {
        source ??= new FilterPresetSettings();
        return new FilterPresetSettings
        {
            UseAndFilteringStyle = source.UseAndFilteringStyle,
            IsInstalled = source.IsInstalled,
            IsUnInstalled = source.IsUnInstalled,
            Hidden = source.Hidden,
            Favorite = source.Favorite,
            Name = source.Name,
            Version = source.Version,
            ReleaseYear = Clone(source.ReleaseYear),
            Genre = Clone(source.Genre),
            Platform = Clone(source.Platform),
            Publisher = Clone(source.Publisher),
            Developer = Clone(source.Developer),
            Category = Clone(source.Category),
            Tag = Clone(source.Tag),
            Series = Clone(source.Series),
            Region = Clone(source.Region),
            Source = Clone(source.Source),
            AgeRating = Clone(source.AgeRating),
            Library = Clone(source.Library),
            CompletionStatuses = Clone(source.CompletionStatuses),
            Feature = Clone(source.Feature),
            UserScore = Clone(source.UserScore),
            CriticScore = Clone(source.CriticScore),
            CommunityScore = Clone(source.CommunityScore),
            LastActivity = Clone(source.LastActivity),
            RecentActivity = Clone(source.RecentActivity),
            Added = Clone(source.Added),
            Modified = Clone(source.Modified),
            PlayTime = Clone(source.PlayTime),
            InstallSize = Clone(source.InstallSize)
        };
    }

    private static SdkIdFilter Clone(SdkIdFilter value) => value == null
        ? null
        : new SdkIdFilter(value.Ids?.ToList()) { Text = value.Text };

    private static SdkStringFilter Clone(SdkStringFilter value) => value == null
        ? null
        : new SdkStringFilter(value.Values?.ToList());

    private static SdkEnumFilter Clone(SdkEnumFilter value) => value == null
        ? null
        : new SdkEnumFilter(value.Values?.ToList());

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(property);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}

public enum FullscreenFilterValueKind
{
    Id,
    String,
    Enum
}

public sealed class FullscreenFilterGroupViewModel : INotifyPropertyChanged
{
    private readonly Action<FullscreenFilterGroupViewModel> changed;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Field { get; }
    public string Title { get; }
    public FullscreenFilterValueKind ValueKind { get; }
    public ObservableCollection<FullscreenFilterOptionViewModel> Options { get; } = new();
    public int SelectedCount => Options.Count(item => item.IsSelected);
    public string DisplayTitle => SelectedCount == 0 ? Title : $"{Title} · {SelectedCount:N0}";

    public FullscreenFilterGroupViewModel(
        string field,
        string title,
        FullscreenFilterValueKind valueKind,
        Action<FullscreenFilterGroupViewModel> changed)
    {
        Field = field;
        Title = title;
        ValueKind = valueKind;
        this.changed = changed;
    }

    public void Add(string name, object value, int count) =>
        Options.Add(new FullscreenFilterOptionViewModel(name, value, count, OptionChanged));

    internal void SetSelected(IEnumerable<object> values)
    {
        var selected = values?.ToHashSet() ?? new HashSet<object>();
        foreach (var option in Options)
        {
            option.SetSelected(selected.Contains(option.Value), false);
        }

        RaiseSelectionProperties();
    }

    private void OptionChanged()
    {
        RaiseSelectionProperties();
        changed(this);
    }

    private void RaiseSelectionProperties()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayTitle)));
    }
}

public sealed class FullscreenFilterOptionViewModel : INotifyPropertyChanged
{
    private readonly Action changed;
    private bool isSelected;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Name { get; }
    public object Value { get; }
    public int Count { get; }
    public string DisplayText => $"{Name} ({Count:N0})";

    public bool IsSelected
    {
        get => isSelected;
        set => SetSelected(value, true);
    }

    public FullscreenFilterOptionViewModel(string name, object value, int count, Action changed)
    {
        Name = string.IsNullOrWhiteSpace(name) ? "None" : name;
        Value = value;
        Count = count;
        this.changed = changed;
    }

    internal void SetSelected(bool value, bool notify)
    {
        if (isSelected == value)
        {
            return;
        }

        isSelected = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        if (notify)
        {
            changed();
        }
    }
}
