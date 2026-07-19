using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Input;

namespace Playnite.Avalonia.App.Services;

public enum GameSearchItemAction
{
    Play,
    SwitchTo,
    OpenMenu,
    Edit,
    None
}

public enum DefaultIconSourceOptions
{
    Library,
    Platform,
    General,
    None
}

public enum DefaultCoverSourceOptions
{
    Platform,
    General,
    None
}

public enum DefaultBackgroundSourceOptions
{
    Library,
    Platform,
    Cover,
    None
}

public enum UpdateCheckFrequency
{
    OnEveryStartup,
    OnceADay,
    OnceAWeek,
    Manually
}

public enum LibraryUpdateCheckFrequency
{
    Manually,
    OnEveryStartup,
    OnceADay,
    OnceAWeek
}

public enum AutoBackupFrequency
{
    OnceADay = 1,
    OnceAWeek = 2
}

public sealed record HotKey(Key Key, KeyModifiers Modifiers)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add(OperatingSystem.IsMacOS() ? "Cmd" : "Meta");
        }

        parts.Add(Key.ToString());
        return string.Join(" + ", parts);
    }
}

public abstract class SettingsValueObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class SearchWindowVisibilitySettings : SettingsValueObject
{
    private bool gameIcon = true;
    private bool libraryIcon = true;
    private bool hiddenStatus = true;
    private bool platform = true;
    private bool playTime = true;
    private bool completionStatus = true;
    private bool releaseDate;

    public bool GameIcon { get => gameIcon; set => SetField(ref gameIcon, value); }
    public bool LibraryIcon { get => libraryIcon; set => SetField(ref libraryIcon, value); }
    public bool HiddenStatus { get => hiddenStatus; set => SetField(ref hiddenStatus, value); }
    public bool Platform { get => platform; set => SetField(ref platform, value); }
    public bool PlayTime { get => playTime; set => SetField(ref playTime, value); }
    public bool CompletionStatus { get => completionStatus; set => SetField(ref completionStatus, value); }
    public bool ReleaseDate { get => releaseDate; set => SetField(ref releaseDate, value); }

    public SearchWindowVisibilitySettings Clone()
    {
        var clone = new SearchWindowVisibilitySettings();
        clone.CopyFrom(this);
        return clone;
    }

    public void CopyFrom(SearchWindowVisibilitySettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        GameIcon = source.GameIcon;
        LibraryIcon = source.LibraryIcon;
        HiddenStatus = source.HiddenStatus;
        Platform = source.Platform;
        PlayTime = source.PlayTime;
        CompletionStatus = source.CompletionStatus;
        ReleaseDate = source.ReleaseDate;
    }
}

public sealed class DetailsVisibilitySettings : SettingsValueObject
{
    private bool library = true;
    private bool playTime = true;
    private bool installSize = true;
    private bool installDirectory = true;
    private bool lastPlayed;
    private bool added;
    private bool recentActivity = true;
    private bool completionStatus;
    private bool icon;
    private bool coverImage = true;
    private bool backgroundImage = true;
    private bool platform = true;
    private bool genres = true;
    private bool developers = true;
    private bool publishers = true;
    private bool releaseDate = true;
    private bool categories = true;
    private bool tags = true;
    private bool links = true;
    private bool description = true;
    private bool ageRating;
    private bool series;
    private bool source;
    private bool region;
    private bool version;
    private bool communityScore;
    private bool criticScore;
    private bool userScore;
    private bool features = true;
    private bool notes = true;
    private bool name = true;

    public bool Library { get => library; set => SetField(ref library, value); }
    public bool PlayTime { get => playTime; set => SetField(ref playTime, value); }
    public bool InstallSize { get => installSize; set => SetField(ref installSize, value); }
    public bool InstallDirectory { get => installDirectory; set => SetField(ref installDirectory, value); }
    public bool LastPlayed { get => lastPlayed; set => SetField(ref lastPlayed, value); }
    public bool Added { get => added; set => SetField(ref added, value); }
    public bool RecentActivity { get => recentActivity; set => SetField(ref recentActivity, value); }
    public bool CompletionStatus { get => completionStatus; set => SetField(ref completionStatus, value); }
    public bool Icon { get => icon; set => SetField(ref icon, value); }
    public bool CoverImage { get => coverImage; set => SetField(ref coverImage, value); }
    public bool BackgroundImage { get => backgroundImage; set => SetField(ref backgroundImage, value); }
    public bool Platform { get => platform; set => SetField(ref platform, value); }
    public bool Genres { get => genres; set => SetField(ref genres, value); }
    public bool Developers { get => developers; set => SetField(ref developers, value); }
    public bool Publishers { get => publishers; set => SetField(ref publishers, value); }
    public bool ReleaseDate { get => releaseDate; set => SetField(ref releaseDate, value); }
    public bool Categories { get => categories; set => SetField(ref categories, value); }
    public bool Tags { get => tags; set => SetField(ref tags, value); }
    public bool Links { get => links; set => SetField(ref links, value); }
    public bool Description { get => description; set => SetField(ref description, value); }
    public bool AgeRating { get => ageRating; set => SetField(ref ageRating, value); }
    public bool Series { get => series; set => SetField(ref series, value); }
    public bool Source { get => source; set => SetField(ref source, value); }
    public bool Region { get => region; set => SetField(ref region, value); }
    public bool Version { get => version; set => SetField(ref version, value); }
    public bool CommunityScore { get => communityScore; set => SetField(ref communityScore, value); }
    public bool CriticScore { get => criticScore; set => SetField(ref criticScore, value); }
    public bool UserScore { get => userScore; set => SetField(ref userScore, value); }
    public bool Features { get => features; set => SetField(ref features, value); }
    public bool Notes { get => notes; set => SetField(ref notes, value); }
    public bool Name { get => name; set => SetField(ref name, value); }

    public DetailsVisibilitySettings Clone()
    {
        var clone = new DetailsVisibilitySettings();
        clone.CopyFrom(this);
        return clone;
    }

    public void CopyFrom(DetailsVisibilitySettings source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Library = source.Library;
        PlayTime = source.PlayTime;
        InstallSize = source.InstallSize;
        InstallDirectory = source.InstallDirectory;
        LastPlayed = source.LastPlayed;
        Added = source.Added;
        RecentActivity = source.RecentActivity;
        CompletionStatus = source.CompletionStatus;
        Icon = source.Icon;
        CoverImage = source.CoverImage;
        BackgroundImage = source.BackgroundImage;
        Platform = source.Platform;
        Genres = source.Genres;
        Developers = source.Developers;
        Publishers = source.Publishers;
        ReleaseDate = source.ReleaseDate;
        Categories = source.Categories;
        Tags = source.Tags;
        Links = source.Links;
        Description = source.Description;
        AgeRating = source.AgeRating;
        Series = source.Series;
        Source = source.Source;
        Region = source.Region;
        Version = source.Version;
        CommunityScore = source.CommunityScore;
        CriticScore = source.CriticScore;
        UserScore = source.UserScore;
        Features = source.Features;
        Notes = source.Notes;
        Name = source.Name;
    }
}
