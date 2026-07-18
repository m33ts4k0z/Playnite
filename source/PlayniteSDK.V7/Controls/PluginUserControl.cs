using Avalonia;
using Avalonia.Controls;
using Playnite.SDK.Models;

namespace Playnite.SDK.Controls;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class PluginUserControl : UserControl
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public static readonly StyledProperty<Game> GameContextProperty =
        AvaloniaProperty.Register<PluginUserControl, Game>(nameof(GameContext));

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Game GameContext
    {
        get => GetValue(GameContextProperty);
        set => SetValue(GameContextProperty, value);
    }
}
