using System.Windows.Input;
using Playnite.DesktopApp.Avalonia.Services;
using Playnite.DesktopApp.Avalonia.Views.Settings;
using AppRelayCommand = Playnite.Avalonia.App.ViewModels.RelayCommand;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public sealed class ScriptingSettingsSection : SettingsSectionBase
{
    private readonly DesktopSettings settings;
    private readonly Func<string, DesktopScriptExecutionResult> testScript;
    private readonly Action<string, bool> showMessage;
    private string globalPreScript;
    private string globalGameStartedScript;
    private string globalPostScript;
    private string appStartupScript;
    private string appShutdownScript;
    private string statusText = "Scripts have not been tested in this session.";

    public override string Key => "Scripting";
    public override string Title => "Behavior — Scripts";
    public override global::Avalonia.Controls.Control Content { get; }
    public ICommand TestPreScriptCommand { get; }
    public ICommand TestGameStartedScriptCommand { get; }
    public ICommand TestPostScriptCommand { get; }
    public ICommand TestStartupScriptCommand { get; }
    public ICommand TestShutdownScriptCommand { get; }

    public string GlobalPreScript { get => globalPreScript; set => SetField(ref globalPreScript, value); }
    public string GlobalGameStartedScript
    {
        get => globalGameStartedScript;
        set => SetField(ref globalGameStartedScript, value);
    }
    public string GlobalPostScript { get => globalPostScript; set => SetField(ref globalPostScript, value); }
    public string AppStartupScript { get => appStartupScript; set => SetField(ref appStartupScript, value); }
    public string AppShutdownScript { get => appShutdownScript; set => SetField(ref appShutdownScript, value); }
    public string StatusText { get => statusText; private set => SetField(ref statusText, value); }

    public ScriptingSettingsSection(
        DesktopSettings settings,
        Func<string, DesktopScriptExecutionResult> testScript,
        Action<string, bool> showMessage)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.testScript = testScript ?? throw new ArgumentNullException(nameof(testScript));
        this.showMessage = showMessage ?? ((_, _) => { });
        Content = new ScriptingSettingsView { DataContext = this };
        TestPreScriptCommand = CreateTestCommand(() => GlobalPreScript);
        TestGameStartedScriptCommand = CreateTestCommand(() => GlobalGameStartedScript);
        TestPostScriptCommand = CreateTestCommand(() => GlobalPostScript);
        TestStartupScriptCommand = CreateTestCommand(() => AppStartupScript);
        TestShutdownScriptCommand = CreateTestCommand(() => AppShutdownScript);
    }

    public override void Open()
    {
        GlobalPreScript = settings.GlobalPreScript;
        GlobalGameStartedScript = settings.GlobalGameStartedScript;
        GlobalPostScript = settings.GlobalPostScript;
        AppStartupScript = settings.AppStartupScript;
        AppShutdownScript = settings.AppShutdownScript;
    }

    public override SettingsSectionSaveResult Save()
    {
        settings.GlobalPreScript = GlobalPreScript;
        settings.GlobalGameStartedScript = GlobalGameStartedScript;
        settings.GlobalPostScript = GlobalPostScript;
        settings.AppStartupScript = AppStartupScript;
        settings.AppShutdownScript = AppShutdownScript;
        return SettingsSectionSaveResult.Saved;
    }

    public override SettingsSectionSelfCheckResult SelfCheck()
    {
        var valid = Content.DataContext == this &&
            TestPreScriptCommand != null &&
            TestGameStartedScriptCommand != null &&
            TestPostScriptCommand != null &&
            TestStartupScriptCommand != null &&
            TestShutdownScriptCommand != null;
        return new(Key, valid, valid
            ? "five isolated PowerShell stages and their one-shot test actions are available"
            : "script editors or test actions are incomplete");
    }

    private ICommand CreateTestCommand(Func<string> script) => new AppRelayCommand(() =>
    {
        var result = testScript(script());
        StatusText = result.Message;
        showMessage(result.Message, !result.Success);
    });
}
