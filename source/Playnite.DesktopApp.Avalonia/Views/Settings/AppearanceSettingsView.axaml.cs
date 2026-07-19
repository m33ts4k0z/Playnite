using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Playnite.DesktopApp.Avalonia.Views.Settings;

public sealed partial class AppearanceSettingsView : UserControl
{
    public AppearanceSettingsView() => AvaloniaXamlLoader.Load(this);
}
