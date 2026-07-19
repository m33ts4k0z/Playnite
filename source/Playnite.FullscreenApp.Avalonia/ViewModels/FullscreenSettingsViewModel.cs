using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenSettingsViewModel : INotifyPropertyChanged
{
    private readonly Action onSaved;
    private bool isVisible;
    private bool restartRequired;
    private IFullscreenSettingsSection selectedSection;

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<IFullscreenSettingsSection> Sections { get; }
    public FullscreenGeneralSettingsSection General { get; }
    public FullscreenInputSettingsSection Input { get; }
    public FullscreenAudioSettingsSection Audio { get; }
    public FullscreenLayoutSettingsSection Layout { get; }
    public FullscreenVisualSettingsSection Visuals { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }

    public FullscreenSettingsViewModel(Services.FullscreenSettings settings, Action onSaved)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.onSaved = onSaved ?? (() => { });
        General = new FullscreenGeneralSettingsSection(settings);
        Input = new FullscreenInputSettingsSection(settings);
        Audio = new FullscreenAudioSettingsSection(settings);
        Layout = new FullscreenLayoutSettingsSection(settings);
        Visuals = new FullscreenVisualSettingsSection(settings);
        Sections = new ObservableCollection<IFullscreenSettingsSection>
        {
            General, Input, Audio, Layout, Visuals
        };
        selectedSection = Sections[0];
        SaveCommand = new RelayCommand(Save, () => IsVisible);
        CancelCommand = new RelayCommand(Close, () => IsVisible);
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

    public bool RestartRequired
    {
        get => restartRequired;
        private set => SetField(ref restartRequired, value);
    }

    public IFullscreenSettingsSection SelectedSection
    {
        get => selectedSection;
        set => SetField(ref selectedSection, value);
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

        RestartRequired = Sections.Aggregate(
            false,
            (required, section) => section.Save().RestartRequired || required);
        onSaved();
        IsVisible = false;
    }

    public void Close()
    {
        if (IsVisible)
        {
            IsVisible = false;
        }
    }

    public IReadOnlyList<FullscreenSettingsSectionSelfCheckResult> RunSelfChecks() =>
        Sections.Select(section => section.SelfCheck()).ToList();

    private void RaiseCommandStates()
    {
        ((RelayCommand)SaveCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CancelCommand).RaiseCanExecuteChanged();
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
