using Playnite.Scripting.PowerShell;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Exceptions;
using Playnite.SDK.Models;

namespace Playnite.DesktopApp.Avalonia.Services;

public sealed record DesktopScriptExecutionResult(bool Success, bool Executed, string Message)
{
    public static DesktopScriptExecutionResult Skipped { get; } =
        new(true, false, "The script is empty.");
}

public sealed class DesktopScriptService
{
    private readonly Func<IPlayniteAPI> api;
    private readonly Func<Game> selectedGame;
    private readonly Func<string, IPowerShellRuntime> createRuntime;

    public DesktopScriptService(
        Func<IPlayniteAPI> api,
        Func<Game> selectedGame,
        Func<string, IPowerShellRuntime> createRuntime = null)
    {
        this.api = api ?? throw new ArgumentNullException(nameof(api));
        this.selectedGame = selectedGame ?? throw new ArgumentNullException(nameof(selectedGame));
        this.createRuntime = createRuntime ?? (name => new PowerShellRuntime(name));
    }

    public DesktopScriptExecutionResult RunApplicationScript(string script, string eventName)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return DesktopScriptExecutionResult.Skipped;
        }

        try
        {
            using var runtime = createRuntime($"Avalonia app {eventName} script");
            runtime.Execute(
                script,
                PlaynitePaths.ProgramPath,
                new Dictionary<string, object> { { "PlayniteApi", api() } });
            return new(true, true, $"The application {eventName} script completed.");
        }
        catch (Exception exception)
        {
            return Failure($"The application {eventName} script failed", exception);
        }
    }

    public DesktopScriptExecutionResult TestGameScript(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return DesktopScriptExecutionResult.Skipped;
        }

        try
        {
            var game = selectedGame() ?? new Game("Test game");
            var startingArgs = new OnGameStartingEventArgs
            {
                Game = game,
                SelectedRomFile = game.Roms?.FirstOrDefault()?.Path,
                SourceAction = game.GameActions?.FirstOrDefault()
            };
            var variables = new Dictionary<string, object>
            {
                { "PlayniteApi", api() },
                { "Game", game.GetCopy() },
                { "StartingArgs", startingArgs },
                { "SourceAction", startingArgs.SourceAction },
                { "SelectedRomFile", startingArgs.SelectedRomFile }
            };
            var workingDirectory = game.ExpandVariables(game.InstallDirectory, true);
            using var runtime = createRuntime("Avalonia test script runtime");
            runtime.Execute(
                game.ExpandVariables(script),
                !string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory)
                    ? workingDirectory
                    : PlaynitePaths.ProgramPath,
                variables);
            return new(true, true, "The script completed successfully.");
        }
        catch (Exception exception)
        {
            return Failure("The test script failed", exception);
        }
    }

    private static DesktopScriptExecutionResult Failure(string message, Exception exception)
    {
        var details = exception is ScriptRuntimeException scriptException &&
            !string.IsNullOrWhiteSpace(scriptException.ScriptStackTrace)
                ? $"{exception.Message}{Environment.NewLine}{scriptException.ScriptStackTrace}"
                : exception.Message;
        return new(false, true, $"{message}: {details}");
    }
}
