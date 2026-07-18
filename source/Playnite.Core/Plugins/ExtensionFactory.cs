using Playnite.API;
using Playnite.Database;
using Playnite.Controllers;
using Playnite.Scripting;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using Playnite.Common;
using Playnite.SDK.Models;
using Playnite.SDK.Events;

namespace Playnite.Plugins
{
    public class LoadedPlugin
    {
        public Plugin Plugin { get; }
        public ExtensionManifest Description { get; }
        public string PluginIcon { get; }

        public LoadedPlugin(Plugin plugin, ExtensionManifest description)
        {
            Plugin = plugin;
            Description = description;
            if (!string.IsNullOrEmpty(description.Icon))
            {
                PluginIcon = Path.Combine(Path.GetDirectoryName(description.DescriptionPath), description.Icon);
            }
        }
    }

    public enum AddonLoadError
    {
        None,
        Uknown,
        SDKVersion
    }

    public sealed class ExtensionLoadFailure
    {
        public ExtensionManifest Manifest { get; }
        public AddonLoadError Error { get; }
        public string ExceptionType { get; }
        public string Message { get; }
        public string Details { get; }

        public ExtensionLoadFailure(
            ExtensionManifest manifest,
            AddonLoadError error,
            string message,
            Exception exception = null)
        {
            Manifest = manifest;
            Error = error;
            var rootException = exception?.GetBaseException();
            ExceptionType = rootException?.GetType().FullName;
            Message = rootException?.Message ?? message;
            Details = exception == null
                ? message
                : string.IsNullOrWhiteSpace(message)
                    ? exception.ToString()
                    : $"{message}{Environment.NewLine}{exception}";
        }
    }

    public class ExtensionFactory : ObservableObject, IDisposable
    {
        private static ILogger logger = LogManager.GetLogger();
        private readonly IGameDatabase database;
        private readonly GameControllerFactory controllers;
        private readonly Func<ExtensionManifest, IPlayniteAPI> apiGenerator;
        private readonly Action<string> addonLocalizationLoader;
        private readonly List<AssemblyLoadContext> pluginLoadContexts = new List<AssemblyLoadContext>();

        public List<(ExtensionManifest manifest, AddonLoadError error)> FailedExtensions { get; } = new List<(ExtensionManifest manifest, AddonLoadError error)>();
        public List<ExtensionLoadFailure> LoadFailures { get; } = new List<ExtensionLoadFailure>();

        public Dictionary<Guid, LoadedPlugin> Plugins
        {
            get; private set;
        } = new Dictionary<Guid, LoadedPlugin>();

        public List<LibraryPlugin> LibraryPlugins
        {
            get => Plugins.Where(a => a.Value.Description.Type == ExtensionType.GameLibrary).Select(a => (LibraryPlugin)a.Value.Plugin).ToList();
        }

        public List<MetadataPlugin> MetadataPlugins
        {
            get => Plugins.Where(a => a.Value.Description.Type == ExtensionType.MetadataProvider).Select(a => (MetadataPlugin)a.Value.Plugin).ToList();
        }

        public List<GenericPlugin> GenericPlugins
        {
            get => Plugins.Where(a => a.Value.Description.Type == ExtensionType.GenericPlugin).Select(a => (GenericPlugin)a.Value.Plugin).ToList();
        }

        public  List<PlayniteScript> Scripts
        {
            get; private set;
        } =  new List<PlayniteScript>();

        public ExtensionFactory(
            IGameDatabase database,
            GameControllerFactory controllers,
            Func<ExtensionManifest, IPlayniteAPI> apiGenerator,
            Action<string> addonLocalizationLoader = null)
        {
            this.database = database;
            this.controllers = controllers;
            this.apiGenerator = apiGenerator;
            this.addonLocalizationLoader = addonLocalizationLoader;
            SDK.Data.Markup.Init(new MarkupConverter());
            SDK.Data.Serialization.Init(new DataSerializer());
            SDK.Data.SQLite.Init((path, flags) => new Sqlite(path, flags));
            controllers.Installed += Controllers_Installed;
            controllers.InstallationCancelled += Controllers_InstallationCancelled;
            controllers.Starting += Controllers_Starting;
            controllers.Started += Controllers_Started;
            controllers.Stopped += Controllers_Stopped;
            controllers.Uninstalled += Controllers_Uninstalled;
            controllers.StartupCancelled += Controllers_StartupCancelled;
        }

        public void Dispose()
        {
            DisposePlugins();
            DisposeScripts();
            controllers.Installed -= Controllers_Installed;
            controllers.InstallationCancelled -= Controllers_InstallationCancelled;
            controllers.Starting -= Controllers_Starting;
            controllers.Started -= Controllers_Started;
            controllers.Stopped -= Controllers_Stopped;
            controllers.Uninstalled -= Controllers_Uninstalled;
            controllers.StartupCancelled -= Controllers_StartupCancelled;
        }

        private void DisposeScripts()
        {
            if (Scripts?.Any() == true)
            {
                foreach (var script in Scripts)
                {
                    try
                    {
                        script.Dispose();
                    }
                    catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                    {
                        logger.Error(e, $"Failed to dispose script {script.Name}");
                    }
            }
            }

            Scripts = new List<PlayniteScript>();
        }

        private void DisposePlugins()
        {
            if (Plugins?.Any() == true)
            {
                foreach (var provider in Plugins.Keys)
                {
                    try
                    {
                        Plugins[provider].Plugin.Dispose();
                    }
                    catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                    {
                        logger.Error(e, $"Failed to dispose plugin {provider}");
                    }
                }
            }

            Plugins = new Dictionary<Guid, LoadedPlugin>();
            foreach (var loadContext in pluginLoadContexts)
            {
                loadContext.Unload();
            }

            pluginLoadContexts.Clear();
        }

        public static void CreatePluginFolders()
        {
            FileSystem.CreateDirectory(PlaynitePaths.ExtensionsDataPath);
            FileSystem.CreateDirectory(PlaynitePaths.ExtensionsProgramPath);
            FileSystem.CreateDirectory(PlaynitePaths.ExtensionsUserDataPath);
        }

        private static IEnumerable<ExtensionManifest> GetManifestsFromPath(string path)
        {
            if (Directory.Exists(path))
            {
                var man = GetManifestFromFile(Path.Combine(path, PlaynitePaths.ExtensionManifestFileName));
                if (man != null)
                {
                    yield return man;
                }
            }
            else if (File.Exists(path))
            {
                foreach (var dirPath in File.ReadAllLines(path).Where(a => !a.IsNullOrWhiteSpace() && !a.StartsWith("#")))
                {
                    ExtensionManifest man = null;
                    try
                    {
                        if (Directory.Exists(dirPath))
                        {
                            man = GetManifestFromFile(Path.Combine(dirPath.Trim(), PlaynitePaths.ExtensionManifestFileName));
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(e, "Failed to read extension dev file.");
                    }

                    if (man != null)
                    {
                        yield return man;
                    }
                }
            }
        }

        private static ExtensionManifest GetManifestFromFile(string file)
        {
            if (File.Exists(file))
            {
                try
                {
                    return ExtensionManifest.FromFile(file);
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(e, $"Failed to parse plugin description: {file}");
                    return null;
                }
            }

            return null;
        }

        internal static IEnumerable<BaseExtensionManifest> DeduplicateExtList(List<BaseExtensionManifest> list)
        {
            return list.GroupBy(a => a.Id).Select(g => g.OrderByDescending(x => x.Version).First());
        }

        public static List<ExtensionManifest> GetInstalledManifests(List<string> externalPaths = null)
        {
            var externals = new List<BaseExtensionManifest>();
            var user = new List<BaseExtensionManifest>();
            var install = new List<BaseExtensionManifest>();
            if (externalPaths.HasItems())
            {
                foreach (var ext in externalPaths)
                {
                    foreach (var man in GetManifestsFromPath(ext))
                    {
                        externals.Add(man);
                        man.IsExternalDev = true;
                    }
                }
            }

            if (Directory.Exists(PlaynitePaths.ExtensionsUserDataPath))
            {
                var enumerator = new SafeFileEnumerator(PlaynitePaths.ExtensionsUserDataPath, PlaynitePaths.ExtensionManifestFileName, SearchOption.AllDirectories);
                foreach (var desc in enumerator)
                {
                    var man = GetManifestFromFile(desc.FullName);
                    if (man?.Id.IsNullOrEmpty() == false)
                    {
                        if (externals.Any(a => a.Id == man.Id))
                        {
                            continue;
                        }
                        else
                        {
                            user.Add(man);
                        }
                    }
                }
            }

            if (Directory.Exists(PlaynitePaths.ExtensionsProgramPath))
            {
                var enumerator = new SafeFileEnumerator(PlaynitePaths.ExtensionsProgramPath, PlaynitePaths.ExtensionManifestFileName, SearchOption.AllDirectories);
                foreach (var desc in enumerator)
                {
                    var man = GetManifestFromFile(desc.FullName);
                    if (man?.Id.IsNullOrEmpty() == false)
                    {
                        if (externals.Any(a => a.Id == man.Id) || user.Any(a => a.Id == man.Id))
                        {
                            continue;
                        }
                        else
                        {
                            install.Add(man);
                        }
                    }
                }
            }

            var result = new List<ExtensionManifest>();
            result.AddRange(DeduplicateExtList(externals).Cast<ExtensionManifest>());
            result.AddRange(DeduplicateExtList(user).Cast<ExtensionManifest>());
            result.AddRange(DeduplicateExtList(install).Cast<ExtensionManifest>());
            return result;
        }

        private bool VerifyAssemblyReferences(Assembly asm, ExtensionManifest manifest)
        {
            var references = asm.GetReferencedAssemblies();
            if (references.Any(a => a.Name == "Playnite" || a.Name == "Playnite.Common") &&
                !BuiltinExtensions.BuiltinExtensionIds.Contains(manifest.Id))
            {
                logger.Error($"Unsupported Playnite assemblies are referenced by {manifest.Name} plugin.");
                return false;
            }

            var sdkReference = references.FirstOrDefault(a => a.Name == "Playnite.SDK");
            if (sdkReference == null)
            {
                logger.Error($"Assembly doesn't reference Playnite SDK.");
                return false;
            }

            if (sdkReference.Version.Major != SDK.SdkVersions.SDKVersion.Major ||
                sdkReference.Version > SDK.SdkVersions.SDKVersion)
            {
                logger.Error($"Plugin doesn't support current version of Playnite SDK, supports {sdkReference.Version}");
                return false;
            }

            return true;
        }

        public bool LoadScripts(List<string> ignoreList, bool builtInOnly, List<string> externals)
        {
            var allSuccess = true;
            DisposeScripts();
            var manifests = GetInstalledManifests(externals).Where(a => a.Type == ExtensionType.Script && !ignoreList.Contains(a.Id)).ToList();
            foreach (var desc in manifests)
            {
                if (desc.Id.IsNullOrWhiteSpace())
                {
                    var message = $"Extension {desc.Name}, doesn't have ID.";
                    logger.Error(message);
                    AddLoadFailure(desc, AddonLoadError.Uknown, message);
                    continue;
                }

                if (desc.Module.IsNullOrWhiteSpace())
                {
                    var message = $"Extension {desc.Name}, doesn't have module specified.";
                    logger.Error(message);
                    AddLoadFailure(desc, AddonLoadError.Uknown, message);
                    continue;
                }

                if (builtInOnly && !BuiltinExtensions.BuiltinExtensionIds.Contains(desc.Id))
                {
                    logger.Warn($"Skipping load of {desc.Name}, builtInOnly is enabled.");
                    continue;
                }

                PlayniteScript script = null;
                var scriptPath = Path.Combine(Path.GetDirectoryName(desc.DescriptionPath), desc.Module);
                if (!File.Exists(scriptPath))
                {
                    var message = $"Cannot load script extension, {scriptPath} not found.";
                    logger.Error(message);
                    AddLoadFailure(desc, AddonLoadError.Uknown, message);
                    continue;
                }

                try
                {
                    script = PlayniteScript.FromFile(scriptPath, $"{desc.DirectoryName}#PS");
                    if (script == null)
                    {
                        AddLoadFailure(
                            desc,
                            AddonLoadError.Uknown,
                            $"Script extension {scriptPath} did not produce a PowerShell runtime.");
                        continue;
                    }

                    addonLocalizationLoader?.Invoke(desc.DirectoryPath);
                    script.SetVariable("PlayniteApi", apiGenerator(desc));
                    script.SetVariable("CurrentExtensionInstallPath", desc.DirectoryPath);
                    if (!desc.Id.IsNullOrEmpty())
                    {
                        var extDir = Path.Combine(PlaynitePaths.ExtensionsDataPath, Paths.GetSafePathName(desc.Id));
                        FileSystem.CreateDirectory(extDir);
                        script.SetVariable("CurrentExtensionDataPath", extDir);
                    }
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    allSuccess = false;
                    logger.Error(e, $"Failed to load script file {scriptPath}");
                    AddLoadFailure(desc, AddonLoadError.Uknown, $"Failed to load script file {scriptPath}.", e);
                    continue;
                }

                Scripts.Add(script);
                logger.Info($"Loaded script extension: {scriptPath}, version {desc.Version}");
            }

            return allSuccess;
        }

        public void LoadPlugins(List<string> ignoreList, bool builtInOnly, List<string> externals)
        {
            if (Plugins.HasItems())
            {
                throw new Exception("Plugin can be loaded only once!");
            }

            var manifests = GetInstalledManifests(externals).Where(a => a.Type != ExtensionType.Script && ignoreList?.Contains(a.Id) != true).ToList();
            foreach (var desc in manifests)
            {
                AssemblyLoadContext loadContext = null;
                var loadedAny = false;
                if (desc.Id.IsNullOrEmpty())
                {
                    var message = $"Extension {desc.Name}, doesn't have ID.";
                    logger.Error(message);
                    AddLoadFailure(desc, AddonLoadError.Uknown, message);
                    continue;
                }

                if (desc.Module.IsNullOrWhiteSpace())
                {
                    var message = $"Extension {desc.Name}, doesn't have module specified.";
                    logger.Error(message);
                    AddLoadFailure(desc, AddonLoadError.Uknown, message);
                    continue;
                }

                if (builtInOnly && !BuiltinExtensions.BuiltinExtensionIds.Contains(desc.Id))
                {
                    logger.Warn($"Skipping load of {desc.Name}, builtInOnly is enabled.");
                    continue;
                }

                try
                {
                    addonLocalizationLoader?.Invoke(desc.DirectoryPath);
                    var pluginTypes = LoadPluginTypes(desc, out loadContext);
                    foreach (var pluginType in pluginTypes)
                    {
                        var plugin = (Plugin)Activator.CreateInstance(pluginType, new object[] { apiGenerator(desc) });
                        if (plugin.Id == default)
                        {
                            logger.Error($"Plugin {plugin.GetType()} doesn't have plugin ID specified.");
                            continue;
                        }

                        if (Plugins.ContainsKey(plugin.Id))
                        {
                            logger.Warn($"Plugin {plugin.Id} is already loaded.");
                            continue;
                        }

                        Plugins.Add(plugin.Id, new LoadedPlugin(plugin, desc));
                        loadedAny = true;
                        logger.Info($"Loaded plugin: {desc.Name}, version {desc.Version}");
                    }
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(e.InnerException, $"Failed to load plugin: {desc.Name}");
                    if (e.InnerException == null)
                    {
                        logger.Error(e, string.Empty);
                    }

                    if (e is ReflectionTypeLoadException reflectionTypeLoadException)
                    {
                        foreach (var loaderException in reflectionTypeLoadException.LoaderExceptions)
                        {
                            logger.Error(loaderException, string.Empty);
                        }
                    }

                    var details = e is ReflectionTypeLoadException reflectionFailure
                        ? string.Join(
                            Environment.NewLine,
                            reflectionFailure.LoaderExceptions
                                .Where(loaderException => loaderException != null)
                                .Select(loaderException => loaderException.ToString()))
                        : null;
                    AddLoadFailure(
                        desc,
                        AddonLoadError.Uknown,
                        details ?? $"Failed to load plugin {desc.Name}.",
                        e);
                }
                finally
                {
                    if (loadedAny)
                    {
                        pluginLoadContexts.Add(loadContext);
                    }
                    else
                    {
                        UnloadFailedContext(loadContext);
                    }
                }
            }
        }

        private List<Type> LoadPluginTypes(
            ExtensionManifest descriptor,
            out AssemblyLoadContext loadContext)
        {
            var asmPath = Path.Combine(Path.GetDirectoryName(descriptor.DescriptionPath), descriptor.Module);
            var extensionContext = new ExtensionAssemblyLoadContext(asmPath);
            loadContext = extensionContext;
            var assembly = extensionContext.LoadFromAssemblyPath(Path.GetFullPath(asmPath));
            var pluginTypes = new List<Type>();
            if (VerifyAssemblyReferences(assembly, descriptor))
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.IsInterface || type.IsAbstract)
                    {
                        continue;
                    }
                    else
                    {
                        if (typeof(GenericPlugin).IsAssignableFrom(type) || typeof(LibraryPlugin).IsAssignableFrom(type) || typeof(MetadataPlugin).IsAssignableFrom(type))
                        {
                            var ignore = Attribute.IsDefined(type, typeof(IgnorePluginAttribute));
                            var load = Attribute.IsDefined(type, typeof(LoadPluginAttribute));
                            if ((ignore && load) || !ignore)
                            {
                                pluginTypes.Add(type);
                            }
                        }
                    }
                }
            }
            else
            {
                logger.Error($"Plugin dependencices are not compatible: {descriptor.Name}");
                AddLoadFailure(
                    descriptor,
                    AddonLoadError.SDKVersion,
                    $"Plugin dependencies are not compatible with SDK {SDK.SdkVersions.SDKVersion}.");
            }

            return pluginTypes;
        }

        private static void UnloadFailedContext(AssemblyLoadContext loadContext)
        {
            loadContext?.Unload();
        }

        private void AddLoadFailure(
            ExtensionManifest manifest,
            AddonLoadError error,
            string message,
            Exception exception = null)
        {
            FailedExtensions.Add((manifest, error));
            LoadFailures.Add(new ExtensionLoadFailure(manifest, error, message, exception));
        }

        private sealed class ExtensionAssemblyLoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver dependencyResolver;
            private readonly string extensionDirectory;

            public ExtensionAssemblyLoadContext(string assemblyPath) : base(isCollectible: true)
            {
                assemblyPath = Path.GetFullPath(assemblyPath);
                dependencyResolver = new AssemblyDependencyResolver(assemblyPath);
                extensionDirectory = Path.GetDirectoryName(assemblyPath);
            }

            protected override Assembly Load(AssemblyName assemblyName)
            {
                if (string.Equals(assemblyName.Name, "Playnite.SDK", StringComparison.OrdinalIgnoreCase))
                {
                    return typeof(Plugin).Assembly;
                }

                var assemblyPath = dependencyResolver.ResolveAssemblyToPath(assemblyName) ??
                    Path.Combine(extensionDirectory, assemblyName.Name + ".dll");
                return File.Exists(assemblyPath) ? LoadFromAssemblyPath(assemblyPath) : null;
            }

            protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
            {
                var libraryPath = dependencyResolver.ResolveUnmanagedDllToPath(unmanagedDllName) ??
                    Path.Combine(extensionDirectory, unmanagedDllName);
                if (!File.Exists(libraryPath) && !Path.HasExtension(unmanagedDllName))
                {
                    libraryPath += ".dll";
                }

                return File.Exists(libraryPath) ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
            }
        }

        public bool InvokeExtension(ExtensionFunction function, out Exception error)
        {
            try
            {
                logger.Debug($"Invoking extension function {function}");
                function.Invoke();
                error = null;
                return true;
            }
            catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
            {
                logger.Error(e, $"Failed to execute extension function.");
                error = e;
                return false;
            }
        }

        private void Controllers_Uninstalled(object sender, GameUninstalledEventArgs args)
        {
            if (args.Source?.Game == null)
            {
                logger.Error("No game controller information found!");
                return;
            }

            var callbackArgs = new SDK.Events.OnGameUninstalledEventArgs { Game = args.Source.Game };
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameUninstalled(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameUninstalled method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameUninstalled(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameUninstalled method from {plugin.Description.Name} plugin.");
                }
            }
        }

        private void Controllers_Stopped(object sender, GameStoppedEventArgs args)
        {
            if (args.Source?.Game?.Id == null)
            {
                logger.Error("No game controller information found!");
                return;
            }
        }

        public void InvokeOnGameStopped(Game game, ulong ellapsedTime, bool manuallyStopped)
        {
            var callbackArgs = new SDK.Events.OnGameStoppedEventArgs
            {
                Game = database.Games[game.Id],
                ElapsedSeconds = ellapsedTime,
                ManuallyStopped = manuallyStopped
            };

            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameStopped(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStopped method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameStopped(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStopped method from {plugin.Description.Name} plugin.");
                }
            }
        }

        private void Controllers_StartupCancelled(object sender, OnGameStartupCancelledEventArgs args)
        {
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameStartupCancelled(args);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStartupCancelled method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameStartupCancelled(args);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStartupCancelled method from {plugin.Description.Name} plugin.");
                }
            }
        }

        private void Controllers_Starting(object sender, OnGameStartingEventArgs args)
        {
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameStarting(args);
                    if (args.CancelStartup)
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStarting method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameStarting(args);
                    if (args.CancelStartup)
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStarting method from {plugin.Description.Name} plugin.");
                }
            }
        }

        private void Controllers_Started(object sender, GameStartedEventArgs args)
        {
            if (args.Source?.Game?.Id == null)
            {
                logger.Error("No game controller information found!");
                return;
            }

            var callbackArgs = new OnGameStartedEventArgs
            {
                Game = database.Games[args.Source.Game.Id],
                SourceAction = (args.Source as GenericPlayController)?.StartingArgs?.SourceAction?.GetClone(),
                SelectedRomFile = (args.Source as GenericPlayController)?.StartingArgs.SelectedRomFile,
                StartedProcessId = args.StartedProcessId
            };

            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameStarted(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStarted method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameStarted(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameStarted method from {plugin.Description.Name} plugin.");
                }
            }
        }

        private void Controllers_Installed(object sender, GameInstalledEventArgs args)
        {
            if (args.Source?.Game?.Id == null)
            {
                logger.Error("No game controller information found!");
                return;
            }

            var callbackArgs = new SDK.Events.OnGameInstalledEventArgs { Game = database.Games[args.Source.Game.Id] };
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameInstalled(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameInstalled method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameInstalled(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameInstalled method from {plugin.Description.Name} plugin.");
                }
            }
        }

        private void Controllers_InstallationCancelled(object sender, GameInstallationCancelledEventArgs args)
        {
            if (args.Source?.Game?.Id == null)
            {
                logger.Error("No game controller information found!");
                return;
            }

            var callbackArgs = new SDK.Events.OnGameInstallationCancelledEventArgs { Game = database.Games[args.Source.Game.Id] };
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameInstallationCancelled(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameInstallationCancelled method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameInstallationCancelled(callbackArgs);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameInstallationCancelled method from {plugin.Description.Name} plugin.");
                }
            }
        }

        public void InvokeOnGameSelected(List<Game> oldValue, List<Game> newValue)
        {
            var args = new SDK.Events.OnGameSelectedEventArgs(oldValue, newValue);
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnGameSelected(args);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameSelected method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnGameSelected(args);
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnGameSelected method from {plugin.Description.Name} plugin.");
                }
            }
        }

        public void NotifiyOnApplicationStarted()
        {
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnApplicationStarted();
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnApplicationStarted method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnApplicationStarted(new SDK.Events.OnApplicationStartedEventArgs());
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnApplicationStarted method from {plugin.Description.Name} plugin.");
                }
            }
        }

        public void NotifiyOnApplicationStopped()
        {
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnApplicationStopped();
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnApplicationStopped method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnApplicationStopped(new SDK.Events.OnApplicationStoppedEventArgs());
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnApplicationStopped method from {plugin.Description.Name} plugin.");
                }
            }
        }

        public void NotifiyOnLibraryUpdated()
        {
            foreach (var script in Scripts)
            {
                try
                {
                    script.OnLibraryUpdated();
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnLibraryUpdated method from {script.Name} script.");
                }
            }

            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    plugin.Plugin.OnLibraryUpdated(new SDK.Events.OnLibraryUpdatedEventArgs());
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Failed to execute OnLibraryUpdated method from {plugin.Description.Name} plugin.");
                }
            }
        }

        public LibraryPlugin GetLibraryPlugin(Guid pluginId)
        {
            if (pluginId == Guid.Empty)
            {
                return null;
            }

            return LibraryPlugins.FirstOrDefault(a => a.Id == pluginId);
        }

        public List<PluginUiElementSupport> CustomElementList = new List<PluginUiElementSupport>();
        public void AddCustomElementSupport(Plugin source, AddCustomElementSupportArgs args)
        {
            if (CustomElementList.Any(a => a.Source == source))
            {
                return;
            }

            var elemSupport = args.GetClone<AddCustomElementSupportArgs, PluginUiElementSupport>();
            elemSupport.Source = source;
            CustomElementList.Add(elemSupport);
        }

        public List<PluginSettingsSupport> SettingsSupportList = new List<PluginSettingsSupport>();
        public void AddSettingsSupport(Plugin source, AddSettingsSupportArgs args)
        {
            if (SettingsSupportList.Any(a => a.Source == source))
            {
                return;
            }

            var elemSupport = args.GetClone<AddSettingsSupportArgs, PluginSettingsSupport>();
            elemSupport.Source = source;
            SettingsSupportList.Add(elemSupport);
        }

        public List<PluginConvertersSupport> ConvertersSupportList = new List<PluginConvertersSupport>();
        public void AddConvertersSupport(Plugin source, AddConvertersSupportArgs args)
        {
            if (ConvertersSupportList.Any(a => a.Source == source))
            {
                return;
            }

            ConvertersSupportList.Add(new PluginConvertersSupport
            {
                Source = source,
                Converters = args.Converters,
                SourceName = args.SourceName
            });
        }

        public List<TopPanelItem> GetTopPanelPluginItems()
        {
            var res = new List<TopPanelItem>();
            foreach (var plugin in Plugins.Values)
            {
                try
                {
                    var items = plugin.Plugin.GetTopPanelItems().ToList();
                    if (items.HasItems())
                    {
                        res.AddRange(items);
                    }
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(e, $"Failed to get top panel itesm from {plugin.Description.Id}");
                }
            }

            return res;
        }
    }

    public class PluginUiElementSupport : AddCustomElementSupportArgs
    {
        public Plugin Source { get; set; }
    }

    public class PluginSettingsSupport : AddSettingsSupportArgs
    {
        public Plugin Source { get; set; }
    }

    public class PluginConvertersSupport : AddConvertersSupportArgs
    {
        public Plugin Source { get; set; }
    }
}
