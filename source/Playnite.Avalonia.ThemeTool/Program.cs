using Playnite.Avalonia.Theming;

namespace Playnite.Avalonia.ThemeTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "new" => Create(args),
                "validate" => Validate(args),
                "pack" => Pack(args),
                _ => throw new ArgumentException($"Unknown command '{args[0]}'.")
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static int Create(string[] args)
    {
        if (args.Length != 4)
        {
            throw new ArgumentException("Usage: new <desktop|fullscreen> <name> <output-directory>");
        }

        var mode = ParseMode(args[1]);
        var path = AvaloniaThemeTool.Create(mode, args[2], args[3]);
        Console.WriteLine($"Created {mode} theme API {AvaloniaThemePackage.CurrentApiVersion}: {path}");
        return 0;
    }

    private static int Validate(string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            throw new ArgumentException("Usage: validate <theme-directory|theme.yaml> [desktop|fullscreen]");
        }

        AvaloniaThemeMode? expectedMode = args.Length == 3 ? ParseMode(args[2]) : null;
        var package = AvaloniaThemeTool.Validate(args[1], expectedMode);
        Console.WriteLine(
            $"Valid {package.Mode} theme '{package.Name}', API {package.Manifest?.ThemeApiVersion ?? "raw"}.");
        return 0;
    }

    private static int Pack(string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            throw new ArgumentException(
                "Usage: pack <theme-directory|theme.yaml> <destination-directory> [desktop|fullscreen]");
        }

        AvaloniaThemeMode? expectedMode = args.Length == 4 ? ParseMode(args[3]) : null;
        Console.WriteLine(AvaloniaThemeTool.Pack(args[1], args[2], expectedMode));
        return 0;
    }

    private static AvaloniaThemeMode ParseMode(string value)
    {
        if (Enum.TryParse<AvaloniaThemeMode>(value, true, out var mode))
        {
            return mode;
        }

        throw new ArgumentException($"Theme mode '{value}' must be Desktop or Fullscreen.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Playnite Avalonia theme API 3 tool");
        Console.WriteLine("  new <desktop|fullscreen> <name> <output-directory>");
        Console.WriteLine("  validate <theme-directory|theme.yaml> [desktop|fullscreen]");
        Console.WriteLine("  pack <theme-directory|theme.yaml> <destination-directory> [desktop|fullscreen]");
    }
}
