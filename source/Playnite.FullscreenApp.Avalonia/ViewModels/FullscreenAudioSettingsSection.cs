using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenAudioSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private bool audioEnabled;

    public override string Key => "Audio";
    public override string Title => "Audio";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool AudioEnabled
    {
        get => audioEnabled;
        set => SetField(ref audioEnabled, value);
    }

    public FullscreenAudioSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenAudioSettingsView { DataContext = this };
    }

    public override void Open()
    {
        audioEnabled = settings.AudioEnabled;
        OnPropertyChanged(nameof(AudioEnabled));
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.AudioEnabled = AudioEnabled;
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Content is FullscreenAudioSettingsView, "Audio working-copy view is registered");
}
