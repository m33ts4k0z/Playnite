using System.Reflection;
using Avalonia.Markup.Xaml;

namespace Playnite.Avalonia.Theming;

public sealed class LooseXamlLoadException : Exception
{
    public string SourcePath { get; }

    public LooseXamlLoadException(string sourcePath, Exception innerException)
        : base($"Failed to load loose Avalonia XAML from '{sourcePath}'.", innerException)
    {
        SourcePath = sourcePath;
    }
}

/// <summary>
/// Loads XAML from disk at runtime. Theme and localization files are content,
/// never compiled Avalonia resources, matching Playnite's existing theme model.
/// </summary>
public sealed class LooseXamlLoader
{
    private readonly Assembly localAssembly;

    public LooseXamlLoader(Assembly localAssembly = null)
    {
        this.localAssembly = localAssembly ?? typeof(LooseXamlLoader).Assembly;
    }

    public T LoadFile<T>(string path) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);

        try
        {
            var xaml = File.ReadAllText(fullPath);
            return LoadString<T>(xaml);
        }
        catch (LooseXamlLoadException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new LooseXamlLoadException(fullPath, exception);
        }
    }

    public T LoadString<T>(string xaml) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xaml);
        return (T)AvaloniaRuntimeXamlLoader.Parse<T>(xaml, localAssembly);
    }
}
