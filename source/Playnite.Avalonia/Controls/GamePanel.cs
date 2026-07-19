using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Playnite.Avalonia.Controls;

/// <summary>
/// First lookless Playnite Avalonia control. Its complete visual tree comes
/// from a loose theme while code owns only state and the PART_* contract.
/// </summary>
public class GamePanel : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<GamePanel, string>(nameof(Title), "Untitled");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IReadOnlyList<string> ResolvedParts => resolvedParts;
    public int TemplateAppliedCount { get; private set; }
    public string TemplateMarker { get; private set; } = "(template never applied)";

    private readonly List<string> resolvedParts = new();
    private Button actionButton;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (actionButton != null)
        {
            actionButton.Click -= ActionButtonClick;
            actionButton = null;
        }

        TemplateAppliedCount++;
        resolvedParts.Clear();

        if (e.NameScope.Find<TextBlock>("PART_TitleText") != null)
        {
            resolvedParts.Add("PART_TitleText");
        }

        actionButton = e.NameScope.Find<Button>("PART_ActionButton");
        if (actionButton != null)
        {
            resolvedParts.Add("PART_ActionButton");
            actionButton.Click += ActionButtonClick;
        }

        TemplateMarker = e.NameScope.Find<TextBlock>("PART_ThemeMarker")?.Text
            ?? "(no PART_ThemeMarker in template)";
    }

    private void ActionButtonClick(object sender, RoutedEventArgs e)
    {
        Title = $"Clicked at {DateTime.Now:HH:mm:ss}";
    }
}
