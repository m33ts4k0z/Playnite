using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Playnite.Avalonia.App.ViewModels;
using Playnite.Common;
using Playnite.DesktopApp.Avalonia.Controls;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Emulators;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

internal enum EditorMediaKind
{
    Cover,
    Background,
    Icon
}

public sealed partial class DesktopGameEditorViewModel
{
    private const string ImageFileFilter =
        "Image files|*.bmp;*.gif;*.ico;*.jpeg;*.jpg;*.png;*.webp|All files|*.*";
    private const string AllFileFilter = "All files|*.*";
    private readonly HashSet<string> editorTemporaryFiles = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private CancellationTokenSource editorMediaCancellation;
    private CancellationTokenSource editorMetadataCancellation;
    private Func<List<MetadataPlugin>> editorMetadataPlugins = () => new List<MetadataPlugin>();
    private Func<List<LibraryPlugin>> editorLibraryPlugins = () => new List<LibraryPlugin>();
    private Func<string, Game, DesktopScriptExecutionResult> scriptTester;
    private DesktopEditorMetadataSourceOption selectedEditorMetadataSource;
    private DesktopWebImageResult selectedWebImage;
    private WebImageSearchSource selectedWebImageSource;
    private bool isWebImageSearchVisible;
    private bool isWebImageSearchRunning;
    private bool isDownloadingEditorMetadata;
    private bool isMetadataComparisonVisible;
    private bool isCalculatingInstallSize;
    private string webImageSearchTerm;
    private string webImageSearchMessage;
    private string metadataComparisonTitle;
    private string editorGameId;
    private string editorDatabaseId;
    private string editorModified;
    private string editorPluginName;
    private string coverMediaInfo;
    private string backgroundMediaInfo;
    private string iconMediaInfo;
    private EditorMediaKind webImageTarget;
    private int webImageSearchGeneration;

    public ObservableCollection<DesktopEditorMetadataSourceOption> EditorMetadataSources { get; } = new();
    public ObservableCollection<DesktopWebImageResult> WebImageResults { get; } = new();
    public ObservableCollection<DesktopMetadataComparisonItem> MetadataComparisonItems { get; } = new();
    public IReadOnlyList<WebImageSearchSource> WebImageSources { get; } = Enum.GetValues<WebImageSearchSource>();

    public DesktopEditorMetadataSourceOption SelectedEditorMetadataSource
    {
        get => selectedEditorMetadataSource;
        set
        {
            if (SetField(ref selectedEditorMetadataSource, value))
            {
                ((RelayCommand)DownloadEditorMetadataCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public DesktopWebImageResult SelectedWebImage
    {
        get => selectedWebImage;
        set
        {
            if (SetField(ref selectedWebImage, value))
            {
                ((RelayCommand)UseSelectedWebImageCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public WebImageSearchSource SelectedWebImageSource
    {
        get => selectedWebImageSource;
        set
        {
            if (SetField(ref selectedWebImageSource, value))
            {
                settings.DefaultWebImageSource = value;
                settingsChanged();
            }
        }
    }

    public bool IsWebImageSearchVisible
    {
        get => isWebImageSearchVisible;
        private set => SetField(ref isWebImageSearchVisible, value);
    }

    public bool IsWebImageSearchRunning
    {
        get => isWebImageSearchRunning;
        private set
        {
            if (SetField(ref isWebImageSearchRunning, value))
            {
                ((RelayCommand)SearchWebImagesCommand).RaiseCanExecuteChanged();
                ((RelayCommand)UseSelectedWebImageCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsDownloadingEditorMetadata
    {
        get => isDownloadingEditorMetadata;
        private set
        {
            if (SetField(ref isDownloadingEditorMetadata, value))
            {
                ((RelayCommand)DownloadEditorMetadataCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsMetadataComparisonVisible
    {
        get => isMetadataComparisonVisible;
        private set => SetField(ref isMetadataComparisonVisible, value);
    }

    public bool IsCalculatingInstallSize
    {
        get => isCalculatingInstallSize;
        private set
        {
            if (SetField(ref isCalculatingInstallSize, value))
            {
                ((RelayCommand)CalculateInstallSizeCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string WebImageSearchTerm
    {
        get => webImageSearchTerm;
        set
        {
            if (SetField(ref webImageSearchTerm, value))
            {
                ((RelayCommand)SearchWebImagesCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string WebImageSearchMessage { get => webImageSearchMessage; private set => SetField(ref webImageSearchMessage, value); }
    public string MetadataComparisonTitle { get => metadataComparisonTitle; private set => SetField(ref metadataComparisonTitle, value); }
    public string EditorGameId { get => editorGameId; private set => SetField(ref editorGameId, value); }
    public string EditorDatabaseId { get => editorDatabaseId; private set => SetField(ref editorDatabaseId, value); }
    public string EditorModified { get => editorModified; private set => SetField(ref editorModified, value); }
    public string EditorPluginName { get => editorPluginName; private set => SetField(ref editorPluginName, value); }
    public string CoverMediaInfo { get => coverMediaInfo; private set => SetField(ref coverMediaInfo, value); }
    public string BackgroundMediaInfo { get => backgroundMediaInfo; private set => SetField(ref backgroundMediaInfo, value); }
    public string IconMediaInfo { get => iconMediaInfo; private set => SetField(ref iconMediaInfo, value); }

    public ICommand SelectMediaFileCommand { get; private set; }
    public ICommand SetMediaUrlCommand { get; private set; }
    public ICommand RemoveMediaCommand { get; private set; }
    public ICommand MediaDropCommand { get; private set; }
    public ICommand OpenWebImageSearchCommand { get; private set; }
    public ICommand SearchWebImagesCommand { get; private set; }
    public ICommand UseSelectedWebImageCommand { get; private set; }
    public ICommand CloseWebImageSearchCommand { get; private set; }
    public ICommand DownloadEditorMetadataCommand { get; private set; }
    public ICommand ApplyMetadataComparisonCommand { get; private set; }
    public ICommand CloseMetadataComparisonCommand { get; private set; }
    public ICommand OpenMetadataFolderCommand { get; private set; }
    public ICommand SelectInstallDirectoryCommand { get; private set; }
    public ICommand SelectManualCommand { get; private set; }
    public ICommand CalculateInstallSizeCommand { get; private set; }
    public ICommand TestEditorScriptCommand { get; private set; }

    private void InitializeParityCommands()
    {
        selectedWebImageSource = settings.DefaultWebImageSource;
        SelectMediaFileCommand = new RelayCommand(parameter => SelectMediaFile(ParseMediaKind(parameter)));
        SetMediaUrlCommand = new RelayCommand(async parameter =>
            await SetMediaUrlAsync(ParseMediaKind(parameter)));
        RemoveMediaCommand = new RelayCommand(parameter => SetMediaValue(ParseMediaKind(parameter), string.Empty));
        MediaDropCommand = new RelayCommand(parameter => ApplyMediaDrop(parameter as DesktopMediaDropRequest));
        OpenWebImageSearchCommand = new RelayCommand(async parameter =>
            await OpenWebImageSearchAsync(ParseMediaKind(parameter)));
        SearchWebImagesCommand = new RelayCommand(
            async () => await SearchWebImagesAsync(),
            () => IsWebImageSearchVisible && !IsWebImageSearchRunning && !string.IsNullOrWhiteSpace(WebImageSearchTerm));
        UseSelectedWebImageCommand = new RelayCommand(
            async () => await UseSelectedWebImageAsync(),
            () => IsWebImageSearchVisible && !IsWebImageSearchRunning && SelectedWebImage != null);
        CloseWebImageSearchCommand = new RelayCommand(CloseWebImageSearch);
        DownloadEditorMetadataCommand = new RelayCommand(
            async () => await DownloadEditorMetadataAsync(),
            () => IsSingleEdit && SelectedEditorMetadataSource != null && !IsDownloadingEditorMetadata);
        ApplyMetadataComparisonCommand = new RelayCommand(ApplyMetadataComparison);
        CloseMetadataComparisonCommand = new RelayCommand(() => IsMetadataComparisonVisible = false);
        OpenMetadataFolderCommand = new RelayCommand(OpenMetadataFolder, () => IsSingleEdit);
        SelectInstallDirectoryCommand = new RelayCommand(SelectInstallDirectory);
        SelectManualCommand = new RelayCommand(SelectManual);
        CalculateInstallSizeCommand = new RelayCommand(
            async () => await CalculateInstallSizeAsync(),
            () => IsSingleEdit && !IsCalculatingInstallSize);
        TestEditorScriptCommand = new RelayCommand(parameter => TestEditorScript(parameter as string));
    }

    public void ConfigureMetadataProviders(
        Func<List<MetadataPlugin>> metadataPluginProvider,
        Func<List<LibraryPlugin>> libraryPluginProvider)
    {
        editorMetadataPlugins = metadataPluginProvider ?? (() => new List<MetadataPlugin>());
        editorLibraryPlugins = libraryPluginProvider ?? (() => new List<LibraryPlugin>());
        if (IsVisible)
        {
            RefreshEditorMetadataSources();
            RefreshProvenance();
        }
    }

    public void ConfigureScriptTester(Func<string, Game, DesktopScriptExecutionResult> testScript) =>
        scriptTester = testScript;

    private void RefreshEditorParityState(IReadOnlyList<Game> games)
    {
        editorMediaCancellation?.Cancel();
        editorMediaCancellation?.Dispose();
        editorMediaCancellation = new CancellationTokenSource();
        IsWebImageSearchVisible = false;
        IsMetadataComparisonVisible = false;
        WebImageResults.Clear();
        MetadataComparisonItems.Clear();
        RefreshEditorMetadataSources();
        RefreshProvenance(games[0]);
        RefreshMediaInfo(EditorMediaKind.Cover);
        RefreshMediaInfo(EditorMediaKind.Background);
        RefreshMediaInfo(EditorMediaKind.Icon);
        ((RelayCommand)DownloadEditorMetadataCommand).RaiseCanExecuteChanged();
        ((RelayCommand)OpenMetadataFolderCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CalculateInstallSizeCommand).RaiseCanExecuteChanged();
    }

    private void RefreshEditorMetadataSources()
    {
        var selectedId = SelectedEditorMetadataSource?.Id;
        var selectedLibrary = SelectedEditorMetadataSource?.IsLibrarySource == true;
        EditorMetadataSources.Clear();
        if (IsSingleEdit && editingGameIds.Count == 1)
        {
            var game = database?.Games[editingGameIds[0]];
            var library = (editorLibraryPlugins() ?? new List<LibraryPlugin>())
                .FirstOrDefault(plugin => plugin != null && plugin.Id == game?.PluginId);
            if (library != null)
            {
                EditorMetadataSources.Add(new DesktopEditorMetadataSourceOption(
                    library.Id,
                    library.Name,
                    "Official library metadata",
                    true));
            }

            foreach (var plugin in (editorMetadataPlugins() ?? new List<MetadataPlugin>())
                         .Where(plugin => plugin != null)
                         .GroupBy(plugin => plugin.Id)
                         .Select(group => group.First())
                         .OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var fields = plugin.SupportedFields?.Count > 0
                    ? $"{plugin.SupportedFields.Count:N0} supported fields"
                    : "Metadata provider";
                EditorMetadataSources.Add(new DesktopEditorMetadataSourceOption(
                    plugin.Id,
                    plugin.Name,
                    fields,
                    false));
            }
        }

        SelectedEditorMetadataSource = EditorMetadataSources.FirstOrDefault(source =>
            source.Id == selectedId && source.IsLibrarySource == selectedLibrary) ??
            EditorMetadataSources.FirstOrDefault();
    }

    private void RefreshProvenance(Game game = null)
    {
        game ??= IsSingleEdit && editingGameIds.Count == 1 ? database?.Games[editingGameIds[0]] : null;
        EditorGameId = game?.GameId ?? string.Empty;
        EditorDatabaseId = game?.Id.ToString() ?? string.Empty;
        EditorModified = game?.Modified?.ToString("G", CultureInfo.CurrentCulture) ?? string.Empty;
        EditorPluginName = game == null
            ? string.Empty
            : (editorLibraryPlugins() ?? new List<LibraryPlugin>())
                .FirstOrDefault(plugin => plugin != null && plugin.Id == game.PluginId)?.Name ??
              (game.PluginId == Guid.Empty ? "Custom game" : $"Unavailable plugin ({game.PluginId})");
    }

    private void SelectMediaFile(EditorMediaKind kind)
    {
        var selected = dialogs()?.SelectFiles(ImageFileFilter, false, GetInitialDirectory(GetMediaValue(kind)))
            ?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SetMediaValue(kind, selected);
        }
    }

    private async Task SetMediaUrlAsync(EditorMediaKind kind)
    {
        var result = dialogs()?.ShowInput(
            "Enter the HTTP or HTTPS address of the image.",
            $"Set {GetMediaLabel(kind)} from URL",
            string.Empty);
        if (result?.Result != true || string.IsNullOrWhiteSpace(result.SelectedString))
        {
            return;
        }

        if (!Uri.TryCreate(result.SelectedString.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            ValidationMessage = "The image address must be an HTTP or HTTPS URL.";
            return;
        }

        await DownloadMediaFileAsync(kind, new MetadataFile(uri.AbsoluteUri));
    }

    private void ApplyMediaDrop(DesktopMediaDropRequest request)
    {
        if (request == null || !File.Exists(request.Path))
        {
            return;
        }

        SetMediaValue(ParseMediaKind(request.Target), request.Path);
    }

    private async Task OpenWebImageSearchAsync(EditorMediaKind kind)
    {
        if (!IsSingleEdit)
        {
            return;
        }

        webImageTarget = kind;
        SelectedWebImageSource = settings.DefaultWebImageSource;
        WebImageSearchTerm = BuildWebImageSearchTerm(kind);
        WebImageSearchMessage = string.Empty;
        WebImageResults.Clear();
        SelectedWebImage = null;
        IsWebImageSearchVisible = true;
        await SearchWebImagesAsync();
    }

    private async Task SearchWebImagesAsync()
    {
        if (!IsWebImageSearchVisible || IsWebImageSearchRunning || string.IsNullOrWhiteSpace(WebImageSearchTerm))
        {
            return;
        }

        IsWebImageSearchRunning = true;
        var generation = ++webImageSearchGeneration;
        WebImageSearchMessage = "Searching for images…";
        WebImageResults.Clear();
        SelectedWebImage = null;
        try
        {
            using var downloader = new GoogleImageDownloader();
            var term = WebImageSearchTerm.Trim();
            var source = SelectedWebImageSource;
            var images = source == WebImageSearchSource.Google
                ? await downloader.GetImages(term, SafeSearchSettings.Default)
                : await Task.Run(() => downloader.GetDdgImages(term));
            if (!IsWebImageSearchVisible || generation != webImageSearchGeneration)
            {
                return;
            }

            foreach (var image in images.Where(image => image != null &&
                         !string.IsNullOrWhiteSpace(image.ImageUrl)))
            {
                WebImageResults.Add(new DesktopWebImageResult(image));
            }

            WebImageSearchMessage = WebImageResults.Count == 0
                ? "No images were returned. Try a different search term or source."
                : $"{WebImageResults.Count:N0} images found.";
        }
        catch (Exception exception)
        {
            if (generation == webImageSearchGeneration)
            {
                WebImageSearchMessage = $"Image search failed: {exception.Message}";
            }
        }
        finally
        {
            if (generation == webImageSearchGeneration)
            {
                IsWebImageSearchRunning = false;
            }
        }
    }

    private async Task UseSelectedWebImageAsync()
    {
        var selected = SelectedWebImage;
        if (selected == null)
        {
            return;
        }

        if (!await TryDownloadWebImageAsync(webImageTarget, selected.ImageUrl) &&
            !string.IsNullOrWhiteSpace(selected.ThumbnailUrl))
        {
            await TryDownloadWebImageAsync(webImageTarget, selected.ThumbnailUrl);
        }
    }

    private async Task<bool> TryDownloadWebImageAsync(EditorMediaKind kind, string url)
    {
        var editorToken = editorMediaCancellation?.Token ?? CancellationToken.None;
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(editorToken);
        requestCancellation.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var downloaded = await GetLocalMetadataFileAsync(new MetadataFile(url), requestCancellation.Token);
            if (downloaded == null)
            {
                return false;
            }

            SetMediaValue(kind, downloaded.Path);
            CloseWebImageSearch();
            return true;
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException && !editorToken.IsCancellationRequested)
            {
                WebImageSearchMessage = "The selected image download timed out.";
            }
            else if (exception is not OperationCanceledException)
            {
                WebImageSearchMessage = $"The selected image could not be downloaded: {exception.Message}";
            }
            return false;
        }
    }

    private void CloseWebImageSearch()
    {
        webImageSearchGeneration++;
        IsWebImageSearchRunning = false;
        IsWebImageSearchVisible = false;
        WebImageResults.Clear();
        SelectedWebImage = null;
    }

    private string BuildWebImageSearchTerm(EditorMediaKind kind)
    {
        var template = kind switch
        {
            EditorMediaKind.Cover => settings.WebImageSearchCoverTerm,
            EditorMediaKind.Background => settings.WebImageSearchBackgroundTerm,
            _ => settings.WebImageSearchIconTerm
        };
        template = string.IsNullOrWhiteSpace(template) ? $"{{Name}} {GetMediaLabel(kind)}" : template;
        var releaseYear = ReleaseDate?.Split('-', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var platform = Platforms.FirstOrDefault(option => option.IsSelected == true)?.Name ?? string.Empty;
        return template
            .Replace("{Name}", Name ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{ReleaseYear}", releaseYear, StringComparison.OrdinalIgnoreCase)
            .Replace("{Platform}", platform, StringComparison.OrdinalIgnoreCase);
    }

    private async Task DownloadMediaFileAsync(EditorMediaKind kind, MetadataFile file)
    {
        var editorToken = editorMediaCancellation?.Token ?? CancellationToken.None;
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(editorToken);
        requestCancellation.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var downloaded = await GetLocalMetadataFileAsync(file, requestCancellation.Token);
            if (downloaded == null)
            {
                ValidationMessage = $"The {GetMediaLabel(kind)} could not be downloaded.";
                return;
            }

            SetMediaValue(kind, downloaded.Path);
            ValidationMessage = string.Empty;
        }
        catch (Exception exception)
        {
            if (exception is OperationCanceledException && !editorToken.IsCancellationRequested)
            {
                ValidationMessage = $"The {GetMediaLabel(kind)} download timed out.";
            }
            else if (exception is not OperationCanceledException)
            {
                ValidationMessage = $"The {GetMediaLabel(kind)} could not be downloaded: {exception.Message}";
            }
        }
    }

    private async Task<MetadataFile> GetLocalMetadataFileAsync(MetadataFile file, CancellationToken cancelToken)
    {
        if (file?.HasImageData != true)
        {
            return null;
        }

        var localPath = await Task.Run(() => file.GetLocalFile(cancelToken), cancelToken);
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            return null;
        }

        TrackTemporaryFile(localPath);
        try
        {
            cancelToken.ThrowIfCancellationRequested();
            await Task.Run(() =>
            {
                using var image = new Bitmap(localPath);
                _ = image.PixelSize;
            }, cancelToken);
            cancelToken.ThrowIfCancellationRequested();
            return new MetadataFile(localPath);
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
        {
            RemoveTemporaryFile(localPath);
            throw;
        }
        catch
        {
            RemoveTemporaryFile(localPath);
            throw;
        }
    }

    private void TrackTemporaryFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return;
        }

        var tempRoot = Path.GetFullPath(PlaynitePaths.TempPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(
                tempRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            lock (editorTemporaryFiles)
            {
                editorTemporaryFiles.Add(fullPath);
            }
        }
    }

    private void CleanupEditorTemporaryFiles()
    {
        editorMediaCancellation?.Cancel();
        editorMediaCancellation?.Dispose();
        editorMediaCancellation = null;
        editorMetadataCancellation?.Cancel();
        webImageSearchGeneration++;
        List<string> files;
        lock (editorTemporaryFiles)
        {
            files = editorTemporaryFiles.ToList();
            editorTemporaryFiles.Clear();
        }

        foreach (var file in files)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (IOException exception)
            {
                setStatus($"Temporary editor media could not be removed: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                setStatus($"Temporary editor media could not be removed: {exception.Message}");
            }
        }
    }

    private void RemoveTemporaryFile(string path)
    {
        path = string.IsNullOrWhiteSpace(path) ? path : Path.GetFullPath(path);
        var tracked = false;
        lock (editorTemporaryFiles)
        {
            tracked = editorTemporaryFiles.Remove(path);
        }

        if (!tracked || !File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException exception)
        {
            setStatus($"Temporary editor media could not be removed: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            setStatus($"Temporary editor media could not be removed: {exception.Message}");
        }
    }

    private void RefreshMediaInfo(EditorMediaKind kind)
    {
        var value = GetMediaValue(kind);
        var info = GetMediaInfo(value);
        switch (kind)
        {
            case EditorMediaKind.Cover:
                CoverMediaInfo = info;
                break;
            case EditorMediaKind.Background:
                BackgroundMediaInfo = info;
                break;
            case EditorMediaKind.Icon:
                IconMediaInfo = info;
                break;
        }
    }

    private string GetMediaInfo(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "No image selected";
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return "Remote image";
        }

        var path = ResolvePreviewPath(value);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return "Image file not found";
        }

        try
        {
            using var image = new Bitmap(path);
            var length = new FileInfo(path).Length;
            return $"{image.PixelSize.Width} × {image.PixelSize.Height} · {FormatBytes(length)}";
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return $"Image details unavailable: {exception.Message}";
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var value = (double)Math.Max(bytes, 0);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private async Task DownloadEditorMetadataAsync()
    {
        if (!IsSingleEdit || SelectedEditorMetadataSource == null || IsDownloadingEditorMetadata)
        {
            return;
        }

        var source = SelectedEditorMetadataSource;
        var game = database.Games[editingGameIds[0]]?.GetCopy();
        if (game == null)
        {
            ValidationMessage = "The game no longer exists in the library.";
            return;
        }

        IsDownloadingEditorMetadata = true;
        editorMetadataCancellation?.Cancel();
        editorMetadataCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        editorMetadataCancellation = cancellation;
        var token = cancellation.Token;
        ValidationMessage = string.Empty;
        try
        {
            var metadata = await Task.Run(() => FetchEditorMetadata(source, game, token), token);
            token.ThrowIfCancellationRequested();
            if (!IsVisible || editingGameIds.Count != 1 || editingGameIds[0] != game.Id)
            {
                return;
            }

            if (metadata == null)
            {
                ValidationMessage = $"{source.Name} did not return metadata for this game.";
                return;
            }

            await LocalizeMetadataImagesAsync(metadata, token);
            token.ThrowIfCancellationRequested();
            if (!IsVisible || editingGameIds.Count != 1 || editingGameIds[0] != game.Id)
            {
                return;
            }

            BuildMetadataComparison(metadata, source.Name);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            ValidationMessage = $"Metadata download from {source.Name} failed: {exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(editorMetadataCancellation, cancellation))
            {
                editorMetadataCancellation = null;
            }
            cancellation.Dispose();
            IsDownloadingEditorMetadata = false;
        }
    }

    internal async Task<bool> DownloadEditorMetadataForTestAsync()
    {
        await DownloadEditorMetadataAsync();
        return IsMetadataComparisonVisible;
    }

    private GameMetadata FetchEditorMetadata(
        DesktopEditorMetadataSourceOption source,
        Game game,
        CancellationToken cancelToken)
    {
        if (source.IsLibrarySource)
        {
            var plugin = (editorLibraryPlugins() ?? new List<LibraryPlugin>())
                .FirstOrDefault(candidate => candidate != null && candidate.Id == source.Id);
            using var provider = plugin?.GetMetadataDownloader();
            return provider?.GetMetadata(game);
        }

        var metadataPlugin = (editorMetadataPlugins() ?? new List<MetadataPlugin>())
            .FirstOrDefault(candidate => candidate != null && candidate.Id == source.Id);
        using var metadataProvider = metadataPlugin?.GetMetadataProvider(new MetadataRequestOptions(game, false));
        if (metadataProvider == null)
        {
            return null;
        }

        var fields = metadataProvider.AvailableFields ?? new List<MetadataField>();
#if WINDOWS
        var args = new GetMetadataFieldArgs { CancelToken = cancelToken };
#else
        cancelToken.ThrowIfCancellationRequested();
        var args = new GetMetadataFieldArgs();
#endif
        bool Has(MetadataField field) => fields.Contains(field);
        return new GameMetadata
        {
            Name = Has(MetadataField.Name) ? metadataProvider.GetName(args) : null,
            Genres = Has(MetadataField.Genres) ? ToSet(metadataProvider.GetGenres(args)) : null,
            ReleaseDate = Has(MetadataField.ReleaseDate) ? metadataProvider.GetReleaseDate(args) : null,
            Developers = Has(MetadataField.Developers) ? ToSet(metadataProvider.GetDevelopers(args)) : null,
            Publishers = Has(MetadataField.Publishers) ? ToSet(metadataProvider.GetPublishers(args)) : null,
            Tags = Has(MetadataField.Tags) ? ToSet(metadataProvider.GetTags(args)) : null,
            Features = Has(MetadataField.Features) ? ToSet(metadataProvider.GetFeatures(args)) : null,
            Description = Has(MetadataField.Description) ? metadataProvider.GetDescription(args) : null,
            Links = Has(MetadataField.Links) ? metadataProvider.GetLinks(args)?.Where(link => link != null).ToList() : null,
            CriticScore = Has(MetadataField.CriticScore) ? metadataProvider.GetCriticScore(args) : null,
            CommunityScore = Has(MetadataField.CommunityScore) ? metadataProvider.GetCommunityScore(args) : null,
            AgeRatings = Has(MetadataField.AgeRating) ? ToSet(metadataProvider.GetAgeRatings(args)) : null,
            Series = Has(MetadataField.Series) ? ToSet(metadataProvider.GetSeries(args)) : null,
            Regions = Has(MetadataField.Region) ? ToSet(metadataProvider.GetRegions(args)) : null,
            Platforms = Has(MetadataField.Platform) ? ToSet(metadataProvider.GetPlatforms(args)) : null,
            Icon = Has(MetadataField.Icon) ? metadataProvider.GetIcon(args) : null,
            CoverImage = Has(MetadataField.CoverImage) ? metadataProvider.GetCoverImage(args) : null,
            BackgroundImage = Has(MetadataField.BackgroundImage) ? metadataProvider.GetBackgroundImage(args) : null,
            InstallSize = Has(MetadataField.InstallSize) ? metadataProvider.GetInstallSize(args) : null
        };
    }

    private static HashSet<MetadataProperty> ToSet(IEnumerable<MetadataProperty> properties) =>
        properties?.Where(property => property != null).ToHashSet();

    private async Task LocalizeMetadataImagesAsync(GameMetadata metadata, CancellationToken cancelToken)
    {
        metadata.Icon = await TryLocalizeMetadataImageAsync(metadata.Icon, "icon", cancelToken);
        metadata.CoverImage = await TryLocalizeMetadataImageAsync(metadata.CoverImage, "cover image", cancelToken);
        metadata.BackgroundImage = await TryLocalizeMetadataImageAsync(
            metadata.BackgroundImage,
            "background image",
            cancelToken);
    }

    private async Task<MetadataFile> TryLocalizeMetadataImageAsync(
        MetadataFile file,
        string field,
        CancellationToken cancelToken)
    {
        try
        {
            return await GetLocalMetadataFileAsync(file, cancelToken);
        }
        catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            setStatus($"Downloaded metadata {field} could not be prepared: {exception.Message}");
            return null;
        }
    }

    private void BuildMetadataComparison(GameMetadata metadata, string sourceName)
    {
        MetadataComparisonItems.Clear();
        AddTextComparison("Name", Name, metadata.Name, value => Name = value);
        AddPropertyComparison("Genres", nameof(Genres), Genres, metadata.Genres);
        AddTextComparison(
            "Release date",
            ReleaseDate,
            metadata.ReleaseDate?.Serialize(),
            value => ReleaseDate = value);
        AddPropertyComparison("Developers", nameof(Developers), Developers, metadata.Developers);
        AddPropertyComparison("Publishers", nameof(Publishers), Publishers, metadata.Publishers);
        AddPropertyComparison("Tags", nameof(Tags), Tags, metadata.Tags);
        AddPropertyComparison("Features", nameof(Features), Features, metadata.Features);
        AddTextComparison("Description", Description, metadata.Description, value => Description = value, true);
        AddLinksComparison(metadata.Links);
        AddNullableNumberComparison("Critic score", CriticScore, metadata.CriticScore, value => CriticScore = value);
        AddNullableNumberComparison(
            "Community score",
            CommunityScore,
            metadata.CommunityScore,
            value => CommunityScore = value);
        AddPropertyComparison("Age ratings", nameof(AgeRatings), AgeRatings, metadata.AgeRatings);
        AddPropertyComparison("Series", nameof(Series), Series, metadata.Series);
        AddPropertyComparison("Regions", nameof(Regions), Regions, metadata.Regions);
        AddPropertyComparison("Platforms", nameof(Platforms), Platforms, metadata.Platforms);
        AddMediaComparison("Icon", EditorMediaKind.Icon, metadata.Icon?.Path);
        AddMediaComparison("Cover image", EditorMediaKind.Cover, metadata.CoverImage?.Path);
        AddMediaComparison("Background image", EditorMediaKind.Background, metadata.BackgroundImage?.Path);
        if (metadata.InstallSize.HasValue)
        {
            AddTextComparison(
                "Install size",
                InstallSize,
                metadata.InstallSize.Value.ToString(CultureInfo.InvariantCulture),
                value => InstallSize = value);
        }

        if (MetadataComparisonItems.Count == 0)
        {
            ValidationMessage = $"{sourceName} returned no metadata that differs from the editor values.";
            return;
        }

        MetadataComparisonTitle = $"Compare metadata from {sourceName}";
        IsMetadataComparisonVisible = true;
    }

    private void AddTextComparison(
        string field,
        string current,
        string downloaded,
        Action<string> apply,
        bool abbreviate = false)
    {
        if (string.IsNullOrWhiteSpace(downloaded) ||
            string.Equals(current?.Trim(), downloaded.Trim(), StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        MetadataComparisonItems.Add(new DesktopMetadataComparisonItem(
            field,
            abbreviate ? Abbreviate(current) : current,
            abbreviate ? Abbreviate(downloaded) : downloaded,
            () => apply(downloaded)));
    }

    private void AddNullableNumberComparison(
        string field,
        string current,
        int? downloaded,
        Action<string> apply)
    {
        if (downloaded.HasValue)
        {
            AddTextComparison(
                field,
                current,
                downloaded.Value.ToString(CultureInfo.InvariantCulture),
                apply);
        }
    }

    private void AddPropertyComparison(
        string field,
        string editorField,
        IEnumerable<DesktopMetadataOption> current,
        HashSet<MetadataProperty> downloaded)
    {
        if (downloaded == null || downloaded.Count == 0)
        {
            return;
        }

        var currentText = string.Join(", ", current.Where(option => option.IsSelected == true)
            .Select(option => option.Name)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));
        var downloadedText = string.Join(", ", downloaded.Select(property =>
                GetMetadataPropertyName(editorField, property))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase));
        if (string.IsNullOrWhiteSpace(downloadedText) ||
            string.Equals(currentText, downloadedText, StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        MetadataComparisonItems.Add(new DesktopMetadataComparisonItem(
            field,
            currentText,
            downloadedText,
            () => ApplyMetadataProperties(editorField, downloaded)));
    }

    private void AddLinksComparison(List<Link> downloaded)
    {
        if (downloaded == null || downloaded.Count == 0)
        {
            return;
        }

        var currentText = string.Join(", ", Links.Select(link => $"{link.Name}: {link.Url}"));
        var downloadedText = string.Join(", ", downloaded.Select(link => $"{link.Name}: {link.Url}"));
        if (string.Equals(currentText, downloadedText, StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        MetadataComparisonItems.Add(new DesktopMetadataComparisonItem(
            "Links",
            currentText,
            downloadedText,
            () =>
            {
                Links.Clear();
                foreach (var link in downloaded)
                {
                    AddLinkItem(link);
                }
            }));
    }

    private void AddMediaComparison(string field, EditorMediaKind kind, string downloadedPath)
    {
        if (string.IsNullOrWhiteSpace(downloadedPath))
        {
            return;
        }

        var current = ResolvePreviewPath(GetMediaValue(kind));
        if (PathsReferToSameFile(current, downloadedPath))
        {
            return;
        }

        MetadataComparisonItems.Add(DesktopMetadataComparisonItem.CreateMedia(
            field,
            current,
            downloadedPath,
            () => SetMediaValue(kind, downloadedPath)));
    }

    private static bool PathsReferToSameFile(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            if (string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                return true;
            }

            return File.Exists(first) && File.Exists(second) && FileSystem.AreFileContentsEqual(first, second);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void ApplyMetadataComparison()
    {
        foreach (var item in MetadataComparisonItems.Where(item => item.UseDownloadedValue))
        {
            item.Apply();
        }

        IsMetadataComparisonVisible = false;
        ValidationMessage = string.Empty;
        setStatus("Selected downloaded metadata was applied to the editor. Save to update the library.");
    }

    private void ApplyMetadataProperties(string field, IEnumerable<MetadataProperty> properties)
    {
        var options = GetTaxonomyOptions(field);
        if (options == null)
        {
            return;
        }

        foreach (var option in options)
        {
            option.IsSelected = false;
        }

        foreach (var property in properties)
        {
            if (property is MetadataIdProperty id)
            {
                var existing = options.FirstOrDefault(option => option.Id == id.Id);
                if (existing != null)
                {
                    existing.IsSelected = true;
                }
            }
            else
            {
                var name = GetMetadataPropertyName(field, property);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    AddTaxonomy(field, name, false);
                }
            }
        }
    }

    private string GetMetadataPropertyName(string field, MetadataProperty property)
    {
        if (property is MetadataNameProperty named)
        {
            return named.Name;
        }

        if (property is MetadataIdProperty identified)
        {
            return GetTaxonomyOptions(field)?.FirstOrDefault(option => option.Id == identified.Id)?.Name;
        }

        if (property is MetadataSpecProperty specification)
        {
            if (field == nameof(Platforms))
            {
                return Emulation.Platforms.FirstOrDefault(item =>
                    item.Id == specification.Id || item.Name == specification.Id)?.Name ?? specification.Id;
            }

            if (field == nameof(Regions))
            {
                return Emulation.Regions.FirstOrDefault(item =>
                    item.Id == specification.Id || item.Name == specification.Id)?.Name ?? specification.Id;
            }
        }

        return property?.ToString();
    }

    private static string Abbreviate(string value) =>
        string.IsNullOrWhiteSpace(value) || value.Length <= 320 ? value : value[..320] + "…";

    private void OpenMetadataFolder()
    {
        if (!IsSingleEdit || editingGameIds.Count != 1)
        {
            return;
        }

        var path = database.GetFileStoragePath(editingGameIds[0]);
        Explorer.OpenDirectory(path);
    }

    private void SelectInstallDirectory()
    {
        var selected = dialogs()?.SelectFolder(GetInitialDirectory(InstallDirectory));
        if (!string.IsNullOrWhiteSpace(selected))
        {
            InstallDirectory = selected;
        }
    }

    private void SelectManual()
    {
        var selected = dialogs()?.SelectFiles(AllFileFilter, false, GetInitialDirectory(Manual))?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            Manual = selected;
        }
    }

    private void SelectRomPath(DesktopRomEditorItem rom)
    {
        var selected = dialogs()?.SelectFiles(AllFileFilter, false, GetInitialDirectory(rom?.Path))?.FirstOrDefault();
        if (rom != null && !string.IsNullOrWhiteSpace(selected))
        {
            rom.Path = selected;
        }
    }

    private void SelectActionPath(DesktopGameActionEditorItem action)
    {
        if (action?.IsFileAction != true)
        {
            return;
        }

        var selected = dialogs()?.SelectFiles(AllFileFilter, false, GetInitialDirectory(action.Path))?.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(selected))
        {
            action.Path = selected;
        }
    }

    private void SelectActionWorkingDirectory(DesktopGameActionEditorItem action)
    {
        var selected = dialogs()?.SelectFolder(GetInitialDirectory(action?.WorkingDir));
        if (action != null && !string.IsNullOrWhiteSpace(selected))
        {
            action.WorkingDir = selected;
        }
    }

    private void SelectActionTrackingPath(DesktopGameActionEditorItem action)
    {
        var selected = dialogs()?.SelectFolder(GetInitialDirectory(action?.TrackingPath));
        if (action != null && !string.IsNullOrWhiteSpace(selected))
        {
            action.TrackingPath = selected;
        }
    }

    private async Task CalculateInstallSizeAsync()
    {
        if (!IsSingleEdit || IsCalculatingInstallSize)
        {
            return;
        }

        var game = database.Games[editingGameIds[0]]?.GetCopy();
        if (game == null)
        {
            return;
        }

        game.InstallDirectory = NullIfWhiteSpace(InstallDirectory);
        game.IsInstalled = IsInstalled;
        game.Roms = new ObservableCollection<GameRom>(Roms.Select(rom => rom.ToGameRom()));
        game.GameActions = new ObservableCollection<GameAction>(GameActions.Select(action => action.ToGameAction()));
        IsCalculatingInstallSize = true;
        try
        {
            var size = await Task.Run(() => new GameInstallSizeCalculator(database).Calculate(game, false));
            if (size.HasValue)
            {
                InstallSize = size.Value.ToString(CultureInfo.InvariantCulture);
                ValidationMessage = string.Empty;
                setStatus($"Calculated install size: {FormatBytes(checked((long)Math.Min(size.Value, long.MaxValue)))}.");
            }
            else
            {
                ValidationMessage = "Install size could not be calculated. Check the installed state and file paths.";
            }
        }
        catch (Exception exception)
        {
            ValidationMessage = $"Install size calculation failed: {exception.Message}";
        }
        finally
        {
            IsCalculatingInstallSize = false;
        }
    }

    internal async Task<string> CalculateInstallSizeForTestAsync()
    {
        await CalculateInstallSizeAsync();
        return InstallSize;
    }

    private void TestEditorScript(string target)
    {
        var script = target switch
        {
            "Pre" => PreScript,
            "Started" => GameStartedScript,
            "Post" => PostScript,
            _ => string.Empty
        };
        ReportScriptTest(script);
    }

    private void TestActionScript(DesktopGameActionEditorItem action) => ReportScriptTest(action?.Script);

    private void ReportScriptTest(string script)
    {
        var game = IsSingleEdit && editingGameIds.Count == 1
            ? database.Games[editingGameIds[0]]?.GetCopy()
            : null;
        if (game != null)
        {
            game.Name = Name;
            game.InstallDirectory = NullIfWhiteSpace(InstallDirectory);
            game.PreScript = NullIfWhiteSpace(PreScript);
            game.PostScript = NullIfWhiteSpace(PostScript);
            game.GameStartedScript = NullIfWhiteSpace(GameStartedScript);
            game.Roms = new ObservableCollection<GameRom>(Roms.Select(rom => rom.ToGameRom()));
            game.GameActions = new ObservableCollection<GameAction>(GameActions.Select(action => action.ToGameAction()));
        }

        var result = scriptTester?.Invoke(script, game) ??
            new DesktopScriptExecutionResult(false, false, "The script test service is not available.");
        if (result.Success)
        {
            ValidationMessage = string.Empty;
            setStatus(result.Message);
        }
        else
        {
            ValidationMessage = result.Message;
        }
    }

    private static string GetInitialDirectory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Directory.Exists(value))
        {
            return value;
        }

        return Path.IsPathFullyQualified(value) ? Path.GetDirectoryName(value) : null;
    }

    private string GetMediaValue(EditorMediaKind kind) => kind switch
    {
        EditorMediaKind.Cover => CoverImage,
        EditorMediaKind.Background => BackgroundImage,
        _ => Icon
    };

    private void SetMediaValue(EditorMediaKind kind, string value)
    {
        switch (kind)
        {
            case EditorMediaKind.Cover:
                CoverImage = value;
                break;
            case EditorMediaKind.Background:
                BackgroundImage = value;
                break;
            case EditorMediaKind.Icon:
                Icon = value;
                break;
        }
    }

    private static EditorMediaKind ParseMediaKind(object value) =>
        Enum.TryParse(value?.ToString(), true, out EditorMediaKind kind) ? kind : EditorMediaKind.Cover;

    private static string GetMediaLabel(EditorMediaKind kind) => kind switch
    {
        EditorMediaKind.Cover => "cover image",
        EditorMediaKind.Background => "background image",
        _ => "icon"
    };
}

public sealed class DesktopEditorMetadataSourceOption
{
    public Guid Id { get; }
    public string Name { get; }
    public string Detail { get; }
    public bool IsLibrarySource { get; }

    public DesktopEditorMetadataSourceOption(Guid id, string name, string detail, bool isLibrarySource)
    {
        Id = id;
        Name = name ?? string.Empty;
        Detail = detail ?? string.Empty;
        IsLibrarySource = isLibrarySource;
    }

    public override string ToString() => Name;
}

public sealed class DesktopWebImageResult
{
    public string ImageUrl { get; }
    public string ThumbnailUrl { get; }
    public string PreviewPath => string.IsNullOrWhiteSpace(ThumbnailUrl) ? ImageUrl : ThumbnailUrl;
    public string Size { get; }

    public DesktopWebImageResult(GoogleImage image)
    {
        ImageUrl = image?.ImageUrl ?? string.Empty;
        ThumbnailUrl = image?.ThumbUrl ?? string.Empty;
        Size = image?.Size ?? string.Empty;
    }
}

public sealed class DesktopMetadataComparisonItem : INotifyPropertyChanged
{
    private readonly Action apply;
    private bool useDownloadedValue = true;

    public event PropertyChangedEventHandler PropertyChanged;
    public string Field { get; }
    public string CurrentValue { get; }
    public string DownloadedValue { get; }
    public bool IsImage { get; }
    public string CurrentImagePath { get; }
    public string DownloadedImagePath { get; }

    public bool UseDownloadedValue
    {
        get => useDownloadedValue;
        set
        {
            if (useDownloadedValue == value)
            {
                return;
            }

            useDownloadedValue = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UseDownloadedValue)));
        }
    }

    public DesktopMetadataComparisonItem(
        string field,
        string currentValue,
        string downloadedValue,
        Action apply)
        : this(field, currentValue, downloadedValue, false, null, null, apply)
    {
    }

    private DesktopMetadataComparisonItem(
        string field,
        string currentValue,
        string downloadedValue,
        bool isImage,
        string currentImagePath,
        string downloadedImagePath,
        Action apply)
    {
        Field = field ?? string.Empty;
        CurrentValue = currentValue ?? "Not set";
        DownloadedValue = downloadedValue ?? "Not set";
        IsImage = isImage;
        CurrentImagePath = currentImagePath;
        DownloadedImagePath = downloadedImagePath;
        this.apply = apply ?? (() => { });
    }

    public static DesktopMetadataComparisonItem CreateMedia(
        string field,
        string currentPath,
        string downloadedPath,
        Action apply) =>
        new(field, currentPath, downloadedPath, true, currentPath, downloadedPath, apply);

    public void Apply() => apply();
}
