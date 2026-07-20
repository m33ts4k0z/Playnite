using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Diagnostics;

namespace Playnite.Avalonia.Controls;

public sealed class HtmlTextView : StackPanel
{
    public static readonly StyledProperty<string> HtmlProperty =
        AvaloniaProperty.Register<HtmlTextView, string>(nameof(Html));

    public static readonly StyledProperty<IBrush> ForegroundProperty =
        AvaloniaProperty.Register<HtmlTextView, IBrush>(nameof(Foreground), inherits: true);

    public string Html
    {
        get => GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    public IBrush Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public HtmlTextView()
    {
        Orientation = Orientation.Vertical;
        Spacing = 8;
        HtmlProperty.Changed.AddClassHandler<HtmlTextView>((view, _) => view.Rebuild());
        ForegroundProperty.Changed.AddClassHandler<HtmlTextView>((view, _) => view.Rebuild());
    }

    private void Rebuild()
    {
        Children.Clear();
        if (string.IsNullOrWhiteSpace(Html))
        {
            return;
        }

        var document = new HtmlParser().ParseDocument(Html);
        foreach (var node in document.Body?.ChildNodes ?? document.ChildNodes)
        {
            AddBlock(node);
        }
    }

    private void AddBlock(INode node)
    {
        if (node is IText text)
        {
            AddPlainParagraph(text.Data);
            return;
        }
        if (node is not IElement element)
        {
            return;
        }

        switch (element.TagName)
        {
            case "UL":
            case "OL":
                var index = 1;
                foreach (var item in element.Children.Where(child => child.TagName == "LI"))
                {
                    AddInlineParagraph(item, element.TagName == "OL" ? $"{index++}. " : "• ");
                }
                break;
            case "H1":
            case "H2":
            case "H3":
                AddInlineParagraph(element, string.Empty, FontWeight.Bold, element.TagName == "H1" ? 22 : 18);
                break;
            case "BR":
                Children.Add(new Border { Height = 4 });
                break;
            case "IMG":
                AddPlainParagraph(element.GetAttribute("alt"));
                break;
            default:
                AddInlineParagraph(element);
                break;
        }
    }

    private void AddPlainParagraph(string value)
    {
        value = Normalize(value);
        if (value.Length == 0)
        {
            return;
        }

        Children.Add(new TextBlock
        {
            Text = value,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 23,
            Foreground = Foreground
        });
    }

    private void AddInlineParagraph(
        IElement element,
        string prefix = "",
        FontWeight? paragraphWeight = null,
        double? paragraphSize = null)
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrEmpty(prefix))
        {
            panel.Children.Add(CreateText(prefix, paragraphWeight, paragraphSize, false, Foreground));
        }
        AddInlineNodes(panel, element.ChildNodes, paragraphWeight, false, paragraphSize, Foreground);
        if (panel.Children.Count > 0)
        {
            Children.Add(panel);
        }
    }

    private static void AddInlineNodes(
        WrapPanel panel,
        INodeList nodes,
        FontWeight? inheritedWeight,
        bool inheritedItalic,
        double? inheritedSize,
        IBrush foreground)
    {
        foreach (var node in nodes)
        {
            if (node is IText text)
            {
                var value = Normalize(text.Data);
                if (value.Length > 0)
                {
                    panel.Children.Add(CreateText(value, inheritedWeight, inheritedSize, inheritedItalic, foreground));
                }
                continue;
            }
            if (node is not IElement element)
            {
                continue;
            }

            var bold = element.TagName is "B" or "STRONG" ? FontWeight.Bold : inheritedWeight;
            var italic = inheritedItalic || element.TagName is "I" or "EM";
            if (element.TagName == "A")
            {
                var link = element.GetAttribute("href");
                var label = Normalize(element.TextContent);
                if (label.Length > 0)
                {
                    var button = new Button
                    {
                        Content = label,
                        Padding = new Thickness(3, 0),
                        Margin = new Thickness(1, 0)
                    };
                    button.Click += (_, _) => OpenLink(link);
                    panel.Children.Add(button);
                }
            }
            else if (element.TagName == "BR")
            {
                panel.Children.Add(CreateText(Environment.NewLine, inheritedWeight, inheritedSize, inheritedItalic, foreground));
            }
            else
            {
                AddInlineNodes(panel, element.ChildNodes, bold, italic, inheritedSize, foreground);
            }
        }
    }

    private static TextBlock CreateText(
        string value,
        FontWeight? weight,
        double? size,
        bool italic,
        IBrush foreground)
    {
        var text = new TextBlock
        {
            Text = value,
            FontWeight = weight ?? FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Foreground = foreground
        };
        if (size.HasValue)
        {
            text.FontSize = size.Value;
        }
        return text;
    }

    private static void OpenLink(string link)
    {
        if (Uri.TryCreate(link, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private static string Normalize(string value) =>
        string.Join(" ", (value ?? string.Empty).Split(
            (char[])null,
            StringSplitOptions.RemoveEmptyEntries));
}
