using Avalonia;
using Avalonia.Controls;

namespace Playnite.SDK.Plugins;

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public enum SiderbarItemType
{
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    Button = 0,
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    View = 1
}

/// <summary>Defines this Playnite SDK v7 contract member.</summary>
public class SidebarItem : ObservableObject
{
    private object icon;
    private string title;
    private bool visible = true;
    private double progressValue;
    private double progressMaximum = 100;
    private Thickness iconPadding = new(8);

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public SiderbarItemType Type { get; set; }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public object Icon
    {
        get => icon;
        set => SetValue(ref icon, value);
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public string Title
    {
        get => title;
        set => SetValue(ref title, value);
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public bool Visible
    {
        get => visible;
        set => SetValue(ref visible, value);
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public double ProgressValue
    {
        get => progressValue;
        set => SetValue(ref progressValue, value);
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public double ProgressMaximum
    {
        get => progressMaximum;
        set => SetValue(ref progressMaximum, value);
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Thickness IconPadding
    {
        get => iconPadding;
        set => SetValue(ref iconPadding, value);
    }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Action Activated { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Func<Control> Opened { get; set; }
    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public Action Closed { get; set; }

    /// <summary>Defines this Playnite SDK v7 contract member.</summary>
    public SidebarItem()
    {
        if (ResourceProvider.GetResource("SidebarItemPadding") is Thickness padding)
        {
            iconPadding = padding;
        }
    }
}
