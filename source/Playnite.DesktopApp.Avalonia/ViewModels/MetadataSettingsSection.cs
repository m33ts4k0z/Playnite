using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using Playnite.Metadata;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class MetadataSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly Func<IReadOnlyList<MetadataPlugin>> metadataPlugins;
    private MetadataDownloaderSettings workingSettings;
    private MetadataFieldSourceRow selectedField;
    private MetadataSourceChoice selectedSource;
    private bool downloadBackgroundsImmediately;
    private AgeRatingOrg ageRatingOrgPriority;
    private string webImageSearchIconTerm;
    private string webImageSearchCoverTerm;
    private string webImageSearchBackgroundTerm;
    private global::Playnite.WebImageSearchSource defaultWebImageSource;
    private bool perFieldSettingsChanged;

    public override string Key => "Metadata";
    public override string Title => "Library — Metadata";
    public override global::Avalonia.Controls.Control Content { get; }
    public ObservableCollection<MetadataFieldSourceRow> Fields { get; } = new();
    public IReadOnlyList<AgeRatingOrg> AgeRatingOrganizations { get; } = Enum.GetValues<AgeRatingOrg>();
    public IReadOnlyList<global::Playnite.WebImageSearchSource> WebImageSources { get; } =
        Enum.GetValues<global::Playnite.WebImageSearchSource>();
    public ICommand MoveSourceUpCommand { get; }
    public ICommand MoveSourceDownCommand { get; }

    public bool DownloadBackgroundsImmediately { get => downloadBackgroundsImmediately; set => SetField(ref downloadBackgroundsImmediately, value); }
    public AgeRatingOrg AgeRatingOrgPriority { get => ageRatingOrgPriority; set => SetField(ref ageRatingOrgPriority, value); }
    public string WebImageSearchIconTerm { get => webImageSearchIconTerm; set => SetField(ref webImageSearchIconTerm, value); }
    public string WebImageSearchCoverTerm { get => webImageSearchCoverTerm; set => SetField(ref webImageSearchCoverTerm, value); }
    public string WebImageSearchBackgroundTerm { get => webImageSearchBackgroundTerm; set => SetField(ref webImageSearchBackgroundTerm, value); }
    public global::Playnite.WebImageSearchSource DefaultWebImageSource { get => defaultWebImageSource; set => SetField(ref defaultWebImageSource, value); }
    public MetadataFieldSourceRow SelectedField
    {
        get => selectedField;
        set
        {
            if (SetField(ref selectedField, value))
            {
                SelectedSource = null;
                RaiseMoveCommands();
            }
        }
    }
    public MetadataSourceChoice SelectedSource
    {
        get => selectedSource;
        set
        {
            if (SetField(ref selectedSource, value))
            {
                RaiseMoveCommands();
            }
        }
    }

    public MetadataSettingsSection(
        DesktopSettings settings,
        Func<IReadOnlyList<MetadataPlugin>> metadataPlugins)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.metadataPlugins = metadataPlugins ?? (() => Array.Empty<MetadataPlugin>());
        MoveSourceUpCommand = new AppRelayCommand(() => MoveSelectedSource(-1), CanMoveSourceUp);
        MoveSourceDownCommand = new AppRelayCommand(() => MoveSelectedSource(1), CanMoveSourceDown);
        Content = new MetadataSettingsView { DataContext = this };
    }

    public override void Open()
    {
        workingSettings = MetadataSettingsUtilities.Clone(settings.MetadataSettings);
        DownloadBackgroundsImmediately = settings.DownloadBackgroundsImmediately;
        AgeRatingOrgPriority = settings.AgeRatingOrgPriority;
        WebImageSearchIconTerm = settings.WebImageSearchIconTerm;
        WebImageSearchCoverTerm = settings.WebImageSearchCoverTerm;
        WebImageSearchBackgroundTerm = settings.WebImageSearchBackgroundTerm;
        DefaultWebImageSource = settings.DefaultWebImageSource;
        RebuildFields();
        perFieldSettingsChanged = false;
    }

    public override SettingsSectionValidationResult Validate()
    {
        if (Fields.Any(field => field.Import && field.Sources.All(source => !source.IsEnabled)))
        {
            return new(false, "Every enabled metadata field must have at least one source.");
        }

        return SettingsSectionValidationResult.Valid;
    }

    public override SettingsSectionSaveResult Save()
    {
        foreach (var field in Fields)
        {
            var target = MetadataSettingsUtilities.GetField(workingSettings, field.Field);
            target.Import = field.Import;
            target.Sources = field.Sources.Where(source => source.IsEnabled).Select(source => source.Id).ToList();
        }

        settings.MetadataSettings = MetadataSettingsUtilities.Clone(workingSettings);
        if (perFieldSettingsChanged)
        {
            settings.UsePerFieldMetadataSettings = true;
        }
        settings.DownloadBackgroundsImmediately = DownloadBackgroundsImmediately;
        settings.AgeRatingOrgPriority = AgeRatingOrgPriority;
        settings.WebImageSearchIconTerm = WebImageSearchIconTerm ?? string.Empty;
        settings.WebImageSearchCoverTerm = WebImageSearchCoverTerm ?? string.Empty;
        settings.WebImageSearchBackgroundTerm = WebImageSearchBackgroundTerm ?? string.Empty;
        settings.DefaultWebImageSource = DefaultWebImageSource;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this && Fields.Count == MetadataSettingsUtilities.SupportedFields.Count &&
            Validate().IsValid;
        return new(Key, valid, valid
            ? $"{Fields.Count} metadata fields expose ordered source priorities"
            : "metadata field/source working copies are incomplete");
    }

    private void RebuildFields()
    {
        Fields.Clear();
        var plugins = (metadataPlugins() ?? Array.Empty<MetadataPlugin>())
            .Where(plugin => plugin != null)
            .GroupBy(plugin => plugin.Id)
            .Select(group => group.First())
            .OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        foreach (var field in MetadataSettingsUtilities.SupportedFields)
        {
            var available = new List<MetadataSourceChoice>
            {
                new(Guid.Empty, "Official library metadata", false)
            };
            available.AddRange(plugins
                .Where(plugin => plugin.SupportedFields?.Contains(field) == true)
                .Select(plugin => new MetadataSourceChoice(plugin.Id, plugin.Name, false)));
            var fieldSettings = MetadataSettingsUtilities.GetField(workingSettings, field);
            var choices = new List<MetadataSourceChoice>();
            foreach (var sourceId in fieldSettings.Sources ?? new List<Guid>())
            {
                var known = available.FirstOrDefault(source => source.Id == sourceId);
                choices.Add(new MetadataSourceChoice(
                    sourceId,
                    known?.Name ?? $"Unavailable source ({sourceId})",
                    true));
            }

            choices.AddRange(available
                .Where(source => choices.All(existing => existing.Id != source.Id))
                .Select(source => new MetadataSourceChoice(source.Id, source.Name, false)));
            Fields.Add(new MetadataFieldSourceRow(field, fieldSettings.Import, choices));
            Fields[^1].PropertyChanged += (_, _) => perFieldSettingsChanged = true;
            foreach (var choice in Fields[^1].Sources)
            {
                choice.PropertyChanged += (_, _) => perFieldSettingsChanged = true;
            }
        }

        SelectedField = Fields.FirstOrDefault();
    }

    private bool CanMoveSourceUp() =>
        SelectedField != null && SelectedSource != null && SelectedField.Sources.IndexOf(SelectedSource) > 0;

    private bool CanMoveSourceDown() =>
        SelectedField != null && SelectedSource != null &&
        SelectedField.Sources.IndexOf(SelectedSource) is var index && index >= 0 && index < SelectedField.Sources.Count - 1;

    private void MoveSelectedSource(int offset)
    {
        if (SelectedField == null || SelectedSource == null)
        {
            return;
        }

        var index = SelectedField.Sources.IndexOf(SelectedSource);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= SelectedField.Sources.Count)
        {
            return;
        }

        SelectedField.Sources.Move(index, target);
        perFieldSettingsChanged = true;
        RaiseMoveCommands();
    }

    private void RaiseMoveCommands()
    {
        ((AppRelayCommand)MoveSourceUpCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)MoveSourceDownCommand).RaiseCanExecuteChanged();
    }
}

public sealed class MetadataFieldSourceRow : INotifyPropertyChanged
{
    private bool import;

    public MetadataField Field { get; }
    public string Name => Field.ToString();
    public ObservableCollection<MetadataSourceChoice> Sources { get; }
    public bool Import { get => import; set => SetField(ref import, value); }
    public event PropertyChangedEventHandler PropertyChanged;

    public MetadataFieldSourceRow(MetadataField field, bool import, IEnumerable<MetadataSourceChoice> sources)
    {
        Field = field;
        this.import = import;
        Sources = new ObservableCollection<MetadataSourceChoice>(sources);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

public sealed class MetadataSourceChoice : INotifyPropertyChanged
{
    private bool isEnabled;

    public Guid Id { get; }
    public string Name { get; }
    public bool IsEnabled { get => isEnabled; set => SetField(ref isEnabled, value); }
    public event PropertyChangedEventHandler PropertyChanged;

    public MetadataSourceChoice(Guid id, string name, bool isEnabled)
    {
        Id = id;
        Name = name;
        this.isEnabled = isEnabled;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
