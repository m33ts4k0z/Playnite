using Playnite.Common;
using Playnite.Database;
using Playnite.Emulators;
using Playnite.Plugins;
using Playnite.Scripting.PowerShell;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Exceptions;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

    public sealed class GameActionRunnerPolicy
    {
        public Func<string, IPowerShellRuntime> CreateScriptRuntime { get; set; } =
            name => new PowerShellRuntime(name);
        public Func<string> GlobalPreScript { get; set; } = () => null;
        public Func<string> GlobalGameStartedScript { get; set; } = () => null;
        public Func<string> GlobalPostScript { get; set; } = () => null;
        public Func<bool> IsHdrEnabled { get; set; } = HdrUtilities.IsHdrEnabled;
        public Action<bool> SetHdrEnabled { get; set; } = HdrUtilities.SetHdrEnabled;
        public Func<bool> ShutdownClients { get; set; } = () => false;
        public Func<uint> ClientShutdownGraceSeconds { get; set; } = () => 60;
        public Func<uint> ClientShutdownMinimumSessionSeconds { get; set; } = () => 120;
        public Func<IReadOnlyCollection<Guid>> ClientShutdownPluginIds { get; set; } =
            () => Array.Empty<Guid>();
        public Func<IEnumerable<LibraryPlugin>> AdditionalLibraryPlugins { get; set; } =
            () => Array.Empty<LibraryPlugin>();
        public Func<Game, IEnumerable<PlayController>> AdditionalPlayControllers { get; set; } =
            _ => Array.Empty<PlayController>();
        public Func<Game, IEnumerable<InstallController>> AdditionalInstallControllers { get; set; } =
            _ => Array.Empty<InstallController>();
        public Func<Game, IEnumerable<UninstallController>> AdditionalUninstallControllers { get; set; } =
            _ => Array.Empty<UninstallController>();
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
        private readonly ConcurrentDictionary<Guid, IPowerShellRuntime> scriptRuntimes =
            new ConcurrentDictionary<Guid, IPowerShellRuntime>();
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> clientShutdownJobs =
            new ConcurrentDictionary<Guid, CancellationTokenSource>();
        private readonly object hdrStateLock = new object();
        private readonly HashSet<Guid> hdrManagedGames = new HashSet<Guid>();
        private bool previousHdrEnabled;

        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> OperationFailed;
        public event EventHandler<Game> GameStateChanged;

        public GameControllerFactory Controllers { get { return controllers; } }
        public ExtensionFactory Extensions { get { return extensions; } }
        public GameActionRunnerPolicy Policy { get; }

        public GameActionRunner(
            GameDatabase database,
            GameControllerFactory controllers,
            ExtensionFactory extensions,
            Func<IPlayniteAPI> apiProvider,
            GameActionRunnerPolicy policy = null)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
            this.controllers = controllers ?? throw new ArgumentNullException(nameof(controllers));
            this.extensions = extensions ?? throw new ArgumentNullException(nameof(extensions));
            this.apiProvider = apiProvider ?? throw new ArgumentNullException(nameof(apiProvider));
            Policy = policy ?? new GameActionRunnerPolicy();

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
                var scriptRuntime = CreateScriptRuntime(game);
                if (selected is AutomaticPlayController || selected is GameAction)
                {
                    selectedController = new GenericPlayController(database, game, scriptRuntime, apiProvider());
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

                CancelClientShutdown(game);
                var scriptVariables = new Dictionary<string, object>
                {
                    { "StartingArgs", startingArgs },
                    { "SourceAction", startingArgs.SourceAction },
                    { "SelectedRomFile", startingArgs.SelectedRomFile }
                };

                if (game.EnableSystemHdr)
                {
                    ApplyHdr(game);
                }

                if (!ExecuteScript(
                    scriptRuntime,
                    Policy.GlobalPreScript(),
                    game,
                    game.UseGlobalPreScript,
                    true,
                    "Game starting script failed.",
                    scriptVariables))
                {
                    CancelStartup(game, "Game startup was cancelled because the global pre-script failed.");
                    return GameOperationResult.Failed(
                        $"Startup of {game.Name} was cancelled because the global pre-script failed.");
                }

                if (startingArgs.CancelStartup)
                {
                    CancelStartup(game, "Game startup was cancelled by the global pre-script.");
                    return GameOperationResult.Failed(
                        $"Startup of {game.Name} was cancelled by the global pre-script.");
                }

                if (!ExecuteScript(
                    scriptRuntime,
                    game.PreScript,
                    game,
                    true,
                    false,
                    "Game starting script failed.",
                    scriptVariables))
                {
                    CancelStartup(game, "Game startup was cancelled because the game pre-script failed.");
                    return GameOperationResult.Failed(
                        $"Startup of {game.Name} was cancelled because the game pre-script failed.");
                }

                if (startingArgs.CancelStartup)
                {
                    CancelStartup(game, "Game startup was cancelled by the game pre-script.");
                    return GameOperationResult.Failed(
                        $"Startup of {game.Name} was cancelled by the game pre-script.");
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
                RestoreHdr(game);
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
            foreach (var shutdownJob in clientShutdownJobs.Values.ToList())
            {
                shutdownJob.Cancel();
            }

            clientShutdownJobs.Clear();
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

            try
            {
                result.AddRange((Policy.AdditionalPlayControllers(game) ?? Enumerable.Empty<PlayController>()).Select(controller =>
                    new OperationChoice
                    {
                        Name = controller.Name ?? game.Name,
                        Action = controller
                    }));
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(exception, "Failed to get additional play actions.");
                OperationFailed?.Invoke(this, $"Additional play actions failed: {exception.Message}");
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

            try
            {
                result.AddRange((Policy.AdditionalInstallControllers(game) ?? Enumerable.Empty<InstallController>()).Select(controller =>
                    new OperationChoice
                    {
                        Name = controller.Name ?? game.Name,
                        Action = controller
                    }));
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(exception, "Failed to get additional install actions.");
                OperationFailed?.Invoke(this, $"Additional install actions failed: {exception.Message}");
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

            try
            {
                result.AddRange((Policy.AdditionalUninstallControllers(game) ?? Enumerable.Empty<UninstallController>()).Select(controller =>
                    new OperationChoice
                    {
                        Name = controller.Name ?? game.Name,
                        Action = controller
                    }));
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(exception, "Failed to get additional uninstall actions.");
                OperationFailed?.Invoke(this, $"Additional uninstall actions failed: {exception.Message}");
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
            var variables = new Dictionary<string, object>
            {
                { "SourceAction", (args.Source as GenericPlayController)?.StartingArgs?.SourceAction?.GetClone() },
                { "SelectedRomFile", (args.Source as GenericPlayController)?.StartingArgs?.SelectedRomFile },
                { "StartedProcessId", args.StartedProcessId }
            };
            if (scriptRuntimes.TryGetValue(game.Id, out var runtime))
            {
                ExecuteScript(
                    runtime,
                    game.GameStartedScript,
                    game,
                    true,
                    false,
                    "Game started script failed.",
                    variables);
                ExecuteScript(
                    runtime,
                    Policy.GlobalGameStartedScript(),
                    game,
                    game.UseGlobalGameStartedScript,
                    true,
                    "Game started script failed.",
                    variables);
            }

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
            controllers.RemovePlayController(game.Id);
            RestoreHdr(game);

            var variables = new Dictionary<string, object>
            {
                { "SourceAction", (args.Source as GenericPlayController)?.StartingArgs?.SourceAction?.GetClone() },
                { "SelectedRomFile", (args.Source as GenericPlayController)?.StartingArgs?.SelectedRomFile }
            };
            if (scriptRuntimes.TryGetValue(game.Id, out var runtime))
            {
                ExecuteScript(
                    runtime,
                    game.PostScript,
                    game,
                    true,
                    false,
                    "Game stopped script failed.",
                    variables);
                ExecuteScript(
                    runtime,
                    Policy.GlobalPostScript(),
                    game,
                    game.UseGlobalPostScript,
                    true,
                    "Game stopped script failed.",
                    variables);
            }

            extensions.InvokeOnGameStopped(game, args.SessionLength, false);
            ScheduleClientShutdown(game, args.SessionLength);
            GameStateChanged?.Invoke(this, game);
            RaiseStatus($"{game.Name} stopped after {TimeSpan.FromSeconds(args.SessionLength):g}.");
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
            RestoreHdr(game);
            SetGameState(game, launching: false, running: false);
            RaiseStatus(reason);
        }

        private IPowerShellRuntime CreateScriptRuntime(Game game)
        {
            IPowerShellRuntime runtime;
            try
            {
                runtime = Policy.CreateScriptRuntime($"{game.Name} {game.Id} runtime");
                if (runtime == null)
                {
                    throw new InvalidOperationException("The script runtime factory returned null.");
                }
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(exception, "Failed to create PowerShell runtime; using the no-op runtime.");
                OperationFailed?.Invoke(
                    this,
                    $"PowerShell runtime creation failed; game scripts will not run: {exception.Message}");
                runtime = new DummyPowerShellRuntime();
            }

            DisposeScriptRuntime(game.Id);
            scriptRuntimes[game.Id] = runtime;
            return runtime;
        }

        private bool ExecuteScript(
            IPowerShellRuntime runtime,
            string script,
            Game game,
            bool execute,
            bool global,
            string phase,
            Dictionary<string, object> variables)
        {
            if (!execute || string.IsNullOrWhiteSpace(script))
            {
                return true;
            }

            try
            {
                var scriptVariables = new Dictionary<string, object>
                {
                    { "PlayniteApi", apiProvider() },
                    { "Game", game.GetCopy() }
                };
                if (variables != null)
                {
                    foreach (var variable in variables)
                    {
                        scriptVariables[variable.Key] = variable.Value;
                    }
                }

                var expandedScript = game.ExpandVariables(script);
                var workingDirectory = game.ExpandVariables(game.InstallDirectory, true);
                runtime.Execute(
                    expandedScript,
                    !string.IsNullOrEmpty(workingDirectory) && Directory.Exists(workingDirectory)
                        ? workingDirectory
                        : PlaynitePaths.ProgramPath,
                    scriptVariables);
                return true;
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(exception, global
                    ? "Failed to execute global game script."
                    : "Failed to execute per-game script.");
                var details = exception is ScriptRuntimeException scriptException &&
                    !string.IsNullOrWhiteSpace(scriptException.ScriptStackTrace)
                        ? $"{exception.Message}{Environment.NewLine}{scriptException.ScriptStackTrace}"
                        : exception.Message;
                OperationFailed?.Invoke(this, $"{phase} {details}");
                return false;
            }
        }

        private void ApplyHdr(Game game)
        {
            lock (hdrStateLock)
            {
                if (hdrManagedGames.Count == 0)
                {
                    previousHdrEnabled = Policy.IsHdrEnabled();
                }

                hdrManagedGames.Add(game.Id);
                Policy.SetHdrEnabled(true);
            }
        }

        private void RestoreHdr(Game game)
        {
            lock (hdrStateLock)
            {
                if (hdrManagedGames.Remove(game.Id) && hdrManagedGames.Count == 0)
                {
                    Policy.SetHdrEnabled(previousHdrEnabled);
                }
            }
        }

        private void CancelClientShutdown(Game game)
        {
            if (game.IsCustomGame ||
                !clientShutdownJobs.TryRemove(game.PluginId, out var cancellation))
            {
                return;
            }

            cancellation.Cancel();
            logger.Debug($"Cancelled pending client shutdown for plugin {game.PluginId}.");
        }

        private void ScheduleClientShutdown(Game game, ulong sessionLength)
        {
            if (!Policy.ShutdownClients() || game.IsCustomGame ||
                sessionLength <= Policy.ClientShutdownMinimumSessionSeconds())
            {
                return;
            }

            if (database.Games.Any(candidate =>
                candidate.PluginId == game.PluginId &&
                (candidate.IsRunning || candidate.IsInstalling || candidate.IsUninstalling)))
            {
                logger.Debug("Client shutdown skipped because another game from the library is active.");
                return;
            }

            var plugin = extensions.GetLibraryPlugin(game.PluginId) ??
                (Policy.AdditionalLibraryPlugins() ?? Enumerable.Empty<LibraryPlugin>())
                    .FirstOrDefault(candidate => candidate.Id == game.PluginId);
            var selectedPlugins = Policy.ClientShutdownPluginIds() ?? Array.Empty<Guid>();
            if (plugin?.Properties?.CanShutdownClient != true ||
                plugin.Client == null ||
                !selectedPlugins.Contains(plugin.Id))
            {
                return;
            }

            CancelClientShutdown(game);
            var cancellation = new CancellationTokenSource();
            clientShutdownJobs[plugin.Id] = cancellation;
            _ = RunClientShutdown(plugin, Policy.ClientShutdownGraceSeconds(), cancellation);
        }

        private async Task RunClientShutdown(
            LibraryPlugin plugin,
            uint graceSeconds,
            CancellationTokenSource cancellation)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(graceSeconds), cancellation.Token).ConfigureAwait(false);
                if (!cancellation.IsCancellationRequested)
                {
                    plugin.Client.Shutdown();
                }
            }
            catch (OperationCanceledException)
            {
                logger.Debug($"Client shutdown for {plugin.Name} was cancelled.");
            }
            catch (Exception exception) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(exception, $"Failed to shut down {plugin.Name} client.");
                OperationFailed?.Invoke(this, $"Failed to shut down {plugin.Name}: {exception.Message}");
            }
            finally
            {
                ((ICollection<KeyValuePair<Guid, CancellationTokenSource>>)clientShutdownJobs)
                    .Remove(new KeyValuePair<Guid, CancellationTokenSource>(plugin.Id, cancellation));
                cancellation.Dispose();
            }
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
            if (scriptRuntimes.TryRemove(gameId, out var runtime))
            {
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
