using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenGeneralSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private bool showHiddenGames;

    public override string Key => "General";
    public override string Title => "General";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool ShowHiddenGames
    {
        get => showHiddenGames;
        set => SetField(ref showHiddenGames, value);
    }

    public FullscreenGeneralSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenGeneralSettingsView { DataContext = this };
    }

    public override void Open()
    {
        showHiddenGames = settings.ShowHiddenGames;
        OnPropertyChanged(nameof(ShowHiddenGames));
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.ShowHiddenGames = ShowHiddenGames;
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Content is FullscreenGeneralSettingsView, "General working-copy view is registered");
}
