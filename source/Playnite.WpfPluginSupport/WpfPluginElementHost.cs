using Avalonia.Platform;
using Playnite.Avalonia.Controls;
using Playnite.Plugins;
using Playnite.SDK;
using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System.Windows.Interop;
using LegacyControl = System.Windows.Controls.Control;

namespace Playnite.WpfPluginSupport;

public sealed class WpfPluginElementHost : global::Avalonia.Controls.NativeControlHost,
    IPluginElementContextSink
{
    private const int ChildWindowStyle = 0x40000000;
    private const int VisibleWindowStyle = 0x10000000;
    private const int ClipSiblingsWindowStyle = 0x04000000;
    private const int ClipChildrenWindowStyle = 0x02000000;

    private readonly LegacyControl legacyControl;
    private HwndSource source;
    private object gameContext;

    public LegacyControl LegacyControl => legacyControl;
    public IntPtr NativeHandle => source?.Handle ?? IntPtr.Zero;

    public object GameContext
    {
        set
        {
            gameContext = value;
            ApplyGameContext();
        }
    }

    public WpfPluginElementHost(LegacyControl legacyControl, object gameContext)
    {
        this.legacyControl = legacyControl ?? throw new ArgumentNullException(nameof(legacyControl));
        this.gameContext = gameContext;
        ApplyGameContext();
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        WpfPluginSupportRuntime.EnsureApplication();
        source = new HwndSource(new HwndSourceParameters
        {
            ParentWindow = parent.Handle,
            WindowStyle = ChildWindowStyle | VisibleWindowStyle |
                ClipSiblingsWindowStyle | ClipChildrenWindowStyle,
            Width = 1,
            Height = 1
        })
        {
            RootVisual = legacyControl
        };
        return new PlatformHandle(source.Handle, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        if (source != null)
        {
            source.RootVisual = null;
            source.Dispose();
            source = null;
        }

        base.DestroyNativeControlCore(control);
    }

    private void ApplyGameContext()
    {
        if (legacyControl is PluginUserControl pluginControl)
        {
            pluginControl.GameContext = gameContext as Game;
        }
        else
        {
            legacyControl.DataContext = gameContext;
        }
    }
}

public static class WpfPluginElementFactory
{
    public static global::Avalonia.Controls.Control Create(
        ExtensionFactory extensions,
        ApplicationMode mode,
        string pluginSource,
        string elementName,
        object gameContext)
    {
        if (extensions == null || string.IsNullOrWhiteSpace(pluginSource) ||
            string.IsNullOrWhiteSpace(elementName))
        {
            return null;
        }

        var support = extensions.CustomElementList.FirstOrDefault(item =>
            string.Equals(item.SourceName, pluginSource, StringComparison.OrdinalIgnoreCase) &&
            item.ElementList?.Contains(elementName, StringComparer.Ordinal) == true);
        if (support == null)
        {
            return null;
        }

        var legacyControl = support.Source.GetGameViewControl(new GetGameViewControlArgs
        {
            Name = elementName,
            Mode = mode
        });
        return legacyControl == null ? null : new WpfPluginElementHost(legacyControl, gameContext);
    }
}
