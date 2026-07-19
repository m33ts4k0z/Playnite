using Playnite.Common;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Playnite.Controllers
{
    /// <summary>
    /// Starts portable file and URL actions without requiring a plugin host.
    /// Plugin, emulator, and script actions remain the responsibility of the
    /// full game-action runner and fail explicitly here.
    /// </summary>
    public static class PortableGameActionLauncher
    {
        public static string Launch(Game game)
        {
            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            var actions = game.GameActions == null
                ? new List<GameAction>()
                : game.GameActions.Where(candidate => candidate.IsPlayAction).ToList();
            if (actions.Count == 0)
            {
                throw new InvalidOperationException($"No play action is available for {game.Name}.");
            }

            if (actions.Count > 1)
            {
                throw new InvalidOperationException(
                    $"{game.Name} has multiple play actions; choose one from the full game action UI.");
            }

            var action = actions[0].ExpandVariables(game);
            switch (action.Type)
            {
                case GameActionType.File:
                    ProcessStarter.StartProcess(action.Path, action.Arguments, action.WorkingDir);
                    break;
                case GameActionType.URL:
                    ProcessStarter.StartUrl(action.Path);
                    break;
                case GameActionType.Emulator:
                    throw new NotSupportedException(
                        "Emulator play actions require the portable emulator action selector.");
                case GameActionType.Script:
                    throw new NotSupportedException(
                        "Script play actions require the portable scripting action runner.");
                default:
                    throw new NotSupportedException($"Unsupported play action type {action.Type}.");
            }

            return $"Starting {game.Name}.";
        }
    }
}
