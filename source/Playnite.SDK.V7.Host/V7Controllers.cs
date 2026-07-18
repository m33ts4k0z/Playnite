using Playnite.SDK.Plugins;

namespace Playnite.SDK.V7.Host;

public sealed class V7ControllerInstance : IDisposable
{
    private readonly ControllerBase controller;
    private readonly Func<string, string, string> hostCall;
    private bool disposed;

    public Guid Token { get; } = Guid.NewGuid();
    public string Name => controller.Name;
    public string Kind => controller switch
    {
        PlayController => "Play",
        InstallController => "Install",
        UninstallController => "Uninstall",
        _ => "Unknown"
    };

    internal V7ControllerInstance(
        ControllerBase controller,
        Func<string, string, string> hostCall)
    {
        this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
        this.hostCall = hostCall ?? throw new ArgumentNullException(nameof(hostCall));
        if (controller is PlayController play)
        {
            play.Started += Play_Started;
            play.Stopped += Play_Stopped;
        }
        else if (controller is InstallController install)
        {
            install.Installed += Install_Installed;
            install.InstallCancelled += Install_Cancelled;
        }
        else if (controller is UninstallController uninstall)
        {
            uninstall.Uninstalled += Uninstall_Uninstalled;
        }
    }

    public void Execute()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        switch (controller)
        {
            case PlayController play:
                play.Play(new PlayActionArgs());
                break;
            case InstallController install:
                install.Install(new InstallActionArgs());
                break;
            case UninstallController uninstall:
                uninstall.Uninstall(new UninstallActionArgs());
                break;
            default:
                throw new NotSupportedException($"Unknown SDK v7 controller type {controller.GetType().FullName}.");
        }
    }

    private void Play_Started(object sender, GameStartedEventArgs args) =>
        Publish("Started", new { args.StartedProcessId });

    private void Play_Stopped(object sender, GameStoppedEventArgs args) =>
        Publish("Stopped", new { args.SessionLength });

    private void Install_Installed(object sender, GameInstalledEventArgs args) =>
        Publish("Installed", new { args.InstalledInfo });

    private void Install_Cancelled(object sender, GameInstallationCancelledEventArgs args) =>
        Publish("InstallCancelled", null);

    private void Uninstall_Uninstalled(object sender, GameUninstalledEventArgs args) =>
        Publish("Uninstalled", null);

    private void Publish(string eventName, object data) => hostCall(
        "ControllerEvent",
        V7RpcJson.Serialize(new { Token, Event = eventName, Data = data }));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (controller is PlayController play)
        {
            play.Started -= Play_Started;
            play.Stopped -= Play_Stopped;
        }
        else if (controller is InstallController install)
        {
            install.Installed -= Install_Installed;
            install.InstallCancelled -= Install_Cancelled;
        }
        else if (controller is UninstallController uninstall)
        {
            uninstall.Uninstalled -= Uninstall_Uninstalled;
        }
        controller.Dispose();
    }
}
