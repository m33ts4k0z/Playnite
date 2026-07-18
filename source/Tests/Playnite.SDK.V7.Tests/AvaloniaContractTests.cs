using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Threading;
using NUnit.Framework;
using Playnite.SDK.Controls;
using Playnite.SDK.Plugins;

namespace Playnite.SDK.V7.Tests;

[TestFixture]
public class AvaloniaContractTests
{
    [Test]
    public void AssemblyReportsSdkVersionSeven()
    {
        Assert.That(SdkVersions.SDKVersion.Major, Is.EqualTo(7));
    }

    [Test]
    public void AssemblyDoesNotReferenceWpf()
    {
        var references = typeof(IPlayniteAPI).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .ToArray();

        Assert.That(references, Does.Not.Contain("PresentationFramework"));
        Assert.That(references, Does.Not.Contain("PresentationCore"));
        Assert.That(references, Does.Not.Contain("WindowsBase"));
        Assert.That(references, Does.Contain("Avalonia.Base"));
    }

    [Test]
    public void PluginViewsUseAvaloniaControls()
    {
        Assert.That(GetReturnType(typeof(Plugin), nameof(Plugin.GetSettingsView)), Is.EqualTo(typeof(Control)));
        Assert.That(GetReturnType(typeof(Plugin), nameof(Plugin.GetGameViewControl)), Is.EqualTo(typeof(Control)));
        Assert.That(GetReturnType(typeof(IDialogsFactory), nameof(IDialogsFactory.CreateWindow)), Is.EqualTo(typeof(Window)));
    }

    [Test]
    public void MainViewUsesAvaloniaDispatcher()
    {
        Assert.That(typeof(IMainViewAPI).GetProperty(nameof(IMainViewAPI.UIDispatcher))?.PropertyType,
            Is.EqualTo(typeof(Dispatcher)));
    }

    [Test]
    public void SettingsUseAvaloniaDock()
    {
        Assert.That(typeof(IPlayniteSettingsAPI).GetProperty(nameof(IPlayniteSettingsAPI.SidebarPosition))?.PropertyType,
            Is.EqualTo(typeof(Dock)));
    }

    [Test]
    public void PluginConvertersUseAvaloniaConverterContract()
    {
        var converterListType = typeof(AddConvertersSupportArgs)
            .GetProperty(nameof(AddConvertersSupportArgs.Converters))?.PropertyType;

        Assert.That(converterListType, Is.EqualTo(typeof(List<IValueConverter>)));
    }

    [Test]
    public void WebResourcesExposeOwnedResponseStream()
    {
        Assert.That(typeof(WebViewResourceLoadedEventArgs)
            .GetProperty(nameof(WebViewResourceLoadedEventArgs.ResponseContent))?.PropertyType,
            Is.EqualTo(typeof(Stream)));
        Assert.That(typeof(IDisposable).IsAssignableFrom(typeof(WebViewResourceLoadedEventArgs)), Is.True);
    }

    [Test]
    public void PluginUserControlUsesStyledGameContext()
    {
        Assert.That(typeof(UserControl).IsAssignableFrom(typeof(PluginUserControl)), Is.True);
        Assert.That(PluginUserControl.GameContextProperty, Is.InstanceOf<StyledProperty<Playnite.SDK.Models.Game>>());
    }

    [Test]
    public void RelayCommandsUseAvaloniaKeyGestures()
    {
        Assert.That(typeof(RelayCommand).GetProperty(nameof(RelayCommand.Gesture))?.PropertyType,
            Is.EqualTo(typeof(KeyGesture)));
    }

    private static Type GetReturnType(Type type, string methodName)
    {
        return type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)?.ReturnType;
    }
}
