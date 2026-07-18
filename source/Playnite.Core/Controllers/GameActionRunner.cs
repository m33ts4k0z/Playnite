using Playnite.Common;
using Playnite.Database;
using Playnite.Emulators;
using Playnite.Plugins;
using Playnite.Scripting.PowerShell;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Playnite.Controllers
{
    public enum GameOperationKind
    {
        Play,
        Install,
        Uninstall
    }

    public class GameOperationResult
    {
        public bool Success { get; private set; }
        public bool SelectionRequired { get; private set; }
        public string Message { get; private set; }
        public IReadOnlyList<string> Choices { get; private set; }

        private GameOperationResult()
        {
            Choices = new List<string>();
        }

        public static GameOperationResult Completed(string message)
        {
            return new GameOperationResult { Success = true, Message = message };
        }

        public static GameOperationResult Failed(string message)
        {
            return new GameOperationResult { Message = message };
        }

        public static GameOperationResult Select(string message, IEnumerable<string> choices)
        {
            return new GameOperationResult
            {
                SelectionRequired = true,
                Message = message,
                Choices = choices.ToList()
            };
        }
    }

    /// <summary>
    /// UI-independent game action orchestration shared by Avalonia hosts. It
    /// discovers actions from loaded plugins and the database, manages controller
    /// lifetimes and persists running/install state. UI policy (choice dialogs,
    /// error presentation and settings) stays in the application shell.
    /// </summary>
    public sealed class GameActionRunner : IDisposable
    {
        private sealed class OperationChoice
        {
            public string Name { get; set; }
            public object Action { get; set; }
        }

        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly GameDatabase database;
        private readonly GameControllerFactory controllers;
        private readonly ExtensionFactory extensions;
        private readonly Func<IPlayniteAPI> apiProvider;
        private readonly Dictionary<Guid, IPowerShellRuntime> scriptRuntimes =
            new Dictionary<Guid, IPowerShellRuntime>();

        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> OperationFailed;
        public event EventHandler<Game> GameStateChanged;

        public GameControllerFactory Controllers { get { return controllers; } }
        public ExtensionFactory Extensions { get { return extensions; } }

        public GameActionRunner(
            GameDatabase database,
            GameControllerFactory controllers,
            ExtensionFactory extensions,
            Func<IPlayniteAPI> apiProvider)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.controllers = controllers ?? throw new ArgumentNullException(nameof(controllers));
            this.extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
            this.apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));

            controllers.Started += Controllers_Started;
            controllers.Stopped += Controllers_Stopped;
            controllers.Installed += Controllers_Installed;
            controllers.InstallationCancelled += Controllers_InstallationCancelled;
            controllers.Uninstalled += Controllers_Uninstalled;
        }

        public GameOperationResult Play(Game game, int choiceIndex = -1)
        {
            if (game == null)
            {
                return GameOperationResult.Failed("No game is selected.");
            }

            if (!game.IsInstalled)
            {
                return Install(game, choiceIndex);
            }

            if (game.IsRunning || game.IsLaunching)
            {
                return GameOperationResult.Failed($"{game.Name} is already running or launching.");
            }

            List<OperationChoice> choices = null;
            PlayController selectedController = null;
            try
            {
                choices = GetPlayChoices(game);
                if (choices.Count == 0)
                {
                    return GameOperationResult.Failed($"No play action is available for {game.Name}.");
                }

                if (choices.Count > 1 && (choiceIndex < 0 || choiceIndex >= choices.Count))
                {
                    DisposePlayChoices(choices, null);
                    return GameOperationResult.Select(
                        $"Choose how to start {game.Name}.",
                        choices.Select(choice => choice.Name));
                }

                var selected = choices[choiceIndex >= 0 ? choiceIndex : 0].Action;
                IPowerShellRuntime scriptRuntime = null;
                if (selected is AutomaticPlayController || selected is GameAction)
                {
                    try
                    {
                        scriptRuntime = new PowerShellRuntime($"{game.Name} {game.Id} runtime");
                    }
                    catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
                    {
                        logger.Error(exception, "Failed to create PowerShell runtime; using the no-op runtime.");
                        scriptRuntime = new DummyPowerShellRuntime();
                    }

                    selectedController = new GenericPlayController(database, game, scriptRuntime, apiProvider());
                    scriptRuntimes[game.Id] = scriptRuntime;
                }
                else
                {
                    selectedController = selected as PlayController;
                }

                DisposePlayChoices(choices, selectedController);
                if (selectedController == null)
                {
                    DisposeScriptRuntime(game.Id);
                    return GameOperationResult.Failed($"No compatible controller could start {game.Name}.");
                }

                controllers.RemovePlayController(game.Id);
                controllers.AddController(selectedController);
                SetGameState(game, launching: true);

                var startingArgs = new OnGameStartingEventArgs
                {
                    Game = game.GetCopy(),
                    SourceAction = (selected as GameAction)?.GetClone(),
                    SelectedRomFile = (selected as EmulationPlayAction)?.SelectedRomPath
                };
                controllers.InvokeOnStarting(this, startingArgs);
                if (startingArgs.CancelStartup)
                {
                    CancelStartup(game, "Game startup was cancelled by an extension.");
                    return GameOperationResult.Failed($"Startup of {game.Name} was cancelled by an extension.");
                }

                if (selectedController is GenericPlayController genericController)
                {
                    if (selected is EmulationPlayAction emulationAction)
                    {
                        genericController.StartEmulator(emulationAction, true, startingArgs);
                    }
                    else if (selected is AutomaticPlayController automaticAction)
                    {
                        genericController.Start(automaticAction);
                    }
                    else if (selected is GameAction gameAction)
                    {
                        genericController.Start(gameAction, true, startingArgs);
                    }
                    else
                    {
                        throw new NotSupportedException("Unknown generic play action type.");
                    }
                }
                else
                {
                    selectedController.Play(new PlayActionArgs());
                }

                var message = $"Starting {game.Name}.";
                RaiseStatus(message);
                return GameOperationResult.Completed(message);
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(exception, $"Cannot start {game.Name}.");
                if (selectedController != null)
                {
                    controllers.RemoveController(selectedController);
                }

                DisposePlayChoices(choices, selectedController);
                DisposeScriptRuntime(game.Id);
                SetGameState(game, launching: false, running: false);
                return Fail($"Cannot start {game.Name}: {exception.Message}");
            }
        }

        public GameOperationResult Install(Game game, int choiceIndex = -1)
        {
            if (game == null)
            {
                return GameOperationResult.Failed("No game is selected.");
            }

            var choices = GetInstallChoices(game);
            if (choices.Count == 0)
            {
                return GameOperationResult.Failed($"No install action is available for {game.Name}.");
            }

            if (choices.Count > 1 && (choiceIndex < 0 || choiceIndex >= choices.Count))
            {
                DisposeControllerChoices(choices, null);
                return GameOperationResult.Select(
                    $"Choose how to install {game.Name}.",
                    choices.Select(choice => choice.Name));
            }

            var controller = choices[choiceIndex >= 0 ? choiceIndex : 0].Action as InstallController;
            try
            {
                DisposeControllerChoices(choices, controller);
                controllers.RemoveInstallController(game.Id);
                controllers.AddController(controller);
                SetGameState(game, installing: true);
                controller.Install(new InstallActionArgs());
                var message = $"Installing {game.Name}.";
                RaiseStatus(message);
                return GameOperationResult.Completed(message);
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                if (controller != null)
                {
                    controllers.RemoveController(controller);
                }

                SetGameState(game, installing: false);
                return Fail($"Cannot install {game.Name}: {exception.Message}");
            }
        }

        public GameOperationResult Uninstall(Game game, int choiceIndex = -1)
        {
            if (game == null)
            {
                return GameOperationResult.Failed("No game is selected.");
            }

            if (game.IsRunning || game.IsLaunching)
            {
                return GameOperationResult.Failed($"{game.Name} cannot be uninstalled while it is running.");
            }

            var choices = GetUninstallChoices(game);
            if (choices.Count == 0)
            {
                return GameOperationResult.Failed($"No uninstall action is available for {game.Name}.");
            }

            if (choices.Count > 1 && (choiceIndex < 0 || choiceIndex >= choices.Count))
            {
                DisposeControllerChoices(choices, null);
                return GameOperationResult.Select(
                    $"Choose how to uninstall {game.Name}.",
                    choices.Select(choice => choice.Name));
            }

            var controller = choices[choiceIndex >= 0 ? choiceIndex : 0].Action as UninstallController;
            try
            {
                DisposeControllerChoices(choices, controller);
                controllers.RemoveUninstallController(game.Id);
                controllers.AddController(controller);
                SetGameState(game, uninstalling: true);
                controller.Uninstall(new UninstallActionArgs());
                var message = $"Uninstalling {game.Name}.";
                RaiseStatus(message);
                return GameOperationResult.Completed(message);
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                if (controller != null)
                {
                    controllers.RemoveController(controller);
                }

                SetGameState(game, uninstalling: false);
                return Fail($"Cannot uninstall {game.Name}: {exception.Message}");
            }
        }

        public void Dispose()
        {
            controllers.Started -= Controllers_Started;
            controllers.Stopped -= Controllers_Stopped;
            controllers.Installed -= Controllers_Installed;
            controllers.InstallationCancelled -= Controllers_InstallationCancelled;
            controllers.Uninstalled -= Controllers_Uninstalled;

            foreach (var runtime in scriptRuntimes.Values.ToList())
            {
                runtime.Dispose();
            }

            scriptRuntimes.Clear();
        }

        private List<OperationChoice> GetPlayChoices(Game game)
        {
            var result = new List<OperationChoice>();
            foreach (var plugin in extensions.Plugins.Values)
            {
                if (!game.IncludeLibraryPluginAction && plugin.Plugin.Id == game.PluginId)
                {
                    continue;
                }

                try
                {
                    var pluginControllers = plugin.Plugin.GetPlayActions(
                        new GetPlayActionsArgs { Game = game })?.ToList();
                    if (pluginControllers != null)
                    {
                        result.AddRange(pluginControllers.Select(controller => new OperationChoice
                        {
                            Name = controller.Name ?? plugin.Description.Name,
                            Action = controller
                        }));
                    }
                }
                catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(exception, $"Failed to get play actions from {plugin.Description.Name}.");
                }
            }

            if (game.GameActions?.Any(action => action.IsPlayAction) == true)
            {
                foreach (var action in ExpandGameActions(game))
                {
                    result.Add(new OperationChoice
                    {
                        Name = string.IsNullOrWhiteSpace(action.Name) ? game.Name : action.Name,
                        Action = action
                    });
                }
            }

            return result;
        }

        private IEnumerable<GameAction> ExpandGameActions(Game game)
        {
            var selectEmulatorAdded = false;
            var multipleRoms = game.Roms?.Count > 1;
            var roms = game.Roms?.Any() == true
                ? game.Roms
                : new ObservableCollection<GameRom> { new GameRom() };

            foreach (var action in game.GameActions.Where(candidate => candidate.IsPlayAction))
            {
                if (action.Type != GameActionType.Emulator)
                {
                    yield return action;
                    continue;
                }

                if (action.EmulatorId == Guid.Empty)
                {
                    if (selectEmulatorAdded)
                    {
                        continue;
                    }

                    selectEmulatorAdded = true;
                    foreach (var supported in game.GetCompatibleEmulators(database).OrderBy(item => item.Key.Name))
                    {
                        foreach (var profile in supported.Value.OrderBy(item => item.Name))
                        {
                            foreach (var rom in roms)
                            {
                                yield return CreateEmulationAction(
                                    action,
                                    supported.Key.Id,
                                    profile,
                                    rom,
                                    multipleRoms ? $"{supported.Key.Name}: {profile.Name}: {rom.Name}" : $"{supported.Key.Name}: {profile.Name}");
                            }
                        }
                    }
                }
                else
                {
                    var emulator = database.Emulators[action.EmulatorId];
                    if (emulator == null)
                    {
                        continue;
                    }

                    var profiles = action.EmulatorProfileId == null
                        ? game.GetCompatibleProfiles(emulator)
                        : emulator.AllProfiles.Where(profile => profile.Id == action.EmulatorProfileId).ToList();
                    foreach (var profile in profiles)
                    {
                        foreach (var rom in roms)
                        {
                            yield return CreateEmulationAction(
                                action,
                                emulator.Id,
                                profile,
                                rom,
                                multipleRoms ? $"{action.Name}: {profile.Name}: {rom.Name}" : $"{action.Name}: {profile.Name}");
                        }
                    }
                }
            }
        }

        private static EmulationPlayAction CreateEmulationAction(
            GameAction source,
            Guid emulatorId,
            EmulatorProfile profile,
            GameRom rom,
            string name)
        {
            var action = source.GetClone<GameAction, EmulationPlayAction>();
            action.Name = name;
            action.EmulatorId = emulatorId;
            action.EmulatorProfileId = profile.Id;
            action.SelectedEmulatorProfile = profile;
            action.SelectedRomPath = rom.Path;
            return action;
        }

        private List<OperationChoice> GetInstallChoices(Game game)
        {
            var result = new List<OperationChoice>();
            foreach (var plugin in extensions.Plugins.Values)
            {
                try
                {
                    var actions = plugin.Plugin.GetInstallActions(new GetInstallActionsArgs { Game = game })?.ToList();
                    if (actions != null)
                    {
                        result.AddRange(actions.Select(action => new OperationChoice
                        {
                            Name = action.Name ?? plugin.Description.Name,
                            Action = action
                        }));
                    }
                }
                catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(exception, $"Failed to get install actions from {plugin.Description.Name}.");
                }
            }

            return result;
        }

        private List<OperationChoice> GetUninstallChoices(Game game)
        {
            var result = new List<OperationChoice>();
            foreach (var plugin in extensions.Plugins.Values)
            {
                try
                {
                    var actions = plugin.Plugin.GetUninstallActions(new GetUninstallActionsArgs { Game = game })?.ToList();
                    if (actions != null)
                    {
                        result.AddRange(actions.Select(action => new OperationChoice
                        {
                            Name = action.Name ?? plugin.Description.Name,
                            Action = action
                        }));
                    }
                }
                catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(exception, $"Failed to get uninstall actions from {plugin.Description.Name}.");
                }
            }

            return result;
        }

        private void Controllers_Started(object sender, GameStartedEventArgs args)
        {
            var game = args.Source?.Game == null ? null : database.Games[args.Source.Game.Id];
            if (game == null)
            {
                return;
            }

            SetGameState(game, launching: false, running: true);
            RaiseStatus($"{game.Name} is running.");
        }

        private void Controllers_Stopped(object sender, GameStoppedEventArgs args)
        {
            var game = args.Source?.Game == null ? null : database.Games[args.Source.Game.Id];
            if (game == null)
            {
                return;
            }

            game.IsLaunching = false;
            game.IsRunning = false;
            game.LastActivity = DateTime.Now;
            game.Playtime += args.SessionLength;
            game.PlayCount++;
            database.Games.Update(game);
            GameStateChanged?.Invoke(this, game);
            RaiseStatus($"{game.Name} stopped after {TimeSpan.FromSeconds(args.SessionLength):g}.");
            controllers.RemovePlayController(game.Id);
            DisposeScriptRuntime(game.Id);
        }

        private void Controllers_Installed(object sender, GameInstalledEventArgs args)
        {
            var game = args.Source?.Game == null ? null : database.Games[args.Source.Game.Id];
            if (game == null)
            {
                return;
            }

            game.IsInstalling = false;
            game.IsInstalled = true;
            if (args.InstalledInfo != null)
            {
                game.InstallDirectory = args.InstalledInfo.InstallDirectory;
                if (args.InstalledInfo.Roms != null)
                {
                    game.Roms = new ObservableCollection<GameRom>(args.InstalledInfo.Roms);
                }
            }

            database.Games.Update(game);
            GameStateChanged?.Invoke(this, game);
            RaiseStatus($"{game.Name} was installed.");
            controllers.RemoveInstallController(game.Id);
        }

        private void Controllers_InstallationCancelled(object sender, GameInstallationCancelledEventArgs args)
        {
            var game = args.Source?.Game == null ? null : database.Games[args.Source.Game.Id];
            if (game == null)
            {
                return;
            }

            SetGameState(game, installing: false);
            RaiseStatus($"Installation of {game.Name} was cancelled.");
            controllers.RemoveInstallController(game.Id);
        }

        private void Controllers_Uninstalled(object sender, GameUninstalledEventArgs args)
        {
            var game = args.Source?.Game == null ? null : database.Games[args.Source.Game.Id];
            if (game == null)
            {
                return;
            }

            game.IsUninstalling = false;
            game.IsInstalled = false;
            database.Games.Update(game);
            GameStateChanged?.Invoke(this, game);
            RaiseStatus($"{game.Name} was uninstalled.");
            controllers.RemoveUninstallController(game.Id);
        }

        private void CancelStartup(Game game, string reason)
        {
            controllers.InvokeOnGameStartupCancelled(this, game.GetCopy());
            controllers.RemovePlayController(game.Id);
            DisposeScriptRuntime(game.Id);
            SetGameState(game, launching: false, running: false);
            RaiseStatus(reason);
        }

        private void SetGameState(
            Game game,
            bool? launching = null,
            bool? running = null,
            bool? installing = null,
            bool? uninstalling = null)
        {
            if (launching.HasValue)
            {
                game.IsLaunching = launching.Value;
            }

            if (running.HasValue)
            {
                game.IsRunning = running.Value;
            }

            if (installing.HasValue)
            {
                game.IsInstalling = installing.Value;
            }

            if (uninstalling.HasValue)
            {
                game.IsUninstalling = uninstalling.Value;
            }

            database.Games.Update(game);
            GameStateChanged?.Invoke(this, game);
        }

        private GameOperationResult Fail(string message)
        {
            logger.Error(message);
            OperationFailed?.Invoke(this, message);
            return GameOperationResult.Failed(message);
        }

        private void RaiseStatus(string message)
        {
            StatusChanged?.Invoke(this, message);
        }

        private void DisposeScriptRuntime(Guid gameId)
        {
            if (scriptRuntimes.TryGetValue(gameId, out var runtime))
            {
                scriptRuntimes.Remove(gameId);
                runtime.Dispose();
            }
        }

        private static void DisposePlayChoices(IEnumerable<OperationChoice> choices, PlayController selected)
        {
            if (choices == null)
            {
                return;
            }

            foreach (var controller in choices.Select(choice => choice.Action).OfType<PlayController>())
            {
                if (!ReferenceEquals(controller, selected))
                {
                    controller.Dispose();
                }
            }
        }

        private static void DisposeControllerChoices(IEnumerable<OperationChoice> choices, ControllerBase selected)
        {
            if (choices == null)
            {
                return;
            }

            foreach (var controller in choices.Select(choice => choice.Action).OfType<ControllerBase>())
            {
                if (!ReferenceEquals(controller, selected))
                {
                    controller.Dispose();
                }
            }
        }
    }
}
