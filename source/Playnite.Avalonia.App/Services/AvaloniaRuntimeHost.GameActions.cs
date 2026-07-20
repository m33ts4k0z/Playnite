using Playnite.Common;
using Playnite.Controllers;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.Scripting.PowerShell;

namespace Playnite.Avalonia.App.Services;

public sealed partial class AvaloniaRuntimeHost
{
    public string ActivateGameAction(Game game, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(action);

        using var scriptRuntime = new PowerShellRuntime("Custom action runtime");
        using var controller = new GenericPlayController(database, game, scriptRuntime, globalApi);
        var startingArgs = new OnGameStartingEventArgs
        {
            Game = game,
            SourceAction = action
        };
        if (action.Type == GameActionType.Emulator)
        {
            var emulator = database.Emulators[action.EmulatorId] ?? throw new InvalidOperationException(
                $"Emulator {action.EmulatorId} was not found.");
            var profile = emulator.AllProfiles.FirstOrDefault(candidate => candidate.Id == action.EmulatorProfileId) ??
                throw new InvalidOperationException("The configured emulator profile was not found.");
            var emulationAction = action.GetClone<GameAction, EmulationPlayAction>();
            emulationAction.SelectedEmulatorProfile = profile;
            emulationAction.SelectedRomPath = game.Roms?.FirstOrDefault()?.Path ?? string.Empty;
            startingArgs.SelectedRomFile = emulationAction.SelectedRomPath;
            controller.StartEmulator(emulationAction, false, startingArgs);
        }
        else
        {
            controller.Start(action, false, startingArgs);
        }

        return $"Ran {action.Name} for {game.Name}.";
    }
}
