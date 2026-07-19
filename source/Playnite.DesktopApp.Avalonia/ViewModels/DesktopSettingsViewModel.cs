using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.Avalonia.App.Services;
using Playnite.Database;
using Playnite.Plugins;
using Playnite.SDK.Plugins;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

/// <summary>
/// Composes independent settings sections and owns only overlay-level state.
/// Section working copies, persistence and validation live in their modules.
/// </summary>
public sealed class DesktopSettingsViewModel : INotifyPropertyChanged
{
    private readonly Action onSaved;
    private readonly Action<string, bool> showMessage;
    private bool isVisible;
    private ISettingsSection selectedSection;
    private bool restartRequired;

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<ISettingsSection> Sections { get; }
    public GeneralSettingsSection General { get; }
    public AppearanceSettingsSection Appearance { get; }
    public AppearanceGeneralSettingsSection AppearanceGeneral { get; }
    public AppearanceGridViewSettingsSection AppearanceGridView { get; }
    public AppearanceListViewSettingsSection AppearanceListView { get; }
    public AppearanceDetailsViewSettingsSection AppearanceDetailsView { get; }
    public AppearanceLayoutSettingsSection AppearanceLayout { get; }
    public AppearanceAdvancedSettingsSection AppearanceAdvanced { get; }
    public AppearanceTopPanelSettingsSection AppearanceTopPanel { get; }
    public MetadataSettingsSection Metadata { get; }
    public SortingSettingsSection Sorting { get; }
    public ImportExclusionsSettingsSection ImportExclusions { get; }
    public SearchSettingsSection Search { get; }
    public UpdatesSettingsSection Updates { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public DesktopSettingsViewModel(
        DesktopSettings settings,
        GameDatabase database,
        Func<IReadOnlyList<MetadataPlugin>> metadataPlugins,
        Func<IReadOnlyList<LoadedPlugin>> plugins,
        Func<IReadOnlyList<V7LoadedPlugin>> v7Plugins,
        DesktopUpdateCoordinator updates,
        Action libraryUpdated,
        Action onSaved,
        Action<string, bool> showMessage)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.onSaved = onSaved ?? (() => { });
        this.showMessage = showMessage ?? ((_, _) => { });

        General = new GeneralSettingsSection(settings, this.showMessage);
        Appearance = new AppearanceSettingsSection(settings);
        AppearanceGeneral = new AppearanceGeneralSettingsSection(settings);
        AppearanceGridView = new AppearanceGridViewSettingsSection(settings);
        AppearanceListView = new AppearanceListViewSettingsSection(settings);
        AppearanceDetailsView = new AppearanceDetailsViewSettingsSection(settings);
        AppearanceLayout = new AppearanceLayoutSettingsSection(settings);
        AppearanceAdvanced = new AppearanceAdvancedSettingsSection(settings);
        AppearanceTopPanel = new AppearanceTopPanelSettingsSection(settings);
        Metadata = new MetadataSettingsSection(settings, metadataPlugins);
        Sorting = new SortingSettingsSection(settings, database, libraryUpdated, this.showMessage);
        ImportExclusions = new ImportExclusionsSettingsSection(database);
        Search = new SearchSettingsSection(settings, plugins, v7Plugins);
        Updates = new UpdatesSettingsSection(settings, updates);
        Sections = new ObservableCollection<ISettingsSection>
        {
            General,
            Appearance,
            AppearanceGeneral,
            AppearanceGridView,
            AppearanceListView,
            AppearanceDetailsView,
            AppearanceLayout,
            AppearanceAdvanced,
            AppearanceTopPanel,
            Metadata,
            Sorting,
            ImportExclusions,
            Search,
            Updates
        };
        selectedSection = Sections[0];
        SaveCommand = new AppRelayCommand(Save, () => IsVisible);
        CancelCommand = new AppRelayCommand(Close, () => IsVisible);
    }

    public bool IsVisible
    {
        get => isVisible;
        private set
        {
            if (SetField(ref isVisible, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public ISettingsSection SelectedSection
    {
        get => selectedSection;
        set => SetField(ref selectedSection, value);
    }

    public bool RestartRequired
    {
        get => restartRequired;
        private set => SetField(ref restartRequired, value);
    }

    public bool Open()
    {
        if (IsVisible)
        {
            return false;
        }

        foreach (var section in Sections)
        {
            section.Open();
        }

        RestartRequired = false;
        SelectedSection = Sections[0];
        IsVisible = true;
        return true;
    }

    public void Save()
    {
        if (!IsVisible)
        {
            return;
        }

        foreach (var section in Sections)
        {
            var validation = section.Validate();
            if (!validation.IsValid)
            {
                SelectedSection = section;
                showMessage(validation.Message, true);
                return;
            }
        }

        RestartRequired = Sections.Aggregate(
            false,
            (required, section) => section.Save().RestartRequired || required);
        onSaved();
        showMessage(
            RestartRequired
                ? "Settings saved. Restart Playnite to apply all changes."
                : "Settings saved.",
            false);
        IsVisible = false;
    }

    public void Close()
    {
        if (IsVisible)
        {
            IsVisible = false;
        }
    }

    public IReadOnlyList<SettingsSectionSelfCheckResult> RunSelfChecks() =>
        Sections.Select(section => section.SelfCheck()).ToList();

    private void RaiseCommandStates()
    {
        ((AppRelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((AppRelayCommand)CancelCommand).RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
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
