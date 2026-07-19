using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenAudioSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private bool audioEnabled;
    private int interfaceVolume;
    private int backgroundVolume;
    private bool muteInBackground;

    public override string Key => "Audio";
    public override string Title => "Audio";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool AudioEnabled
    {
        get => audioEnabled;
        set => SetField(ref audioEnabled, value);
    }

    public int InterfaceVolume
    {
        get => interfaceVolume;
        set => SetField(ref interfaceVolume, Math.Clamp(value, 0, 100));
    }

    public int BackgroundVolume
    {
        get => backgroundVolume;
        set => SetField(ref backgroundVolume, Math.Clamp(value, 0, 100));
    }

    public bool MuteInBackground
    {
        get => muteInBackground;
        set => SetField(ref muteInBackground, value);
    }

    public FullscreenAudioSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenAudioSettingsView { DataContext = this };
    }

    public override void Open()
    {
        audioEnabled = settings.AudioEnabled;
        interfaceVolume = settings.InterfaceVolume;
        backgroundVolume = settings.BackgroundVolume;
        muteInBackground = settings.MuteInBackground;
        OnPropertyChanged(nameof(AudioEnabled));
        OnPropertyChanged(nameof(InterfaceVolume));
        OnPropertyChanged(nameof(BackgroundVolume));
        OnPropertyChanged(nameof(MuteInBackground));
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.AudioEnabled = AudioEnabled;
        settings.InterfaceVolume = InterfaceVolume;
        settings.BackgroundVolume = BackgroundVolume;
        settings.MuteInBackground = MuteInBackground;
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Content is FullscreenAudioSettingsView, "Audio working-copy view is registered");
}
