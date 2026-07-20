using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using Avalonia.Threading;
using Playnite.Common;
#if WINDOWS
using Playnite.Common.Media.Icons;
#endif
using Playnite.Database;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.SDK;
using Playnite.SDK.Models;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;
using InstalledProgram = Playnite.Common.Program;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class DesktopInstalledGameImportViewModel : INotifyPropertyChanged
{
    private static readonly ILogger logger = LogManager.GetLogger();
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
    private static readonly string[] supportedExecutableExtensions = OperatingSystem.IsWindows()
        ? [".exe", ".bat", ".lnk"]
        : [".AppImage", ".appimage", ".sh", ".desktop", ".exe", ".bat"];
    private readonly GameDatabase database;
    private readonly DesktopSettings settings;
    private readonly DesktopMetadataDownloadViewModel metadataDownload;
    private readonly Action synchronizeLibrary;
    private readonly Action<string, bool> showMessage;
    private readonly List<DesktopInstalledProgramOption> allPrograms = new();
    private Func<CancellationToken, Task<IReadOnlyList<DesktopDetectedProgram>>> detectPrograms;
    private Func<string, CancellationToken, Task<IReadOnlyList<InstalledProgram>>> scanFolder;
    private Func<string, InstalledProgram> readProgram;
    private Func<Task<string>> chooseFolder = () => Task.FromResult<string>(null);
    private Func<Task<string>> chooseExecutable = () => Task.FromResult<string>(null);
    private Action notifyLibraryUpdated = () => { };
    private HashSet<string> importedExecutables = new(PathComparer);
    private CancellationTokenSource cancellationSource;
    private bool isVisible;
    private bool isRunning;
    private bool hideImported = true;
    private bool selectAll;
    private string folderPath = string.Empty;
    private string executablePath = string.Empty;
    private int progressValue;
    private int progressTotal;
    private string progressText = string.Empty;
    private string errorText;

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler SettingsChanged;

    public ObservableCollection<DesktopInstalledProgramOption> Programs { get; } = new();

    public bool IsVisible
    {
        get => isVisible;
        private set => SetField(ref isVisible, value);
    }

    public bool IsRunning
    {
        get => isRunning;
        private set
        {
            if (SetField(ref isRunning, value))
            {
                OnPropertyChanged(nameof(CanConfigure));
                RaiseCommandStates();
            }
        }
    }

    public bool CanConfigure => !IsRunning;

    public bool HideImported
    {
        get => hideImported;
        set
        {
            if (SetField(ref hideImported, value))
            {
                RefreshVisiblePrograms();
            }
        }
    }

    public bool SelectAll
    {
        get => selectAll;
        set
        {
            if (!SetField(ref selectAll, value))
            {
                return;
            }

            foreach (var program in Programs)
            {
                program.IsSelected = value;
            }
        }
    }

    public string FolderPath
    {
        get => folderPath;
        set
        {
            if (SetField(ref folderPath, value?.Trim() ?? string.Empty))
            {
                ((AppRelayCommand)ScanFolderCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string ExecutablePath
    {
        get => executablePath;
        set
        {
            if (SetField(ref executablePath, value?.Trim() ?? string.Empty))
            {
                ((AppRelayCommand)AddExecutableCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool DownloadMetadataOnImport
    {
        get => settings.DownloadMetadataOnImport;
        set
        {
            if (settings.DownloadMetadataOnImport == value)
            {
                return;
            }

            settings.DownloadMetadataOnImport = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string ProgramSummary => allPrograms.Count == Programs.Count
        ? $"{Programs.Count:N0} program(s) found"
        : $"{Programs.Count:N0} of {allPrograms.Count:N0} program(s) shown";
    public int ProgressValue { get => progressValue; private set => SetField(ref progressValue, value); }
    public int ProgressTotal { get => progressTotal; private set => SetField(ref progressTotal, value); }
    public string ProgressText { get => progressText; private set => SetField(ref progressText, value); }
    public string ErrorText { get => errorText; private set => SetField(ref errorText, value); }

    public ICommand DetectInstalledCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand ScanFolderCommand { get; }
    public ICommand BrowseExecutableCommand { get; }
    public ICommand AddExecutableCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CloseCommand { get; }

    public DesktopInstalledGameImportViewModel(
        GameDatabase database,
        DesktopSettings settings,
        DesktopMetadataDownloadViewModel metadataDownload,
        Action synchronizeLibrary,
        Action<string, bool> showMessage)
    {
        this.database = database;
        this.settings = settings ?? new DesktopSettings();
        this.metadataDownload = metadataDownload ?? throw new ArgumentNullException(nameof(metadataDownload));
        this.synchronizeLibrary = synchronizeLibrary ?? (() => { });
        this.showMessage = showMessage ?? ((_, _) => { });
        detectPrograms = DetectProgramsAsync;
        scanFolder = async (path, token) =>
            await Programs2Facade.ScanFolderAsync(path, token);
        readProgram = Playnite.Common.Programs.GetProgramData;

        DetectInstalledCommand = new AppRelayCommand(
            async () => await DetectInstalledAsync(),
            () => IsVisible && !IsRunning);
        BrowseFolderCommand = new AppRelayCommand(
            async () => await BrowseFolderAsync(),
            () => IsVisible && !IsRunning);
        ScanFolderCommand = new AppRelayCommand(
            async () => await ScanFolderAsync(FolderPath),
            () => IsVisible && !IsRunning && !string.IsNullOrWhiteSpace(FolderPath));
        BrowseExecutableCommand = new AppRelayCommand(
            async () => await BrowseExecutableAsync(),
            () => IsVisible && !IsRunning);
        AddExecutableCommand = new AppRelayCommand(
            () => AddExecutable(ExecutablePath),
            () => IsVisible && !IsRunning && !string.IsNullOrWhiteSpace(ExecutablePath));
        ImportCommand = new AppRelayCommand(
            async () => await ImportSelectedAsync(),
            () => IsVisible && !IsRunning && Programs.Any(program => program.IsSelected));
        CancelCommand = new AppRelayCommand(Cancel, () => IsRunning);
        CloseCommand = new AppRelayCommand(Close, () => !IsRunning);
    }

    public void ConfigureFilePickers(Func<Task<string>> folderPicker, Func<Task<string>> executablePicker)
    {
        chooseFolder = folderPicker ?? (() => Task.FromResult<string>(null));
        chooseExecutable = executablePicker ?? (() => Task.FromResult<string>(null));
    }

    public void ConfigureLibraryUpdated(Action libraryUpdated) =>
        notifyLibraryUpdated = libraryUpdated ?? (() => { });

    internal void ConfigureProvidersForTesting(
        Func<CancellationToken, Task<IReadOnlyList<DesktopDetectedProgram>>> detector,
        Func<string, CancellationToken, Task<IReadOnlyList<InstalledProgram>>> folderScanner,
        Func<string, InstalledProgram> programReader,
        Action libraryUpdated)
    {
        detectPrograms = detector ?? DetectProgramsAsync;
        scanFolder = folderScanner ?? ((path, token) => Programs2Facade.ScanFolderAsync(path, token));
        readProgram = programReader ?? Playnite.Common.Programs.GetProgramData;
        notifyLibraryUpdated = libraryUpdated ?? (() => { });
    }

    public bool Open()
    {
        if (database == null || IsVisible || IsRunning)
        {
            return false;
        }

        importedExecutables = new HashSet<string>(database.GetImportedExeFiles(), PathComparer);
        allPrograms.Clear();
        Programs.Clear();
        selectAll = false;
        OnPropertyChanged(nameof(SelectAll));
        OnPropertyChanged(nameof(ProgramSummary));
        ErrorText = null;
        ProgressValue = 0;
        ProgressTotal = 0;
        ProgressText = "Choose a discovery source to find installed games.";
        IsVisible = true;
        RaiseCommandStates();
        return true;
    }

    public void Close()
    {
        if (!IsRunning)
        {
            IsVisible = false;
        }
    }

    public void Cancel()
    {
        if (IsRunning && cancellationSource?.IsCancellationRequested == false)
        {
            cancellationSource.Cancel();
            ProgressText = "Cancelling installed-game task…";
        }
    }

    public async Task<bool> DetectInstalledAsync()
    {
        return await DiscoverAsync(
            "Detecting installed desktop and Microsoft Store programs…",
            token => detectPrograms(token),
            "Installed-program detection");
    }

    public async Task<bool> ScanFolderAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            ErrorText = "Enter an existing folder to scan.";
            return false;
        }

        FolderPath = Path.GetFullPath(path);
        return await DiscoverAsync(
            $"Scanning {FolderPath} for executables…",
            async token => (await scanFolder(FolderPath, token))
                .Where(program => program != null)
                .Select(program => new DesktopDetectedProgram(program, DesktopInstalledProgramType.Win32))
                .ToList(),
            "Executable folder scan");
    }

    public bool AddExecutable(string path)
    {
        if (IsRunning)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            ErrorText = "Enter an existing executable, batch file, or shortcut.";
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        if (!supportedExecutableExtensions.Contains(Path.GetExtension(fullPath), StringComparer.OrdinalIgnoreCase))
        {
            ErrorText = OperatingSystem.IsWindows()
                ? "Only .exe, .bat, and .lnk files can be imported."
                : "Choose an AppImage, shell script, desktop entry, or executable file.";
            return false;
        }

        try
        {
            var program = readProgram(fullPath);
            if (string.Equals(Path.GetExtension(fullPath), ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var shortcutName = Path.GetFileNameWithoutExtension(fullPath);
                if (!string.IsNullOrWhiteSpace(shortcutName))
                {
                    program.Name = shortcutName;
                }
            }

            AddOrReplaceProgram(new DesktopDetectedProgram(program, DesktopInstalledProgramType.Win32), true);
            ExecutablePath = fullPath;
            ErrorText = null;
            ProgressText = $"Added {program.Name} to the import list.";
            return true;
        }
        catch (Exception exception)
        {
            logger.Error(exception, $"Failed to read executable import data from {fullPath}.");
            ErrorText = $"Executable could not be read: {exception.Message}";
            return false;
        }
    }

    public async Task<bool> ImportSelectedAsync()
    {
        if (IsRunning)
        {
            return false;
        }

        var selected = Programs.Where(program => program.IsSelected).ToList();
        if (selected.Count == 0)
        {
            ErrorText = "Select at least one program to import.";
            return false;
        }

        ErrorText = null;
        ProgressValue = 0;
        ProgressTotal = selected.Count;
        ProgressText = $"Importing installed games [0/{ProgressTotal}]";
        IsVisible = false;
        IsRunning = true;
        cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;
        var addedGames = new List<Game>();
        var failures = new List<string>();

        try
        {
            var defaultStatusId = database.GetCompletionStatusSettings().DefaultStatus;
            using (database.BufferedUpdate())
            {
                for (var index = 0; index < selected.Count; index++)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    var option = selected[index];
                    ProgressText = $"Importing {option.Name} [{index + 1}/{selected.Count}]";
                    try
                    {
                        var added = database.ImportGame(CreateMetadata(option));
                        if (defaultStatusId != Guid.Empty)
                        {
                            added.CompletionStatusId = defaultStatusId;
                            database.Games.Update(added);
                        }

                        addedGames.Add(added);
                        importedExecutables.Add(GetExecutableIdentity(option.Program));
                    }
                    catch (Exception exception)
                    {
                        logger.Error(exception, $"Failed to import installed program {option.Name}.");
                        failures.Add($"{option.Name}: {exception.Message}");
                    }

                    ProgressValue = index + 1;
                    await Dispatcher.UIThread.InvokeAsync(synchronizeLibrary);
                }
            }

            if (!token.IsCancellationRequested && settings.GameSortingNameAutofill && addedGames.Count > 0)
            {
                SortingNameService.FillMissing(
                    database,
                    addedGames,
                    settings.GameSortingNameRemovedArticles);
            }

            if (!token.IsCancellationRequested && DownloadMetadataOnImport && addedGames.Count > 0)
            {
                ProgressValue = 0;
                ProgressTotal = addedGames.Count;
                ProgressText = $"Downloading metadata for {addedGames.Count:N0} imported game(s)";
                var configured = await metadataDownload.DownloadConfiguredGamesAsync(
                    addedGames,
                    (_, index, total) => Dispatcher.UIThread.Post(() =>
                    {
                        ProgressValue = Math.Min(index + 1, total);
                        ProgressTotal = total;
                        ProgressText = $"Downloading metadata [{ProgressValue}/{ProgressTotal}]";
                    }),
                    token);
                if (!configured)
                {
                    failures.Add("Metadata-on-import is enabled, but no metadata fields or sources are configured.");
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                synchronizeLibrary();
                if (addedGames.Count > 0)
                {
                    notifyLibraryUpdated();
                }

                foreach (var program in allPrograms)
                {
                    program.IsImported = importedExecutables.Contains(GetExecutableIdentity(program.Program));
                }

                RefreshVisiblePrograms();
            });

            if (token.IsCancellationRequested)
            {
                ProgressText = $"Installed-game import cancelled after {addedGames.Count:N0} game(s).";
                showMessage(ProgressText, false);
                return false;
            }

            if (failures.Count > 0)
            {
                ErrorText = string.Join(Environment.NewLine, failures);
                ProgressText = $"Installed-game import completed with {failures.Count:N0} error(s).";
                showMessage($"{ProgressText} {ErrorText}", true);
                return false;
            }

            ProgressValue = ProgressTotal;
            ProgressText = $"Installed-game import finished; {addedGames.Count:N0} game(s) added.";
            showMessage(ProgressText, false);
            IsVisible = false;
            return true;
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Avalonia Desktop installed-game import failed.");
            ErrorText = $"Installed-game import failed: {exception.Message}";
            ProgressText = ErrorText;
            showMessage(ErrorText, true);
            return false;
        }
        finally
        {
            cancellationSource.Dispose();
            cancellationSource = null;
            IsRunning = false;
        }
    }

    private async Task<bool> DiscoverAsync(
        string status,
        Func<CancellationToken, Task<IReadOnlyList<DesktopDetectedProgram>>> discovery,
        string operationName)
    {
        if (IsRunning)
        {
            return false;
        }

        ErrorText = null;
        ProgressValue = 0;
        ProgressTotal = 0;
        ProgressText = status;
        IsRunning = true;
        cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;

        try
        {
            var detected = await discovery(token) ?? Array.Empty<DesktopDetectedProgram>();
            token.ThrowIfCancellationRequested();
            ReplacePrograms(detected);
            ProgressValue = Programs.Count;
            ProgressTotal = Programs.Count;
            ProgressText = allPrograms.Count == 0
                ? $"{operationName} finished without finding any programs."
                : $"{operationName} found {allPrograms.Count:N0} program(s).";
            return true;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            ProgressText = $"{operationName} cancelled.";
            return false;
        }
        catch (Exception exception)
        {
            logger.Error(exception, $"{operationName} failed.");
            ErrorText = $"{operationName} failed: {exception.Message}";
            ProgressText = ErrorText;
            showMessage(ErrorText, true);
            return false;
        }
        finally
        {
            cancellationSource.Dispose();
            cancellationSource = null;
            IsRunning = false;
        }
    }

    private async Task BrowseFolderAsync()
    {
        try
        {
            var path = await chooseFolder();
            if (!string.IsNullOrWhiteSpace(path))
            {
                FolderPath = path;
                await ScanFolderAsync(path);
            }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Installed-game folder picker failed.");
            ErrorText = $"Folder selection failed: {exception.Message}";
        }
    }

    private async Task BrowseExecutableAsync()
    {
        try
        {
            var path = await chooseExecutable();
            if (!string.IsNullOrWhiteSpace(path))
            {
                ExecutablePath = path;
                AddExecutable(path);
            }
        }
        catch (Exception exception)
        {
            logger.Error(exception, "Installed-game executable picker failed.");
            ErrorText = $"Executable selection failed: {exception.Message}";
        }
    }

    private static async Task<IReadOnlyList<DesktopDetectedProgram>> DetectProgramsAsync(
        CancellationToken token)
    {
        var detected = new List<DesktopDetectedProgram>();
        var installed = await Playnite.Common.Programs.GetInstalledPrograms(token);
        token.ThrowIfCancellationRequested();
        if (installed != null)
        {
            detected.AddRange(installed
                .Where(program => program != null)
                .Select(program => new DesktopDetectedProgram(program, DesktopInstalledProgramType.Win32)));
        }

        if (OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            var storePrograms = await Task.Run(Playnite.Common.Programs.GetUWPApps, token);
            token.ThrowIfCancellationRequested();
            detected.AddRange(storePrograms
                .Where(program => program != null)
                .Select(program => new DesktopDetectedProgram(program, DesktopInstalledProgramType.MicrosoftStore)));
        }

        return detected;
    }

    private void ReplacePrograms(IEnumerable<DesktopDetectedProgram> detected)
    {
        foreach (var program in allPrograms)
        {
            program.PropertyChanged -= Program_PropertyChanged;
        }

        allPrograms.Clear();
        foreach (var detectedProgram in detected
                     .Where(item => item?.Program != null)
                     .OrderBy(item => item.Program.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            AddOrReplaceProgram(detectedProgram, false, false);
        }

        allPrograms.Sort((left, right) =>
            StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));
        selectAll = false;
        OnPropertyChanged(nameof(SelectAll));
        RefreshVisiblePrograms();
    }

    private void AddOrReplaceProgram(DesktopDetectedProgram detected, bool select, bool refresh = true)
    {
        var identity = GetExecutableIdentity(detected.Program);
        var existing = allPrograms.FirstOrDefault(program =>
            string.Equals(GetExecutableIdentity(program.Program), identity, PathComparison));
        if (existing != null)
        {
            existing.IsSelected |= select;
            if (refresh)
            {
                RefreshVisiblePrograms();
            }

            return;
        }

        var option = new DesktopInstalledProgramOption(
            detected.Program,
            detected.Type,
            importedExecutables.Contains(identity),
            select);
        option.PropertyChanged += Program_PropertyChanged;
        allPrograms.Add(option);
        if (refresh)
        {
            allPrograms.Sort((left, right) =>
                StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name));
            RefreshVisiblePrograms();
        }
    }

    private void RefreshVisiblePrograms()
    {
        Programs.Clear();
        foreach (var program in allPrograms.Where(program => !HideImported || !program.IsImported))
        {
            Programs.Add(program);
        }

        selectAll = Programs.Count > 0 && Programs.All(program => program.IsSelected);
        OnPropertyChanged(nameof(SelectAll));
        OnPropertyChanged(nameof(ProgramSummary));
        ((AppRelayCommand)ImportCommand).RaiseCanExecuteChanged();
    }

    private static GameMetadata CreateMetadata(DesktopInstalledProgramOption option)
    {
        var program = option.Program;
        var gameName = (program.Name ?? Path.GetFileNameWithoutExtension(program.Path) ?? "Imported game")
            .RemoveTrademarks();
        var game = new GameMetadata
        {
            Name = gameName,
            GameId = program.AppId,
            InstallDirectory = program.WorkDir,
            Source = option.Type == DesktopInstalledProgramType.MicrosoftStore
                ? new MetadataNameProperty("Microsoft Store")
                : null,
            IsInstalled = true,
            Platforms = new HashSet<MetadataProperty>
            {
                new MetadataSpecProperty(OperatingSystem.IsWindows() ? "pc_windows" : "pc_linux")
            }
        };

        var actionPath = program.Path;
        if (option.Type == DesktopInstalledProgramType.Win32 && !string.IsNullOrWhiteSpace(program.WorkDir))
        {
            actionPath = program.Path.Replace(
                program.WorkDir.EndWithDirSeparator(),
                ExpandableVariables.InstallationDirectory.EndWithDirSeparator(),
                PathComparison);
        }

        game.GameActions = new List<GameAction>
        {
            new()
            {
                Path = actionPath,
                Arguments = program.Arguments,
                Type = GameActionType.File,
                WorkingDir = option.Type == DesktopInstalledProgramType.Win32
                    ? ExpandableVariables.InstallationDirectory
                    : string.Empty,
                Name = gameName,
                IsPlayAction = true
            }
        };

        game.Icon = CreateIconMetadata(program);

        return game;
    }

    private static MetadataFile CreateIconMetadata(InstalledProgram program)
    {
        var iconPath = ResolveIconPath(program);
        if (iconPath == null)
        {
            return null;
        }

        var extension = Path.GetExtension(iconPath);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ico", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".tga", StringComparison.OrdinalIgnoreCase))
        {
            return new MetadataFile(iconPath);
        }

#if WINDOWS
        using var iconData = new MemoryStream();
        return IconExtractor.ExtractMainIconFromFile(iconPath, iconData)
            ? new MetadataFile($"{Guid.NewGuid():N}.ico", iconData.ToArray())
            : null;
#else
        return null;
#endif
    }

    private static string ResolveIconPath(InstalledProgram program)
    {
        var iconPath = program.Icon;
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return null;
        }

        var match = Regex.Match(iconPath.Trim(), "^\\\"?(.*?)\\\"?,(-?\\d+)$");
        if (match.Success)
        {
            iconPath = match.Groups[1].Value;
        }

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            iconPath = program.Path;
        }

        iconPath = Environment.ExpandEnvironmentVariables(iconPath.Trim().Trim('"'));
        return File.Exists(iconPath) ? iconPath : null;
    }

    private static string GetExecutableIdentity(InstalledProgram program) =>
        (program.Path ?? string.Empty) + (program.Arguments ?? string.Empty);

    private void Program_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopInstalledProgramOption.IsSelected))
        {
            selectAll = Programs.Count > 0 && Programs.All(program => program.IsSelected);
            OnPropertyChanged(nameof(SelectAll));
            ((AppRelayCommand)ImportCommand).RaiseCanExecuteChanged();
        }
    }

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)DetectInstalledCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)BrowseFolderCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)ScanFolderCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)BrowseExecutableCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)AddExecutableCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)ImportCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)CancelCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)CloseCommand).RaiseCanExecuteChanged();
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

    private static class Programs2Facade
    {
        public static async Task<IReadOnlyList<InstalledProgram>> ScanFolderAsync(
            string path,
            CancellationToken token) =>
            await Playnite.Common.Programs.GetExecutablesFromFolder(path, SearchOption.AllDirectories, token) ??
            new List<InstalledProgram>();
    }
}

public enum DesktopInstalledProgramType
{
    Win32,
    MicrosoftStore
}

public sealed record DesktopDetectedProgram(
    InstalledProgram Program,
    DesktopInstalledProgramType Type);

public sealed class DesktopInstalledProgramOption : INotifyPropertyChanged
{
    private bool isSelected;
    private bool isImported;

    public event PropertyChangedEventHandler PropertyChanged;

    public InstalledProgram Program { get; }
    public DesktopInstalledProgramType Type { get; }
    public string Name => Program.Name;
    public string DisplayPath => Type == DesktopInstalledProgramType.MicrosoftStore
        ? "Microsoft Store"
        : Program.Path;
    public string TypeName => Type == DesktopInstalledProgramType.MicrosoftStore
        ? "Microsoft Store app"
        : "Desktop program";
    public string ImportState => IsImported ? "Already in library" : "Ready to import";

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public bool IsImported
    {
        get => isImported;
        set
        {
            if (isImported == value)
            {
                return;
            }

            isImported = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsImported)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImportState)));
        }
    }

    public DesktopInstalledProgramOption(
        InstalledProgram program,
        DesktopInstalledProgramType type,
        bool isImported,
        bool isSelected)
    {
        Program = program ?? throw new ArgumentNullException(nameof(program));
        Type = type;
        this.isImported = isImported;
        this.isSelected = isSelected;
    }
}
