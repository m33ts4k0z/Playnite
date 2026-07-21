using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopStatisticsViewModel : INotifyPropertyChanged
{
    private readonly GameDatabase database;
    private readonly DesktopSettings settings;
    private readonly Func<Guid, string> resolveLibraryName;
    private readonly Action<Guid> navigateToGame;
    private IReadOnlyList<DesktopStatisticsFilter> filters = Array.Empty<DesktopStatisticsFilter>();
    private IReadOnlyList<DesktopStatisticsFilterOption> filterOptions = Array.Empty<DesktopStatisticsFilterOption>();
    private DesktopStatisticsFilter selectedFilter;
    private DesktopStatisticsFilterOption selectedFilterOption;
    private DesktopStatisticsSnapshot globalStats = DesktopStatisticsSnapshot.Empty;
    private DesktopStatisticsSnapshot filteredStats = DesktopStatisticsSnapshot.Empty;
    private bool includeHidden;
    private bool isVisible;

    public event PropertyChangedEventHandler PropertyChanged;

    public bool IsVisible { get => isVisible; private set => SetField(ref isVisible, value); }
    public IReadOnlyList<DesktopStatisticsFilter> Filters { get => filters; private set => SetField(ref filters, value); }
    public IReadOnlyList<DesktopStatisticsFilterOption> FilterOptions { get => filterOptions; private set => SetField(ref filterOptions, value); }
    public DesktopStatisticsSnapshot GlobalStats { get => globalStats; private set => SetField(ref globalStats, value); }
    public DesktopStatisticsSnapshot FilteredStats { get => filteredStats; private set => SetField(ref filteredStats, value); }

    public bool IncludeHidden
    {
        get => includeHidden;
        set
        {
            if (SetField(ref includeHidden, value))
            {
                Refresh();
            }
        }
    }

    public DesktopStatisticsFilter SelectedFilter
    {
        get => selectedFilter;
        set
        {
            if (!SetField(ref selectedFilter, value))
            {
                return;
            }

            SelectedFilterOption = null;
            LoadFilterOptions();
            RefreshFiltered();
        }
    }

    public DesktopStatisticsFilterOption SelectedFilterOption
    {
        get => selectedFilterOption;
        set
        {
            if (SetField(ref selectedFilterOption, value))
            {
                RefreshFiltered();
            }
        }
    }

    public ICommand CloseCommand { get; }
    public ICommand RefreshCommand { get; }

    public DesktopStatisticsViewModel(
        GameDatabase database,
        DesktopSettings settings,
        Func<Guid, string> resolveLibraryName,
        Action<Guid> navigateToGame)
    {
        this.database = database;
        this.settings = settings ?? new DesktopSettings();
        this.resolveLibraryName = resolveLibraryName ?? (id => id.ToString());
        this.navigateToGame = navigateToGame ?? (_ => { });
        CloseCommand = new AppRelayCommand(Close);
        RefreshCommand = new AppRelayCommand(Refresh);
        RefreshLocalization();
    }

    public void Open()
    {
        RefreshLocalization();
        Refresh();
        IsVisible = true;
    }

    public void Close() => IsVisible = false;

    public void RefreshIfVisible()
    {
        if (IsVisible)
        {
            Refresh();
        }
    }

    public void RefreshLocalization()
    {
        var selectedKind = SelectedFilter?.Kind ?? DesktopStatisticsFilterKind.None;
        Filters = new[]
        {
            Filter(DesktopStatisticsFilterKind.None, "LOCNone", "None"),
            Filter(DesktopStatisticsFilterKind.Library, "LOCGameProviderTitle", "Library"),
            Filter(DesktopStatisticsFilterKind.Genre, "LOCGenresLabel", "Genres"),
            Filter(DesktopStatisticsFilterKind.Feature, "LOCFeaturesLabel", "Features"),
            Filter(DesktopStatisticsFilterKind.Tag, "LOCTagsLabel", "Tags"),
            Filter(DesktopStatisticsFilterKind.Platform, "LOCPlatformsTitle", "Platforms"),
            Filter(DesktopStatisticsFilterKind.Developer, "LOCDevelopersLabel", "Developers"),
            Filter(DesktopStatisticsFilterKind.Publisher, "LOCPublishersLabel", "Publishers"),
            Filter(DesktopStatisticsFilterKind.Category, "LOCCategoriesLabel", "Categories"),
            Filter(DesktopStatisticsFilterKind.ReleaseYear, "LOCGameReleaseYearTitle", "Release year"),
            Filter(DesktopStatisticsFilterKind.Series, "LOCSeriesLabel", "Series"),
            Filter(DesktopStatisticsFilterKind.AgeRating, "LOCAgeRatingsLabel", "Age ratings"),
            Filter(DesktopStatisticsFilterKind.Region, "LOCRegionsLabel", "Regions"),
            Filter(DesktopStatisticsFilterKind.Source, "LOCSourcesLabel", "Sources"),
            Filter(DesktopStatisticsFilterKind.CompletionStatus, "LOCCompletionStatus", "Completion status"),
            Filter(DesktopStatisticsFilterKind.InstallationStatus, "LOCGameInstallationStatus", "Installation status")
        };

        selectedFilter = Filters.First(item => item.Kind == selectedKind);
        OnPropertyChanged(nameof(SelectedFilter));
        selectedFilterOption = null;
        OnPropertyChanged(nameof(SelectedFilterOption));
        LoadFilterOptions();
        RefreshIfVisible();
    }

    public void Refresh()
    {
        if (database?.IsOpen != true)
        {
            GlobalStats = DesktopStatisticsSnapshot.Empty;
            FilteredStats = DesktopStatisticsSnapshot.Empty;
            return;
        }

        var games = database.Games.ToList();
        GlobalStats = BuildSnapshot(games, false);
        FilteredStats = BuildSnapshot(games, true);
    }

    private void RefreshFiltered()
    {
        if (database?.IsOpen == true)
        {
            FilteredStats = BuildSnapshot(database.Games.ToList(), true);
        }
    }

    private void LoadFilterOptions()
    {
        if (database?.IsOpen != true || SelectedFilter == null)
        {
            FilterOptions = Array.Empty<DesktopStatisticsFilterOption>();
            return;
        }

        IEnumerable<DesktopStatisticsFilterOption> options = SelectedFilter.Kind switch
        {
            DesktopStatisticsFilterKind.None => Array.Empty<DesktopStatisticsFilterOption>(),
            DesktopStatisticsFilterKind.Library => database.Games
                .Select(game => game.PluginId)
                .Distinct()
                .Select(id => new DesktopStatisticsFilterOption(id, ResolveLibrary(id))),
            DesktopStatisticsFilterKind.Genre => BuildDatabaseOptions(database.UsedGenres, database.Genres),
            DesktopStatisticsFilterKind.Feature => BuildDatabaseOptions(database.UsedFeastures, database.Features),
            DesktopStatisticsFilterKind.Tag => BuildDatabaseOptions(database.UsedTags, database.Tags),
            DesktopStatisticsFilterKind.Platform => BuildDatabaseOptions(database.UsedPlatforms, database.Platforms),
            DesktopStatisticsFilterKind.Developer => BuildDatabaseOptions(database.UsedDevelopers, database.Companies),
            DesktopStatisticsFilterKind.Publisher => BuildDatabaseOptions(database.UsedPublishers, database.Companies),
            DesktopStatisticsFilterKind.Category => BuildDatabaseOptions(database.UsedCategories, database.Categories),
            DesktopStatisticsFilterKind.ReleaseYear => database.Games
                .Where(game => game.ReleaseYear.HasValue)
                .Select(game => game.ReleaseYear.Value)
                .Distinct()
                .Select(year => new DesktopStatisticsFilterOption(year, year.ToString(CultureInfo.CurrentCulture))),
            DesktopStatisticsFilterKind.Series => BuildDatabaseOptions(database.UsedSeries, database.Series),
            DesktopStatisticsFilterKind.AgeRating => BuildDatabaseOptions(database.UsedAgeRatings, database.AgeRatings),
            DesktopStatisticsFilterKind.Region => BuildDatabaseOptions(database.UsedRegions, database.Regions),
            DesktopStatisticsFilterKind.Source => BuildDatabaseOptions(database.UsedSources, database.Sources),
            DesktopStatisticsFilterKind.CompletionStatus => BuildDatabaseOptions(database.UsedCompletionStatuses, database.CompletionStatuses),
            DesktopStatisticsFilterKind.InstallationStatus => new[]
            {
                new DesktopStatisticsFilterOption(true, Localize("LOCGameIsGameInstalledTitle", "Installed")),
                new DesktopStatisticsFilterOption(false, Localize("LOCGameIsUnInstalledTitle", "Not installed"))
            },
            _ => Array.Empty<DesktopStatisticsFilterOption>()
        };

        FilterOptions = options
            .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static IEnumerable<DesktopStatisticsFilterOption> BuildDatabaseOptions<T>(
        IEnumerable<Guid> usedIds,
        IEnumerable<T> items)
        where T : DatabaseObject
    {
        var used = usedIds.ToHashSet();
        return items
            .Where(item => used.Contains(item.Id))
            .Select(item => new DesktopStatisticsFilterOption(item.Id, DesktopLocalization.ResolveStored(item.Name)));
    }

    private DesktopStatisticsSnapshot BuildSnapshot(IReadOnlyList<Game> source, bool filtered)
    {
        var games = source
            .Where(game => IncludeHidden || !game.Hidden)
            .Where(game => !filtered || PassesFilter(game))
            .ToList();
        var total = (ulong)games.Count;
        var installed = (ulong)games.Count(game => game.IsInstalled);
        var hidden = (ulong)games.Count(game => game.Hidden);
        var favorite = (ulong)games.Count(game => game.Favorite);
        var playedGames = games.Where(game => game.Playtime > 0).ToList();
        var totalPlaytime = playedGames.Aggregate(0UL, (current, game) => current + game.Playtime);
        var totalInstallSize = games
            .Where(game => game.IsInstalled && game.InstallSize > 0)
            .Aggregate(0UL, (current, game) => current + game.InstallSize.GetValueOrDefault());

        var overview = new[]
        {
            Metric(Localize("LOCAll", "All"), total, total),
            Metric(Localize("LOCGameIsGameInstalledTitle", "Installed"), installed, total),
            Metric(Localize("LOCGameIsUnInstalledTitle", "Not installed"), total - installed, total),
            Metric(Localize("LOCGameHiddenTitle", "Hidden"), hidden, total),
            Metric(Localize("LOCGameFavoriteTitle", "Favorite"), favorite, total)
        };

        var completionStates = games
            .Select(game => new { Game = game, Status = database.CompletionStatuses.FirstOrDefault(item => item.Id == game.CompletionStatusId) })
            .Where(item => item.Status != null)
            .GroupBy(item => item.Status)
            .Select(group => Metric(DesktopLocalization.ResolveStored(group.Key.Name), (ulong)group.Count(), total))
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var libraries = games
            .GroupBy(game => game.PluginId)
            .Select(group => Metric(ResolveLibrary(group.Key), (ulong)group.Count(), total))
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var topPlayed = games
            .OrderByDescending(game => game.Playtime)
            .ThenBy(game => game.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(50)
            .Select(game => new DesktopStatisticsGameMetric(
                game.Id,
                game.Name,
                game.Playtime,
                Percentage(game.Playtime, totalPlaytime),
                FormatPlaytime(game.Playtime),
                () =>
                {
                    Close();
                    navigateToGame(game.Id);
                }))
            .ToList();

        return new DesktopStatisticsSnapshot(
            total,
            overview,
            completionStates,
            libraries,
            topPlayed,
            totalPlaytime,
            playedGames.Count > 0 ? totalPlaytime / (ulong)playedGames.Count : 0,
            totalInstallSize,
            FormatPlaytime(totalPlaytime),
            FormatPlaytime(playedGames.Count > 0 ? totalPlaytime / (ulong)playedGames.Count : 0),
            FormatSize(totalInstallSize));
    }

    private bool PassesFilter(Game game)
    {
        if (SelectedFilterOption == null || SelectedFilter == null || SelectedFilter.Kind == DesktopStatisticsFilterKind.None)
        {
            return true;
        }

        var value = SelectedFilterOption.Value;
        return SelectedFilter.Kind switch
        {
            DesktopStatisticsFilterKind.Library => value is Guid libraryId && game.PluginId == libraryId,
            DesktopStatisticsFilterKind.Genre => Contains(game.GenreIds, value),
            DesktopStatisticsFilterKind.Feature => Contains(game.FeatureIds, value),
            DesktopStatisticsFilterKind.Tag => Contains(game.TagIds, value),
            DesktopStatisticsFilterKind.Platform => Contains(game.PlatformIds, value),
            DesktopStatisticsFilterKind.Developer => Contains(game.DeveloperIds, value),
            DesktopStatisticsFilterKind.Publisher => Contains(game.PublisherIds, value),
            DesktopStatisticsFilterKind.Category => Contains(game.CategoryIds, value),
            DesktopStatisticsFilterKind.ReleaseYear => value is int year && game.ReleaseYear == year,
            DesktopStatisticsFilterKind.Series => Contains(game.SeriesIds, value),
            DesktopStatisticsFilterKind.AgeRating => Contains(game.AgeRatingIds, value),
            DesktopStatisticsFilterKind.Region => Contains(game.RegionIds, value),
            DesktopStatisticsFilterKind.Source => value is Guid sourceId && game.SourceId == sourceId,
            DesktopStatisticsFilterKind.CompletionStatus => value is Guid statusId && game.CompletionStatusId == statusId,
            DesktopStatisticsFilterKind.InstallationStatus => value is bool installed && game.IsInstalled == installed,
            _ => true
        };
    }

    private static bool Contains(IEnumerable<Guid> ids, object value) =>
        value is Guid id && ids?.Contains(id) == true;

    private DesktopStatisticsMetric Metric(string name, ulong value, ulong total) =>
        new(name, value, Percentage(value, total), value.ToString("N0", CultureInfo.CurrentCulture));

    private static int Percentage(ulong value, ulong total) =>
        total == 0 ? 0 : Convert.ToInt32((double)value / total * 100d);

    private string ResolveLibrary(Guid id)
    {
        if (id == Guid.Empty)
        {
            return "Playnite";
        }

        var name = resolveLibraryName(id);
        return string.IsNullOrWhiteSpace(name) ? id.ToString() : DesktopLocalization.ResolveStored(name);
    }

    private string FormatPlaytime(ulong seconds)
    {
        if (seconds == 0)
        {
            return Localize("LOCPlayedNone", "Not played");
        }

        var duration = TimeSpan.FromSeconds(seconds);
        if (settings.PlaytimeUseDaysFormat && duration.TotalHours >= 24)
        {
            return FormatLocalized("LOCPlayedDays", "{0}d {1}h {2}m", (int)duration.TotalDays, duration.Hours, duration.Minutes);
        }

        if (duration.TotalHours >= 1)
        {
            return FormatLocalized("LOCPlayedHours", "{0}h {1}m", (int)duration.TotalHours, duration.Minutes);
        }

        return duration.TotalMinutes >= 1
            ? FormatLocalized("LOCPlayedMinutes", "{0} minutes", (int)duration.TotalMinutes)
            : FormatLocalized("LOCPlayedSeconds", "{0} seconds", Math.Max(1, (int)duration.TotalSeconds));
    }

    private static string FormatSize(ulong bytes)
    {
        const double kibibyte = 1024d;
        const double mebibyte = kibibyte * 1024d;
        const double gibibyte = mebibyte * 1024d;
        return bytes >= gibibyte
            ? $"{bytes / gibibyte:N2} GB"
            : $"{bytes / mebibyte:N2} MB";
    }

    private static string FormatLocalized(string key, string fallback, params object[] values)
    {
        var format = Localize(key, fallback);
        try
        {
            return string.Format(CultureInfo.CurrentCulture, format, values);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.CurrentCulture, fallback, values);
        }
    }

    private static DesktopStatisticsFilter Filter(DesktopStatisticsFilterKind kind, string key, string fallback) =>
        new(kind, Localize(key, fallback));

    private static string Localize(string key, string fallback) => DesktopLocalization.Resolve(key, fallback);

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

public enum DesktopStatisticsFilterKind
{
    None,
    Library,
    Genre,
    Feature,
    Tag,
    Platform,
    Developer,
    Publisher,
    Category,
    ReleaseYear,
    Series,
    AgeRating,
    Region,
    Source,
    CompletionStatus,
    InstallationStatus
}

public sealed record DesktopStatisticsFilter(DesktopStatisticsFilterKind Kind, string Name)
{
    public override string ToString() => Name;
}

public sealed record DesktopStatisticsFilterOption(object Value, string Name)
{
    public override string ToString() => Name;
}

public record DesktopStatisticsMetric(string Name, ulong Value, int Percentage, string ValueText)
{
    public string PercentageText => $"{Percentage}%";
}

public sealed record DesktopStatisticsGameMetric : DesktopStatisticsMetric
{
    public Guid GameId { get; }
    public ICommand OpenCommand { get; }

    public DesktopStatisticsGameMetric(
        Guid gameId,
        string name,
        ulong value,
        int percentage,
        string valueText,
        Action open)
        : base(name, value, percentage, valueText)
    {
        GameId = gameId;
        OpenCommand = new AppRelayCommand(open);
    }
}

public sealed record DesktopStatisticsSnapshot(
    ulong TotalCount,
    IReadOnlyList<DesktopStatisticsMetric> Overview,
    IReadOnlyList<DesktopStatisticsMetric> CompletionStates,
    IReadOnlyList<DesktopStatisticsMetric> Libraries,
    IReadOnlyList<DesktopStatisticsGameMetric> TopPlayed,
    ulong TotalPlaytime,
    ulong AveragePlaytime,
    ulong TotalInstallSize,
    string TotalPlaytimeText,
    string AveragePlaytimeText,
    string TotalInstallSizeText)
{
    public static DesktopStatisticsSnapshot Empty { get; } = new(
        0,
        Array.Empty<DesktopStatisticsMetric>(),
        Array.Empty<DesktopStatisticsMetric>(),
        Array.Empty<DesktopStatisticsMetric>(),
        Array.Empty<DesktopStatisticsGameMetric>(),
        0,
        0,
        0,
        DesktopLocalization.Resolve("LOCPlayedNone", "Not played"),
        DesktopLocalization.Resolve("LOCPlayedNone", "Not played"),
        "0.00 MB");
}
