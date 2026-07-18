using Newtonsoft.Json.Linq;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Playnite.Avalonia.App.Services;

internal interface IV7ControllerAdapter
{
    Guid Token { get; }
    void Dispatch(string eventName, JObject data);
}

internal sealed class V7RemoteController : IDisposable
{
    private readonly object instance;
    private readonly MethodInfo execute;
    private readonly MethodInfo dispose;
    private bool disposed;

    public Guid Token { get; }
    public string Name { get; }
    public string Kind { get; }

    public V7RemoteController(object instance)
    {
        this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        var type = instance.GetType();
        Token = ReadProperty<Guid>(type, nameof(Token));
        Name = ReadProperty<string>(type, nameof(Name));
        Kind = ReadProperty<string>(type, nameof(Kind));
        execute = GetRequiredMethod(type, "Execute");
        dispose = GetRequiredMethod(type, nameof(IDisposable.Dispose));
    }

    public void Execute() => Invoke(execute);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Invoke(dispose);
    }

    private T ReadProperty<T>(Type type, string name) =>
        (T)(type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMemberException(type.FullName, name)).GetValue(instance);

    private static MethodInfo GetRequiredMethod(Type type, string name) =>
        type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new MissingMethodException(type.FullName, name);

    private void Invoke(MethodInfo method)
    {
        try
        {
            method.Invoke(instance, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}

internal sealed class V7PlayControllerAdapter : PlayController, IV7ControllerAdapter
{
    private readonly V7RemoteController remote;
    private readonly Action<Guid> unregister;

    public Guid Token => remote.Token;

    public V7PlayControllerAdapter(
        Game game,
        V7RemoteController remote,
        Action<Guid> unregister)
        : base(game)
    {
        this.remote = remote;
        this.unregister = unregister;
        Name = remote.Name;
    }

    public override void Play(PlayActionArgs args) => remote.Execute();

    public void Dispatch(string eventName, JObject data)
    {
        if (eventName == "Started")
        {
            InvokeOnStarted(new GameStartedEventArgs
            {
                StartedProcessId = data?.Value<int>("StartedProcessId") ?? 0
            });
        }
        else if (eventName == "Stopped")
        {
            InvokeOnStopped(new GameStoppedEventArgs(data?.Value<ulong>("SessionLength") ?? 0));
        }
    }

    public override void Dispose()
    {
        unregister(Token);
        remote.Dispose();
        base.Dispose();
    }
}

internal sealed class V7InstallControllerAdapter : InstallController, IV7ControllerAdapter
{
    private readonly V7RemoteController remote;
    private readonly Action<Guid> unregister;

    public Guid Token => remote.Token;

    public V7InstallControllerAdapter(
        Game game,
        V7RemoteController remote,
        Action<Guid> unregister)
        : base(game)
    {
        this.remote = remote;
        this.unregister = unregister;
        Name = remote.Name;
    }

    public override void Install(InstallActionArgs args) => remote.Execute();

    public void Dispatch(string eventName, JObject data)
    {
        if (eventName == "Installed")
        {
            InvokeOnInstalled(new GameInstalledEventArgs(
                data?["InstalledInfo"]?.ToObject<GameInstallationData>()));
        }
        else if (eventName == "InstallCancelled")
        {
            InvokeOnInstallationCancelled(new GameInstallationCancelledEventArgs());
        }
    }

    public override void Dispose()
    {
        unregister(Token);
        remote.Dispose();
        base.Dispose();
    }
}

internal sealed class V7UninstallControllerAdapter : UninstallController, IV7ControllerAdapter
{
    private readonly V7RemoteController remote;
    private readonly Action<Guid> unregister;

    public Guid Token => remote.Token;

    public V7UninstallControllerAdapter(
        Game game,
        V7RemoteController remote,
        Action<Guid> unregister)
        : base(game)
    {
        this.remote = remote;
        this.unregister = unregister;
        Name = remote.Name;
    }

    public override void Uninstall(UninstallActionArgs args) => remote.Execute();

    public void Dispatch(string eventName, JObject data)
    {
        if (eventName == "Uninstalled")
        {
            InvokeOnUninstalled();
        }
    }

    public override void Dispose()
    {
        unregister(Token);
        remote.Dispose();
        base.Dispose();
    }
}
