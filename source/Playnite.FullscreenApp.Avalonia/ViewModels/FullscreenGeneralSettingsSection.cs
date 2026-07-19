using Playnite.FullscreenApp.Avalonia.Services;
using Playnite.FullscreenApp.Avalonia.Views.Settings;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed class FullscreenGeneralSettingsSection : FullscreenSettingsSectionBase
{
    private readonly FullscreenSettings settings;
    private bool showHiddenGames;
    private bool usePrimaryDisplay;
    private bool showClock;
    private bool showBattery;
    private bool showBatteryPercentage;
    private bool minimizeAfterGameStartup;
    private FullscreenMonitorOption selectedMonitor;

    public IReadOnlyList<FullscreenMonitorOption> Monitors { get; private set; } =
        new[] { new FullscreenMonitorOption(-1, "System default display") };

    public override string Key => "General";
    public override string Title => "General";
    public override global::Avalonia.Controls.Control Content { get; }

    public bool ShowHiddenGames
    {
        get => showHiddenGames;
        set => SetField(ref showHiddenGames, value);
    }

    public bool UsePrimaryDisplay
    {
        get => usePrimaryDisplay;
        set => SetField(ref usePrimaryDisplay, value);
    }

    public bool ShowClock
    {
        get => showClock;
        set => SetField(ref showClock, value);
    }

    public bool ShowBattery
    {
        get => showBattery;
        set => SetField(ref showBattery, value);
    }

    public bool ShowBatteryPercentage
    {
        get => showBatteryPercentage;
        set => SetField(ref showBatteryPercentage, value);
    }

    public bool MinimizeAfterGameStartup
    {
        get => minimizeAfterGameStartup;
        set => SetField(ref minimizeAfterGameStartup, value);
    }

    public FullscreenMonitorOption SelectedMonitor
    {
        get => selectedMonitor;
        set => SetField(ref selectedMonitor, value);
    }

    public FullscreenGeneralSettingsSection(FullscreenSettings settings)
    {
        this.settings = settings;
        Content = new FullscreenGeneralSettingsView { DataContext = this };
    }

    public override void Open()
    {
        showHiddenGames = settings.ShowHiddenGames;
        usePrimaryDisplay = settings.UsePrimaryDisplay;
        showClock = settings.ShowClock;
        showBattery = settings.ShowBattery;
        showBatteryPercentage = settings.ShowBatteryPercentage;
        minimizeAfterGameStartup = settings.MinimizeAfterGameStartup;
        selectedMonitor = Monitors.FirstOrDefault(option => option.Index == settings.Monitor) ?? Monitors[0];
        OnPropertyChanged(nameof(ShowHiddenGames));
        OnPropertyChanged(nameof(UsePrimaryDisplay));
        OnPropertyChanged(nameof(ShowClock));
        OnPropertyChanged(nameof(ShowBattery));
        OnPropertyChanged(nameof(ShowBatteryPercentage));
        OnPropertyChanged(nameof(MinimizeAfterGameStartup));
        OnPropertyChanged(nameof(SelectedMonitor));
    }

    public override FullscreenSettingsSectionSaveResult Save()
    {
        settings.ShowHiddenGames = ShowHiddenGames;
        settings.UsePrimaryDisplay = UsePrimaryDisplay;
        settings.ShowClock = ShowClock;
        settings.ShowBattery = ShowBattery;
        settings.ShowBatteryPercentage = ShowBatteryPercentage;
        settings.MinimizeAfterGameStartup = MinimizeAfterGameStartup;
        settings.Monitor = SelectedMonitor?.Index ?? -1;
        return FullscreenSettingsSectionSaveResult.Saved;
    }

    public void SetMonitors(IReadOnlyList<string> displayNames)
    {
        Monitors = displayNames.Count == 0
            ? new[] { new FullscreenMonitorOption(-1, "System default display") }
            : displayNames.Select((name, index) => new FullscreenMonitorOption(index, name)).ToList();
        OnPropertyChanged(nameof(Monitors));
        selectedMonitor = Monitors.FirstOrDefault(option => option.Index == settings.Monitor) ?? Monitors[0];
        OnPropertyChanged(nameof(SelectedMonitor));
    }

    public override FullscreenSettingsSectionSelfCheckResult SelfCheck() =>
        new(Key, Content is FullscreenGeneralSettingsView, "General working-copy view is registered");
}

public sealed record FullscreenMonitorOption(int Index, string Name)
{
    public override string ToString() => Name;
}
