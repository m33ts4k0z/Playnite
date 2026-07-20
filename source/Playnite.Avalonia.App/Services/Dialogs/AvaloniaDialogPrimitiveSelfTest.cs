using Avalonia.Controls;
using Playnite.Avalonia.Controls;
using Playnite.SDK;

namespace Playnite.Avalonia.App.Services;

public static class AvaloniaDialogPrimitiveSelfTest
{
    public static string ValidateConstructionAndRoundTrip()
    {
        var toggle = new MessageBoxToggle("Synthetic toggle", false);
        var textInput = new TextBox { Text = "seed" };
        toggle.Selected = true;
        textInput.Text = "round trip";
        var inputResult = new StringSelectionDialogResult(toggle.Selected, textInput.Text);

        var searchResults = new List<GenericItemOption>
        {
            new("Search item", "Synthetic search result")
        };
        var searchList = new ListBox { ItemsSource = searchResults, SelectedIndex = 0 };
        var imageOption = new ImageFileOption("synthetic-image.png") { Description = "Image" };
        var image = new GameCoverImage { SourcePath = imageOption.Path };
        var progress = new ProgressBar { Maximum = 10, Value = 4, IsIndeterminate = false };

        var single = new AvaloniaSelectionResult<int>(true, new[] { 7 });
        var multiple = new AvaloniaSelectionResult<int>(true, new[] { 2, 3, 5 });
        var progressResult = new GlobalProgressResult(true, false, null);
        var unrouted = typeof(IDialogsFactory)
            .GetMethods()
            .Where(method => !AvaloniaSdkDialogRouter.Supports(method))
            .Select(method => method.Name)
            .Distinct()
            .ToList();

        if (!inputResult.Result || inputResult.SelectedString != "round trip" ||
            !ReferenceEquals(searchList.ItemsSource, searchResults) ||
            image.SourcePath != imageOption.Path ||
            progress.Value != 4 ||
            single.SelectedItem != 7 ||
            !multiple.SelectedItems.SequenceEqual(new[] { 2, 3, 5 }) ||
            progressResult.Result != true || progressResult.Canceled ||
            unrouted.Count != 0)
        {
            throw new InvalidOperationException(
                "One or more shared dialog primitives failed synthetic construction or round-trip validation.");
        }

        return "text/toggle, search, single/multi-select, image, progress, selectable-text, " +
            $"and all {typeof(IDialogsFactory).GetMethods().Length} SDK v6 dialog members constructed";
    }
}
