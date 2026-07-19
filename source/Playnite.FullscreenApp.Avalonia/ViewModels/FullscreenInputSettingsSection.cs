using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenInputSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private bool swapConfirmCancelButtons;

    public override string Key => "Input";
    public override string Title => "Input";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool SwapConfirmCancelButtons
    {
        get => swapConfirmCancelButtons;
        set => SetField(ref swapConfirmCancelButtons, value);
    }

    public FullscreenInputSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenInputSettingsView { DataContext = this };
    }

    public override void Open()
    {
        swapConfirmCancelButtons = settings.SwapConfirmCancelButtons;
        OnPropertyChanged(nameof(SwapConfirmCancelButtons));
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.SwapConfirmCancelButtons = SwapConfirmCancelButtons;
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Content is FullscreenInputSettingsView, "Input working-copy view is registered");
}
