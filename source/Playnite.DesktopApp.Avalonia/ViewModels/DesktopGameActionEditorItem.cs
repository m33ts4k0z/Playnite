using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.Avalonia.App.ViewModels;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopEmulatorProfileOption
{
    public string Id { get; }
    public string Name { get; }

    public DesktopEmulatorProfileOption(string id, string name)
    {
        Id = id;
        Name = name ?? string.Empty;
    }

    public override string ToString() => Name;
}

public sealed class DesktopEmulatorOption
{
    public Guid Id { get; }
    public string Name { get; }
    public IReadOnlyList<DesktopEmulatorProfileOption> Profiles { get; }

    public DesktopEmulatorOption(
        Guid id,
        string name,
        IEnumerable<DesktopEmulatorProfileOption> profiles = null)
    {
        Id = id;
        Name = name ?? string.Empty;
        Profiles = profiles?.ToList() ?? new List<DesktopEmulatorProfileOption>();
    }

    public override string ToString() => Name;
}

public sealed class DesktopGameActionEditorItem : INotifyPropertyChanged
{
    private string name;
    private GameActionType type;
    private bool isPlayAction;
    private string path;
    private string arguments;
    private string workingDir;
    private string script;
    private TrackingMode trackingMode;
    private string trackingPath;
    private int initialTrackingDelay;
    private int trackingFrequency;
    private Guid emulatorId;
    private string emulatorProfileId;
    private bool overrideDefaultArgs;
    private string additionalArguments;
    private DesktopEmulatorOption selectedEmulator;
    private DesktopEmulatorProfileOption selectedEmulatorProfile;

    public event PropertyChangedEventHandler PropertyChanged;

    public IReadOnlyList<GameActionType> Types { get; } = Enum.GetValues<GameActionType>();
    public IReadOnlyList<TrackingMode> TrackingModes { get; } = Enum.GetValues<TrackingMode>();
    public IReadOnlyList<DesktopEmulatorOption> Emulators { get; }
    public IReadOnlyList<DesktopEmulatorProfileOption> EmulatorProfiles { get; private set; } =
        Array.Empty<DesktopEmulatorProfileOption>();

    public string Name { get => name; set => SetField(ref name, value); }
    public GameActionType Type
    {
        get => type;
        set
        {
            if (SetField(ref type, value))
            {
                RaiseTypeState();
            }
        }
    }

    public bool IsPlayAction
    {
        get => isPlayAction;
        set
        {
            if (SetField(ref isPlayAction, value))
            {
                OnPropertyChanged(nameof(ShowTracking));
                OnPropertyChanged(nameof(ShowTrackingPath));
            }
        }
    }

    public string Path { get => path; set => SetField(ref path, value); }
    public string Arguments { get => arguments; set => SetField(ref arguments, value); }
    public string WorkingDir { get => workingDir; set => SetField(ref workingDir, value); }
    public string Script { get => script; set => SetField(ref script, value); }
    public TrackingMode TrackingMode
    {
        get => trackingMode;
        set
        {
            if (SetField(ref trackingMode, value))
            {
                OnPropertyChanged(nameof(ShowTrackingPath));
            }
        }
    }

    public string TrackingPath { get => trackingPath; set => SetField(ref trackingPath, value); }
    public int InitialTrackingDelay { get => initialTrackingDelay; set => SetField(ref initialTrackingDelay, value); }
    public int TrackingFrequency { get => trackingFrequency; set => SetField(ref trackingFrequency, value); }
    public bool OverrideDefaultArgs { get => overrideDefaultArgs; set => SetField(ref overrideDefaultArgs, value); }
    public string AdditionalArguments { get => additionalArguments; set => SetField(ref additionalArguments, value); }

    public DesktopEmulatorOption SelectedEmulator
    {
        get => selectedEmulator;
        set
        {
            if (ReferenceEquals(selectedEmulator, value))
            {
                return;
            }

            selectedEmulator = value;
            emulatorId = value?.Id ?? Guid.Empty;
            OnPropertyChanged();
            RefreshEmulatorProfiles(null);
        }
    }

    public DesktopEmulatorProfileOption SelectedEmulatorProfile
    {
        get => selectedEmulatorProfile;
        set
        {
            if (ReferenceEquals(selectedEmulatorProfile, value))
            {
                return;
            }

            selectedEmulatorProfile = value;
            emulatorProfileId = value?.Id;
            OnPropertyChanged();
        }
    }

    public bool IsFileAction => Type == GameActionType.File;
    public bool IsUrlAction => Type == GameActionType.URL;
    public bool IsEmulatorAction => Type == GameActionType.Emulator;
    public bool IsScriptAction => Type == GameActionType.Script;
    public bool ShowPath => IsFileAction || IsUrlAction;
    public bool ShowTracking => IsPlayAction && (IsFileAction || IsUrlAction);
    public bool ShowTrackingPath => ShowTracking &&
        (TrackingMode == TrackingMode.Directory || TrackingMode == TrackingMode.ProcessName);

    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand SelectPathCommand { get; }
    public ICommand SelectWorkingDirectoryCommand { get; }
    public ICommand SelectTrackingPathCommand { get; }
    public ICommand TestScriptCommand { get; }
    public ICommand RemoveCommand { get; }

    public DesktopGameActionEditorItem(
        GameAction action,
        IEnumerable<DesktopEmulatorOption> emulatorOptions,
        Action<DesktopGameActionEditorItem> moveUp,
        Action<DesktopGameActionEditorItem> moveDown,
        Action<DesktopGameActionEditorItem> remove,
        Action<DesktopGameActionEditorItem> selectPath,
        Action<DesktopGameActionEditorItem> selectWorkingDirectory,
        Action<DesktopGameActionEditorItem> selectTrackingPath,
        Action<DesktopGameActionEditorItem> testScript)
    {
        var source = action?.GetCopy() ?? new GameAction
        {
            Name = "New Action",
            IsPlayAction = true
        };
        name = source.Name ?? string.Empty;
        type = source.Type;
        isPlayAction = source.IsPlayAction;
        path = source.Path ?? string.Empty;
        arguments = source.Arguments ?? string.Empty;
        workingDir = source.WorkingDir ?? string.Empty;
        script = source.Script ?? string.Empty;
        trackingMode = source.TrackingMode;
        trackingPath = source.TrackingPath ?? string.Empty;
        initialTrackingDelay = source.InitialTrackingDelay;
        trackingFrequency = source.TrackingFrequency;
        emulatorId = source.EmulatorId;
        emulatorProfileId = source.EmulatorProfileId;
        overrideDefaultArgs = source.OverrideDefaultArgs;
        additionalArguments = source.AdditionalArguments ?? string.Empty;

        var options = emulatorOptions?.ToList() ?? new List<DesktopEmulatorOption>();
        if (emulatorId != Guid.Empty && options.All(option => option.Id != emulatorId))
        {
            options.Add(new DesktopEmulatorOption(emulatorId, $"Missing emulator ({emulatorId})"));
        }

        Emulators = options;
        selectedEmulator = Emulators.FirstOrDefault(option => option.Id == emulatorId);
        RefreshEmulatorProfiles(emulatorProfileId);
        MoveUpCommand = new RelayCommand(() => moveUp?.Invoke(this));
        MoveDownCommand = new RelayCommand(() => moveDown?.Invoke(this));
        SelectPathCommand = new RelayCommand(() => selectPath?.Invoke(this));
        SelectWorkingDirectoryCommand = new RelayCommand(() => selectWorkingDirectory?.Invoke(this));
        SelectTrackingPathCommand = new RelayCommand(() => selectTrackingPath?.Invoke(this));
        TestScriptCommand = new RelayCommand(() => testScript?.Invoke(this));
        RemoveCommand = new RelayCommand(() => remove?.Invoke(this));
    }

    public GameAction ToGameAction() => new()
    {
        Name = Name?.Trim(),
        Type = Type,
        IsPlayAction = IsPlayAction,
        Path = NullIfWhiteSpace(Path),
        Arguments = NullIfWhiteSpace(Arguments),
        WorkingDir = NullIfWhiteSpace(WorkingDir),
        Script = NullIfWhiteSpace(Script),
        TrackingMode = TrackingMode,
        TrackingPath = NullIfWhiteSpace(TrackingPath),
        InitialTrackingDelay = InitialTrackingDelay,
        TrackingFrequency = TrackingFrequency,
        EmulatorId = selectedEmulator?.Id ?? emulatorId,
        EmulatorProfileId = selectedEmulatorProfile?.Id ?? emulatorProfileId,
        OverrideDefaultArgs = OverrideDefaultArgs,
        AdditionalArguments = NullIfWhiteSpace(AdditionalArguments)
    };

    private void RefreshEmulatorProfiles(string preferredId)
    {
        var profiles = new List<DesktopEmulatorProfileOption>
        {
            new(null, "Select profile at launch")
        };
        if (selectedEmulator?.Profiles != null)
        {
            profiles.AddRange(selectedEmulator.Profiles);
        }

        if (!string.IsNullOrWhiteSpace(preferredId) && profiles.All(profile => profile.Id != preferredId))
        {
            profiles.Add(new DesktopEmulatorProfileOption(preferredId, $"Missing profile ({preferredId})"));
        }

        EmulatorProfiles = profiles;
        emulatorProfileId = preferredId;
        selectedEmulatorProfile = profiles.FirstOrDefault(profile => profile.Id == preferredId) ?? profiles[0];
        OnPropertyChanged(nameof(EmulatorProfiles));
        OnPropertyChanged(nameof(SelectedEmulatorProfile));
    }

    private void RaiseTypeState()
    {
        OnPropertyChanged(nameof(IsFileAction));
        OnPropertyChanged(nameof(IsUrlAction));
        OnPropertyChanged(nameof(IsEmulatorAction));
        OnPropertyChanged(nameof(IsScriptAction));
        OnPropertyChanged(nameof(ShowPath));
        OnPropertyChanged(nameof(ShowTracking));
        OnPropertyChanged(nameof(ShowTrackingPath));
    }

    private static string NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
