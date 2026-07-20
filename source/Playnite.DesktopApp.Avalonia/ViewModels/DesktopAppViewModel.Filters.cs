using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Input;
using Playnite.SDK;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;
using SdkEnumFilterItemProperties = Playnite.SDK.Models.EnumFilterItemProperties;
using SdkIdItemFilterItemProperties = Playnite.SDK.Models.IdItemFilterItemProperties;
using SdkStringFilterItemProperties = Playnite.SDK.Models.StringFilterItemProperties;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed partial class DesktopAppViewModel
{
    private FilterPresetSettings workingFilterSettings = new();
    private bool suppressFilterControlChanges;
    private bool isFilterPanelVisible;
    private bool isFilterDirty;
    private int liveFilterMatchCount;

    public bool IsFilterPanelVisible
    {
        get => isFilterPanelVisible;
        private set => SetField(ref isFilterPanelVisible, value);
    }

    public bool IsFilterDirty
    {
        get => isFilterDirty;
        private set => SetField(ref isFilterDirty, value);
    }

    public int LiveFilterMatchCount
    {
        get => liveFilterMatchCount;
        private set
        {
            if (SetField(ref liveFilterMatchCount, value))
            {
                OnPropertyChanged(nameof(FilterMatchSummary));
                OnPropertyChanged(nameof(FilterActiveIndicator));
            }
        }
    }

    public bool IsFilterActive => HasActiveFilter(workingFilterSettings);
    public int FilterFieldCount => 29;
    public string FilterMatchSummary =>
        $"{LiveFilterMatchCount:N0} of {allGames.Count:N0} games match";
    public string FilterActiveIndicator => IsFilterActive
        ? $"Filters active · {LiveFilterMatchCount:N0} matches"
        : "No additional filters active";
    public string InstalledFilterLabel =>
        $"{Localize("LOCGameIsInstalledTitle", "Installed")} ({allGames.Count(game => game.Game.IsInstalled):N0})";
    public string UninstalledFilterLabel =>
        $"{Localize("LOCGameIsUnInstalledTitle", "Uninstalled")} ({allGames.Count(game => !game.Game.IsInstalled):N0})";
    public string HiddenFilterLabel =>
        $"{Localize("LOCGameHiddenTitle", "Hidden")} ({allGames.Count(game => game.Game.Hidden):N0})";
    public string FavoriteFilterLabel =>
        $"{Localize("LOCGameFavoriteTitle", "Favorite")} ({allGames.Count(game => game.Game.Favorite):N0})";

    public string FilterName
    {
        get => workingFilterSettings.Name ?? string.Empty;
        set
        {
            var resolved = string.IsNullOrWhiteSpace(value) ? null : value;
            if (!string.Equals(workingFilterSettings.Name, resolved, StringComparison.Ordinal))
            {
                workingFilterSettings.Name = resolved;
                OnPropertyChanged();
                WorkingFilterChanged();
            }
        }
    }

    public string FilterVersion
    {
        get => workingFilterSettings.Version ?? string.Empty;
        set
        {
            var resolved = string.IsNullOrWhiteSpace(value) ? null : value;
            if (!string.Equals(workingFilterSettings.Version, resolved, StringComparison.Ordinal))
            {
                workingFilterSettings.Version = resolved;
                OnPropertyChanged();
                WorkingFilterChanged();
            }
        }
    }

    public bool FilterUseAndStyle
    {
        get => workingFilterSettings.UseAndFilteringStyle;
        set => SetWorkingFilterBoolean(
            workingFilterSettings.UseAndFilteringStyle,
            value,
            () => workingFilterSettings.UseAndFilteringStyle = value,
            nameof(FilterUseAndStyle));
    }

    public bool FilterInstalled
    {
        get => workingFilterSettings.IsInstalled;
        set => SetWorkingFilterBoolean(
            workingFilterSettings.IsInstalled,
            value,
            () => workingFilterSettings.IsInstalled = value,
            nameof(FilterInstalled));
    }

    public bool FilterUninstalled
    {
        get => workingFilterSettings.IsUnInstalled;
        set => SetWorkingFilterBoolean(
            workingFilterSettings.IsUnInstalled,
            value,
            () => workingFilterSettings.IsUnInstalled = value,
            nameof(FilterUninstalled));
    }

    public bool FilterHidden
    {
        get => workingFilterSettings.Hidden;
        set => SetWorkingFilterBoolean(
            workingFilterSettings.Hidden,
            value,
            () => workingFilterSettings.Hidden = value,
            nameof(FilterHidden));
    }

    public bool FilterFavorite
    {
        get => workingFilterSettings.Favorite;
        set => SetWorkingFilterBoolean(
            workingFilterSettings.Favorite,
            value,
            () => workingFilterSettings.Favorite = value,
            nameof(FilterFavorite));
    }

    public IReadOnlyList<DesktopFilterGroupViewModel> FilterGroups { get; private set; } =
        Array.Empty<DesktopFilterGroupViewModel>();

    public ICommand ToggleFilterPanelCommand { get; private set; }
    public ICommand CloseFilterPanelCommand { get; private set; }
    public ICommand ClearFiltersCommand { get; private set; }
    public ICommand SaveFilterPresetCommand { get; private set; }
    public ICommand RenameFilterPresetCommand { get; private set; }
    public ICommand DeleteFilterPresetCommand { get; private set; }

    private void InitializeFilterPanel()
    {
        BuildFilterGroups();
        LoadWorkingFilter(selectedFilterPreset?.Settings ?? new FilterPresetSettings(), false);
        ToggleFilterPanelCommand = new AppRelayCommand(() =>
            IsFilterPanelVisible = !IsFilterPanelVisible);
        CloseFilterPanelCommand = new AppRelayCommand(() => IsFilterPanelVisible = false);
        ClearFiltersCommand = new AppRelayCommand(ClearWorkingFilters, () => IsFilterActive);
        SaveFilterPresetCommand = new AppRelayCommand(SaveCurrentFilterPreset, () => database != null);
        RenameFilterPresetCommand = new AppRelayCommand(
            RenameSelectedFilterPreset,
            () => database != null && SelectedFilterPreset != null);
        DeleteFilterPresetCommand = new AppRelayCommand(
            DeleteSelectedFilterPreset,
            () => database != null && SelectedFilterPreset != null);
        RaiseFilterCommandStates();
    }

    private void BuildFilterGroups()
    {
        if (database == null)
        {
            FilterGroups = Array.Empty<DesktopFilterGroupViewModel>();
            return;
        }

        var groups = new List<DesktopFilterGroupViewModel>
        {
            CreateIdGroup(nameof(FilterPresetSettings.Platform), Localize("LOCPlatformTitle", "Platforms"),
                database.Platforms, game => game.PlatformIds),
            CreateLibraryGroup(),
            CreateIdGroup(nameof(FilterPresetSettings.Genre), Localize("LOCGenreLabel", "Genres"),
                database.Genres, game => game.GenreIds),
            CreateReleaseYearGroup(),
            CreateIdGroup(nameof(FilterPresetSettings.Developer), Localize("LOCDeveloperLabel", "Developers"),
                database.Companies, game => game.DeveloperIds),
            CreateIdGroup(nameof(FilterPresetSettings.Publisher), Localize("LOCPublisherLabel", "Publishers"),
                database.Companies, game => game.PublisherIds),
            CreateIdGroup(nameof(FilterPresetSettings.Category), Localize("LOCCategoryLabel", "Categories"),
                database.Categories, game => game.CategoryIds),
            CreateIdGroup(nameof(FilterPresetSettings.Tag), Localize("LOCTagLabel", "Tags"),
                database.Tags, game => game.TagIds),
            CreateIdGroup(nameof(FilterPresetSettings.Feature), Localize("LOCFeatureLabel", "Features"),
                database.Features, game => game.FeatureIds),
            CreateEnumGroup<PlaytimeCategory>(nameof(FilterPresetSettings.PlayTime),
                Localize("LOCTimePlayed", "Playtime"), game => game.PlaytimeCategory),
            CreateEnumGroup<InstallSizeGroup>(nameof(FilterPresetSettings.InstallSize),
                Localize("LOCInstallSizeLabel", "Install size"), game => game.InstallSizeGroup),
            CreateIdGroup(nameof(FilterPresetSettings.CompletionStatuses),
                Localize("LOCCompletionStatus", "Completion status"), database.CompletionStatuses,
                game => game.CompletionStatusId == Guid.Empty ? null : new[] { game.CompletionStatusId }),
            CreateIdGroup(nameof(FilterPresetSettings.Series), Localize("LOCSeriesLabel", "Series"),
                database.Series, game => game.SeriesIds),
            CreateIdGroup(nameof(FilterPresetSettings.Region), Localize("LOCRegionLabel", "Regions"),
                database.Regions, game => game.RegionIds),
            CreateIdGroup(nameof(FilterPresetSettings.Source), Localize("LOCSourceLabel", "Sources"),
                database.Sources, game => game.SourceId == Guid.Empty ? null : new[] { game.SourceId }),
            CreateIdGroup(nameof(FilterPresetSettings.AgeRating), Localize("LOCAgeRatingLabel", "Age ratings"),
                database.AgeRatings, game => game.AgeRatingIds),
            CreateEnumGroup<ScoreGroup>(nameof(FilterPresetSettings.UserScore),
                Localize("LOCUserScore", "User score"), game => game.UserScoreGroup),
            CreateEnumGroup<ScoreGroup>(nameof(FilterPresetSettings.CommunityScore),
                Localize("LOCCommunityScore", "Community score"), game => game.CommunityScoreGroup),
            CreateEnumGroup<ScoreGroup>(nameof(FilterPresetSettings.CriticScore),
                Localize("LOCCriticScore", "Critic score"), game => game.CriticScoreGroup),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.LastActivity),
                Localize("LOCGameLastActivityTitle", "Last activity"), game => game.LastActivitySegment),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.RecentActivity),
                Localize("LOCRecentActivityLabel", "Recent activity"), game => game.RecentActivitySegment),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.Added),
                Localize("LOCDateAddedLabel", "Date added"), game => game.AddedSegment),
            CreateEnumGroup<PastTimeSegment>(nameof(FilterPresetSettings.Modified),
                Localize("LOCDateModifiedLabel", "Date modified"), game => game.ModifiedSegment)
        };

        FilterGroups = groups;
        OnPropertyChanged(nameof(FilterGroups));
    }

    private DesktopFilterGroupViewModel CreateIdGroup(
        string field,
        string title,
        IEnumerable<DatabaseObject> values,
        Func<Game, IEnumerable<Guid>> getIds)
    {
        var group = new DesktopFilterGroupViewModel(field, title, DesktopFilterValueKind.Id, FilterGroupChanged);
        var normalizedGames = allGames.Select(game =>
            (Game: game.Game, Ids: getIds(game.Game)?.ToHashSet() ?? new HashSet<Guid>())).ToList();
        group.AddOption(Localize("LOCNone", "None"), Guid.Empty,
            normalizedGames.Count(item => item.Ids.Count == 0));
        foreach (var value in values.OrderBy(value => value.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            group.AddOption(value.Name, value.Id,
                normalizedGames.Count(item => item.Ids.Contains(value.Id)));
        }

        return group;
    }

    private DesktopFilterGroupViewModel CreateLibraryGroup()
    {
        var group = new DesktopFilterGroupViewModel(
            nameof(FilterPresetSettings.Library),
            Localize("LOCGameProviderTitle", "Libraries"),
            DesktopFilterValueKind.Id,
            FilterGroupChanged);
        foreach (var item in allGames
            .GroupBy(game => game.Game.PluginId)
            .OrderBy(item => item.Key))
        {
            var name = item.Key == Guid.Empty ? "Playnite" : item.Key.ToString();
            group.AddOption(name, item.Key, item.Count());
        }

        return group;
    }

    private DesktopFilterGroupViewModel CreateReleaseYearGroup()
    {
        var group = new DesktopFilterGroupViewModel(
            nameof(FilterPresetSettings.ReleaseYear),
            Localize("LOCGameReleaseYearTitle", "Release year"),
            DesktopFilterValueKind.String,
            FilterGroupChanged);
        group.AddOption(Localize("LOCNone", "None"), Playnite.FilterSettings.MissingFieldString,
            allGames.Count(game => !game.Game.ReleaseDate.HasValue));
        foreach (var item in allGames
            .Where(game => game.Game.ReleaseDate.HasValue)
            .GroupBy(game => game.Game.ReleaseYear.ToString())
            .OrderByDescending(item => item.Key, StringComparer.Ordinal))
        {
            group.AddOption(item.Key, item.Key, item.Count());
        }

        return group;
    }

    private DesktopFilterGroupViewModel CreateEnumGroup<TEnum>(
        string field,
        string title,
        Func<Game, TEnum> getValue)
        where TEnum : struct, Enum
    {
        var group = new DesktopFilterGroupViewModel(field, title, DesktopFilterValueKind.Enum, FilterGroupChanged);
        foreach (var value in Enum.GetValues<TEnum>())
        {
            var intValue = Convert.ToInt32(value);
            group.AddOption(GetEnumDisplayName(value), intValue,
                allGames.Count(game => EqualityComparer<TEnum>.Default.Equals(getValue(game.Game), value)));
        }

        return group;
    }

    private static string GetEnumDisplayName<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var description = typeof(TEnum).GetField(name)?.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (string.IsNullOrWhiteSpace(description))
        {
            return name;
        }

        return description.StartsWith("LOC", StringComparison.Ordinal)
            ? Localize(description, name)
            : description;
    }

    private void FilterGroupChanged(DesktopFilterGroupViewModel group)
    {
        if (suppressFilterControlChanges)
        {
            return;
        }

        var selected = group.Options.Where(option => option.IsSelected).Select(option => option.Value).ToList();
        switch (group.ValueKind)
        {
            case DesktopFilterValueKind.Id:
                SetIdFilter(group.Field, selected.Cast<Guid>().ToList());
                break;
            case DesktopFilterValueKind.String:
                workingFilterSettings.ReleaseYear = selected.Count == 0
                    ? null
                    : new SdkStringFilterItemProperties(selected.Cast<string>().ToList());
                break;
            case DesktopFilterValueKind.Enum:
                SetEnumFilter(group.Field, selected.Cast<int>().ToList());
                break;
            default:
                throw new InvalidOperationException($"Unsupported filter value kind {group.ValueKind}.");
        }

        WorkingFilterChanged();
    }

    private void SetIdFilter(string field, List<Guid> values)
    {
        var filter = values.Count == 0 ? null : new SdkIdItemFilterItemProperties(values);
        switch (field)
        {
            case nameof(FilterPresetSettings.Genre): workingFilterSettings.Genre = filter; break;
            case nameof(FilterPresetSettings.Platform): workingFilterSettings.Platform = filter; break;
            case nameof(FilterPresetSettings.Publisher): workingFilterSettings.Publisher = filter; break;
            case nameof(FilterPresetSettings.Developer): workingFilterSettings.Developer = filter; break;
            case nameof(FilterPresetSettings.Category): workingFilterSettings.Category = filter; break;
            case nameof(FilterPresetSettings.Tag): workingFilterSettings.Tag = filter; break;
            case nameof(FilterPresetSettings.Series): workingFilterSettings.Series = filter; break;
            case nameof(FilterPresetSettings.Region): workingFilterSettings.Region = filter; break;
            case nameof(FilterPresetSettings.Source): workingFilterSettings.Source = filter; break;
            case nameof(FilterPresetSettings.AgeRating): workingFilterSettings.AgeRating = filter; break;
            case nameof(FilterPresetSettings.Library): workingFilterSettings.Library = filter; break;
            case nameof(FilterPresetSettings.CompletionStatuses): workingFilterSettings.CompletionStatuses = filter; break;
            case nameof(FilterPresetSettings.Feature): workingFilterSettings.Feature = filter; break;
            default: throw new InvalidOperationException($"Unsupported ID filter field {field}.");
        }
    }

    private void SetEnumFilter(string field, List<int> values)
    {
        var filter = values.Count == 0 ? null : new SdkEnumFilterItemProperties(values);
        switch (field)
        {
            case nameof(FilterPresetSettings.UserScore): workingFilterSettings.UserScore = filter; break;
            case nameof(FilterPresetSettings.CriticScore): workingFilterSettings.CriticScore = filter; break;
            case nameof(FilterPresetSettings.CommunityScore): workingFilterSettings.CommunityScore = filter; break;
            case nameof(FilterPresetSettings.LastActivity): workingFilterSettings.LastActivity = filter; break;
            case nameof(FilterPresetSettings.RecentActivity): workingFilterSettings.RecentActivity = filter; break;
            case nameof(FilterPresetSettings.Added): workingFilterSettings.Added = filter; break;
            case nameof(FilterPresetSettings.Modified): workingFilterSettings.Modified = filter; break;
            case nameof(FilterPresetSettings.PlayTime): workingFilterSettings.PlayTime = filter; break;
            case nameof(FilterPresetSettings.InstallSize): workingFilterSettings.InstallSize = filter; break;
            default: throw new InvalidOperationException($"Unsupported enum filter field {field}.");
        }
    }

    private void SetWorkingFilterBoolean(bool current, bool value, Action apply, string propertyName)
    {
        if (current == value)
        {
            return;
        }

        apply();
        OnPropertyChanged(propertyName);
        WorkingFilterChanged();
    }

    private void WorkingFilterChanged()
    {
        if (suppressFilterControlChanges)
        {
            return;
        }

        IsFilterDirty = SelectedFilterPreset != null;
        UpdateFilterState();
        ApplyFilters();
    }

    private void UpdateFilterState()
    {
        LiveFilterMatchCount = database == null
            ? allGames.Count
            : allGames.Count(game => database.GetGameMatchesFilter(
                game.Game,
                workingFilterSettings,
                settings.FuzzyMatchingInNameFilter));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(FilterActiveIndicator));
        (ClearFiltersCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
    }

    private void LoadWorkingFilter(FilterPresetSettings source, bool dirty)
    {
        workingFilterSettings = CloneFilterSettings(source);
        suppressFilterControlChanges = true;
        try
        {
            OnPropertyChanged(nameof(FilterName));
            OnPropertyChanged(nameof(FilterVersion));
            OnPropertyChanged(nameof(FilterUseAndStyle));
            OnPropertyChanged(nameof(FilterInstalled));
            OnPropertyChanged(nameof(FilterUninstalled));
            OnPropertyChanged(nameof(FilterHidden));
            OnPropertyChanged(nameof(FilterFavorite));
            foreach (var group in FilterGroups)
            {
                group.SetSelectedValues(GetSelectedFilterValues(group));
            }
        }
        finally
        {
            suppressFilterControlChanges = false;
        }

        IsFilterDirty = dirty;
        UpdateFilterState();
    }

    private IEnumerable<object> GetSelectedFilterValues(DesktopFilterGroupViewModel group)
    {
        if (group.ValueKind == DesktopFilterValueKind.String)
        {
            return workingFilterSettings.ReleaseYear?.Values?.Cast<object>() ?? Array.Empty<object>();
        }

        if (group.ValueKind == DesktopFilterValueKind.Enum)
        {
            var filter = group.Field switch
            {
                nameof(FilterPresetSettings.UserScore) => workingFilterSettings.UserScore,
                nameof(FilterPresetSettings.CriticScore) => workingFilterSettings.CriticScore,
                nameof(FilterPresetSettings.CommunityScore) => workingFilterSettings.CommunityScore,
                nameof(FilterPresetSettings.LastActivity) => workingFilterSettings.LastActivity,
                nameof(FilterPresetSettings.RecentActivity) => workingFilterSettings.RecentActivity,
                nameof(FilterPresetSettings.Added) => workingFilterSettings.Added,
                nameof(FilterPresetSettings.Modified) => workingFilterSettings.Modified,
                nameof(FilterPresetSettings.PlayTime) => workingFilterSettings.PlayTime,
                nameof(FilterPresetSettings.InstallSize) => workingFilterSettings.InstallSize,
                _ => null
            };
            return filter?.Values?.Cast<object>() ?? Array.Empty<object>();
        }

        var idFilter = group.Field switch
        {
            nameof(FilterPresetSettings.Genre) => workingFilterSettings.Genre,
            nameof(FilterPresetSettings.Platform) => workingFilterSettings.Platform,
            nameof(FilterPresetSettings.Publisher) => workingFilterSettings.Publisher,
            nameof(FilterPresetSettings.Developer) => workingFilterSettings.Developer,
            nameof(FilterPresetSettings.Category) => workingFilterSettings.Category,
            nameof(FilterPresetSettings.Tag) => workingFilterSettings.Tag,
            nameof(FilterPresetSettings.Series) => workingFilterSettings.Series,
            nameof(FilterPresetSettings.Region) => workingFilterSettings.Region,
            nameof(FilterPresetSettings.Source) => workingFilterSettings.Source,
            nameof(FilterPresetSettings.AgeRating) => workingFilterSettings.AgeRating,
            nameof(FilterPresetSettings.Library) => workingFilterSettings.Library,
            nameof(FilterPresetSettings.CompletionStatuses) => workingFilterSettings.CompletionStatuses,
            nameof(FilterPresetSettings.Feature) => workingFilterSettings.Feature,
            _ => null
        };
        return idFilter?.Ids?.Cast<object>() ?? Array.Empty<object>();
    }

    private void ClearWorkingFilters()
    {
        selectedFilterPreset = null;
        settings.ActiveFilterPreset = Guid.Empty;
        OnPropertyChanged(nameof(SelectedFilterPreset));
        LoadWorkingFilter(new FilterPresetSettings(), false);
        ApplyFilters();
        RaiseFilterCommandStates();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveCurrentFilterPreset()
    {
        if (database == null || dialogService == null)
        {
            StatusText = "The filter preset dialog is not available.";
            return;
        }

        var toggles = new List<MessageBoxToggle>
        {
            new(Localize("LOCFilterPresetSaveViewOptions", "Save sorting and grouping"), true),
            new(Localize("LOCFilterPresetShowOnFSTopPanel", "Show in Fullscreen quick selection"), false)
        };
        var result = dialogService.ShowInput(
            Localize("LOCEnterName", "Enter a preset name."),
            Localize("LOCFilterPreset", "Filter preset"),
            SelectedFilterPreset?.Name ?? string.Empty,
            toggles);
        if (!result.Result || string.IsNullOrWhiteSpace(result.SelectedString))
        {
            return;
        }

        var existing = database.FilterPresets.FirstOrDefault(preset =>
            string.Equals(preset.Name, result.SelectedString.Trim(), StringComparison.CurrentCultureIgnoreCase));
        if (existing != null)
        {
            var choice = dialogService.ShowMessage(
                Localize("LOCFilterPresetNameConflict", "A preset with this name already exists."),
                Localize("LOCFilterPreset", "Filter preset"),
                new[] { "Overwrite", Localize("LOCCancelLabel", "Cancel") },
                1,
                1);
            if (choice != "Overwrite")
            {
                return;
            }
        }

        AddOrUpdateFilterPreset(result.SelectedString.Trim(), existing, toggles[0].Selected, toggles[1].Selected);
    }

    private FilterPreset AddOrUpdateFilterPreset(
        string name,
        FilterPreset existing,
        bool saveViewOptions,
        bool showInFullscreen)
    {
        var preset = existing ?? new FilterPreset();
        preset.Name = name;
        preset.Settings = CloneFilterSettings(workingFilterSettings);
        preset.ShowInFullscreeQuickSelection = showInFullscreen;
        preset.SortingOrder = saveViewOptions ? SelectedSortOrder : null;
        preset.SortingOrderDirection = saveViewOptions ? SelectedSortDirection : null;
        preset.GroupingOrder = saveViewOptions ? SelectedGrouping : null;
        if (existing == null)
        {
            database.FilterPresets.Add(preset);
        }
        else
        {
            database.FilterPresets.Update(preset);
        }

        RefreshFilterPresetCollection(preset.Id);
        StatusText = $"Saved filter preset {preset.Name}.";
        return preset;
    }

    private void RenameSelectedFilterPreset()
    {
        var preset = SelectedFilterPreset;
        if (preset == null || dialogService == null)
        {
            return;
        }

        var toggles = new List<MessageBoxToggle>
        {
            new(Localize("LOCFilterPresetShowOnFSTopPanel", "Show in Fullscreen quick selection"),
                preset.ShowInFullscreeQuickSelection)
        };
        var result = dialogService.ShowInput(
            Localize("LOCEnterName", "Enter a preset name."),
            Localize("LOCRename", "Rename filter preset"),
            preset.Name,
            toggles);
        if (!result.Result || string.IsNullOrWhiteSpace(result.SelectedString))
        {
            return;
        }

        var name = result.SelectedString.Trim();
        if (database.FilterPresets.Any(item => item.Id != preset.Id &&
            string.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            dialogService.ShowMessage(
                Localize("LOCFilterPresetNameConflict", "A preset with this name already exists."),
                Localize("LOCFilterPreset", "Filter preset"),
                new[] { "OK" },
                0,
                0);
            return;
        }

        preset.Name = name;
        preset.ShowInFullscreeQuickSelection = toggles[0].Selected;
        database.FilterPresets.Update(preset);
        RefreshFilterPresetCollection(preset.Id);
        StatusText = $"Renamed filter preset to {preset.Name}.";
    }

    private void DeleteSelectedFilterPreset()
    {
        var preset = SelectedFilterPreset;
        if (preset == null || dialogService == null)
        {
            return;
        }

        var removeMessage = Localize("LOCAskRemoveItemMessage", $"Remove filter preset {preset.Name}?")
            .Replace("{0}", preset.Name, StringComparison.Ordinal);
        var remove = dialogService.ShowMessage(
            removeMessage,
            Localize("LOCAskRemoveItemTitle", "Remove filter preset"),
            new[] { Localize("LOCYesLabel", "Yes"), Localize("LOCNoLabel", "No") },
            1,
            1);
        if (remove != Localize("LOCYesLabel", "Yes"))
        {
            return;
        }

        DeleteFilterPreset(preset);
    }

    private void DeleteFilterPreset(FilterPreset preset)
    {
        database.FilterPresets.Remove(preset);
        RefreshFilterPresetCollection(null);
        ClearWorkingFilters();
        StatusText = $"Removed filter preset {preset.Name}.";
    }

    private void RefreshFilterPresetCollection(Guid? selectedId)
    {
        FilterPresets.Clear();
        foreach (var preset in database.GetSortedFilterPresets())
        {
            FilterPresets.Add(preset);
        }

        SelectedFilterPreset = selectedId.HasValue
            ? FilterPresets.FirstOrDefault(preset => preset.Id == selectedId.Value)
            : null;
        OnPropertyChanged(nameof(FilterPresets));
        RaiseFilterCommandStates();
    }

    private void RaiseFilterCommandStates()
    {
        (ClearFiltersCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (SaveFilterPresetCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (RenameFilterPresetCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
        (DeleteFilterPresetCommand as AppRelayCommand)?.RaiseCanExecuteChanged();
    }

    private FilterPresetSettings GetWorkingFilterSettings() => workingFilterSettings;

    internal FilterPreset SaveFilterPresetForTest(string name) =>
        AddOrUpdateFilterPreset(name, null, true, true);

    internal void RenameFilterPresetForTest(FilterPreset preset, string name)
    {
        preset.Name = name;
        database.FilterPresets.Update(preset);
        RefreshFilterPresetCollection(preset.Id);
    }

    internal void DeleteFilterPresetForTest(FilterPreset preset) => DeleteFilterPreset(preset);

    internal void ApplyFilterSettingsForTest(FilterPresetSettings filterSettings)
    {
        LoadWorkingFilter(filterSettings, true);
        ApplyFilters();
    }

    private static bool HasActiveFilter(FilterPresetSettings filter) =>
        filter != null &&
        (filter.IsInstalled || filter.IsUnInstalled || filter.Hidden || filter.Favorite ||
         !string.IsNullOrWhiteSpace(filter.Name) || !string.IsNullOrWhiteSpace(filter.Version) ||
         filter.ReleaseYear?.Values?.Count > 0 || filter.Genre?.Ids?.Count > 0 ||
         filter.Platform?.Ids?.Count > 0 || filter.Publisher?.Ids?.Count > 0 ||
         filter.Developer?.Ids?.Count > 0 || filter.Category?.Ids?.Count > 0 ||
         filter.Tag?.Ids?.Count > 0 || filter.Series?.Ids?.Count > 0 ||
         filter.Region?.Ids?.Count > 0 || filter.Source?.Ids?.Count > 0 ||
         filter.AgeRating?.Ids?.Count > 0 || filter.Library?.Ids?.Count > 0 ||
         filter.CompletionStatuses?.Ids?.Count > 0 || filter.Feature?.Ids?.Count > 0 ||
         filter.UserScore?.Values?.Count > 0 || filter.CriticScore?.Values?.Count > 0 ||
         filter.CommunityScore?.Values?.Count > 0 || filter.LastActivity?.Values?.Count > 0 ||
         filter.RecentActivity?.Values?.Count > 0 || filter.Added?.Values?.Count > 0 ||
         filter.Modified?.Values?.Count > 0 || filter.PlayTime?.Values?.Count > 0 ||
         filter.InstallSize?.Values?.Count > 0);

    private static FilterPresetSettings CloneFilterSettings(FilterPresetSettings source)
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

    private static SdkIdItemFilterItemProperties Clone(SdkIdItemFilterItemProperties value) => value == null
        ? null
        : new SdkIdItemFilterItemProperties(value.Ids?.ToList()) { Text = value.Text };

    private static SdkStringFilterItemProperties Clone(SdkStringFilterItemProperties value) => value == null
        ? null
        : new SdkStringFilterItemProperties(value.Values?.ToList());

    private static SdkEnumFilterItemProperties Clone(SdkEnumFilterItemProperties value) => value == null
        ? null
        : new SdkEnumFilterItemProperties(value.Values?.ToList());
}

public enum DesktopFilterValueKind
{
    Id,
    String,
    Enum
}

public sealed class DesktopFilterGroupViewModel : INotifyPropertyChanged
{
    private readonly Action<DesktopFilterGroupViewModel> changed;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Field { get; }
    public string Title { get; }
    public DesktopFilterValueKind ValueKind { get; }
    public ObservableCollection<DesktopFilterOptionViewModel> Options { get; } = new();
    public int SelectedCount => Options.Count(option => option.IsSelected);
    public string DisplayTitle => SelectedCount == 0 ? Title : $"{Title} · {SelectedCount:N0}";

    public DesktopFilterGroupViewModel(
        string field,
        string title,
        DesktopFilterValueKind valueKind,
        Action<DesktopFilterGroupViewModel> changed)
    {
        Field = field;
        Title = title;
        ValueKind = valueKind;
        this.changed = changed;
    }

    public void AddOption(string name, object value, int count) =>
        Options.Add(new DesktopFilterOptionViewModel(name, value, count, OptionChanged));

    internal void SetSelectedValues(IEnumerable<object> values)
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

public sealed class DesktopFilterOptionViewModel : INotifyPropertyChanged
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

    public DesktopFilterOptionViewModel(string name, object value, int count, Action changed)
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
