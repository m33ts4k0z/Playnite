using Playnite.SDK;
using System.Reflection;

namespace Playnite.Avalonia.App.Services;

internal static class AvaloniaSdkDialogRouter
{
    private static readonly HashSet<string> supportedMethods = new(StringComparer.Ordinal)
    {
        "ShowMessage",
        "ShowErrorMessage",
        "SelectFolder",
        "SelectFile",
        "SelectFiles",
        "SelectIconFile",
        "SelectImagefile",
        "SaveFile",
        "SelectString",
        "ShowSelectableString",
        "ChooseImageFile",
        "ChooseItemWithSearch",
        "ActivateGlobalProgress",
        "CreateWindow",
        "GetCurrentAppWindow"
    };
    private const string IconFilter =
        "Icon Files|*.bmp;*.jpg*;*.jpeg*;*.png;*.gif;*.ico;*.tga;*.exe;*.tif;*.webp;*.avif";
    private const string ImageFilter =
        "Image Files|*.bmp;*.jpg*;*.jpeg*;*.png;*.gif;*.tga;*.tif;*.webp;*.avif";

    public static object Invoke(
        IAvaloniaDialogService dialogs,
        MethodInfo method,
        object[] arguments)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(method);
        arguments ??= Array.Empty<object>();

        switch (method.Name)
        {
            case "ShowMessage":
            case "ShowErrorMessage":
                return ShowMessage(dialogs, method, arguments);
            case "SelectFolder":
                return dialogs.SelectFolder(StringAt(arguments, 0)) ?? string.Empty;
            case "SelectFile":
                return dialogs.SelectFiles(StringAt(arguments, 0), false, StringAt(arguments, 1))
                    .FirstOrDefault() ?? string.Empty;
            case "SelectFiles":
                return dialogs.SelectFiles(StringAt(arguments, 0), true, StringAt(arguments, 1)).ToList();
            case "SelectIconFile":
                return dialogs.SelectFiles(IconFilter, false, StringAt(arguments, 0)).FirstOrDefault()
                    ?? string.Empty;
            case "SelectImagefile":
                return dialogs.SelectFiles(ImageFilter, false, StringAt(arguments, 0)).FirstOrDefault()
                    ?? string.Empty;
            case "SaveFile":
                return dialogs.SaveFile(
                    StringAt(arguments, 0),
                    arguments.OfType<bool>().FirstOrDefault(true),
                    arguments.OfType<string>().Skip(1).FirstOrDefault()) ?? string.Empty;
            case "SelectString":
                return dialogs.ShowInput(
                    StringAt(arguments, 0),
                    StringAt(arguments, 1),
                    StringAt(arguments, 2),
                    arguments.OfType<List<MessageBoxToggle>>().FirstOrDefault());
            case "ShowSelectableString":
                dialogs.ShowSelectableString(
                    StringAt(arguments, 0),
                    StringAt(arguments, 1),
                    StringAt(arguments, 2));
                return null;
            case "ChooseImageFile":
                return dialogs.ChooseImageFile(
                    arguments.OfType<List<ImageFileOption>>().FirstOrDefault()
                        ?? new List<ImageFileOption>(),
                    StringAt(arguments, 1),
                    DoubleAt(arguments, 2, 240),
                    DoubleAt(arguments, 3, 180));
            case "ChooseItemWithSearch":
                return dialogs.ChooseItemWithSearch(
                    arguments.OfType<List<GenericItemOption>>().FirstOrDefault()
                        ?? new List<GenericItemOption>(),
                    arguments.OfType<Func<string, List<GenericItemOption>>>().FirstOrDefault()
                        ?? throw new ArgumentNullException("searchFunction"),
                    StringAt(arguments, 2),
                    StringAt(arguments, 3));
            case "ActivateGlobalProgress":
                var options = arguments.OfType<GlobalProgressOptions>().FirstOrDefault()
                    ?? new GlobalProgressOptions(string.Empty);
                if (arguments.OfType<Action<GlobalProgressActionArgs>>().FirstOrDefault() is { } action)
                {
                    return dialogs.ActivateGlobalProgress(action, options);
                }
                if (arguments.OfType<Func<GlobalProgressActionArgs, Task>>().FirstOrDefault() is { } asyncAction)
                {
                    return dialogs.ActivateGlobalProgress(asyncAction, options);
                }
                throw new ArgumentException("A progress action is required.", nameof(arguments));
            case "CreateWindow":
                return dialogs.CreateLegacyWindow(
                    arguments.OfType<WindowCreationOptions>().FirstOrDefault()
                        ?? new WindowCreationOptions());
            case "GetCurrentAppWindow":
                return dialogs.GetCurrentLegacyWindow();
            default:
                throw new NotSupportedException(
                    $"The Avalonia dialog bridge does not implement SDK call {method.Name}.");
        }
    }

    internal static bool Supports(MethodInfo method) =>
        method != null && supportedMethods.Contains(method.Name);

    private static object ShowMessage(
        IAvaloniaDialogService dialogs,
        MethodInfo method,
        object[] arguments)
    {
        var message = StringAt(arguments, 0) ?? method.Name;
        var caption = StringAt(arguments, 1) ??
            (method.Name == "ShowErrorMessage" ? "Error" : "Playnite");
        var customOptions = arguments.OfType<List<MessageBoxOption>>().FirstOrDefault();
        if (customOptions != null)
        {
            if (customOptions.Count == 0)
            {
                throw new ArgumentException("At least one message option is required.", nameof(arguments));
            }

            var labels = customOptions.Select(option => option.Title ?? string.Empty).ToList();
            var defaultIndex = customOptions.FindIndex(option => option.IsDefault);
            var cancelIndex = customOptions.FindIndex(option => option.IsCancel);
            var selected = dialogs.ShowMessage(
                message,
                caption,
                labels,
                defaultIndex < 0 ? 0 : defaultIndex,
                cancelIndex);
            var selectedIndex = labels.FindIndex(label => string.Equals(label, selected, StringComparison.Ordinal));
            return selectedIndex >= 0 ? customOptions[selectedIndex] : customOptions[0];
        }

        var available = GetMessageBoxResults(arguments);
        var selectedResult = dialogs.ShowMessage(
            message,
            caption,
            available,
            0,
            available.FindIndex(option => option == "Cancel"));
        if (method.ReturnType.IsEnum)
        {
            return Enum.Parse(method.ReturnType, selectedResult);
        }

        return null;
    }

    private static List<string> GetMessageBoxResults(object[] arguments)
    {
        var button = arguments.FirstOrDefault(argument =>
            argument?.GetType().FullName == "System.Windows.MessageBoxButton");
        return button?.ToString() switch
        {
            "OKCancel" => new List<string> { "OK", "Cancel" },
            "YesNo" => new List<string> { "Yes", "No" },
            "YesNoCancel" => new List<string> { "Yes", "No", "Cancel" },
            _ => new List<string> { "OK" }
        };
    }

    private static string StringAt(IReadOnlyList<object> arguments, int index) =>
        index >= 0 && index < arguments.Count ? arguments[index] as string : null;

    private static double DoubleAt(IReadOnlyList<object> arguments, int index, double fallback) =>
        index >= 0 && index < arguments.Count && arguments[index] is double value ? value : fallback;
}
