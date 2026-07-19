using Avalonia;
using Avalonia.Data.Converters;
using Playnite.Plugins;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using LegacyValueConverter = System.Windows.Data.IValueConverter;

namespace Playnite.WpfPluginSupport;

public static class WpfPluginSupportRuntime
{
    public static IValueConverter ResolveConverter(
        ExtensionFactory extensions,
        string pluginSource,
        string converterName)
    {
        if (extensions == null || string.IsNullOrWhiteSpace(pluginSource) ||
            string.IsNullOrWhiteSpace(converterName))
        {
            return null;
        }

        var support = extensions.ConvertersSupportList.FirstOrDefault(item =>
            string.Equals(item.SourceName, pluginSource, StringComparison.OrdinalIgnoreCase));
        var converter = support?.Converters?.FirstOrDefault(item =>
            string.Equals(item.GetType().Name, converterName, StringComparison.Ordinal));
        return converter == null ? null : new LegacyValueConverterAdapter(converter);
    }

    public static void EnsureApplication()
    {
        if (System.Windows.Application.Current == null)
        {
            // System.Windows.Application registers itself as the process-wide
            // Current on construction, so no reference needs to be held.
            _ = new System.Windows.Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }

        AddFallbackResource("True", true);
        AddFallbackResource("False", false);
        AddFallbackResource("FontIcoFont", new FontFamily("Segoe MDL2 Assets"));
        AddFallbackResource("BaseTextBlockStyle", new Style(typeof(TextBlock)));
        AddFallbackResource("NormalBrushDark", Brushes.DimGray);
        AddFallbackResource("WarningBrush", Brushes.OrangeRed);
    }

    public static void LoadPluginResources(string extensionDirectory)
    {
        EnsureApplication();
        if (string.IsNullOrWhiteSpace(extensionDirectory))
        {
            return;
        }

        var localizationDirectory = Path.Combine(extensionDirectory, "Localization");
        if (!Directory.Exists(localizationDirectory))
        {
            return;
        }

        var cultureName = CultureInfo.CurrentUICulture.Name.Replace('-', '_');
        var localizationPath = Path.Combine(localizationDirectory, cultureName + ".xaml");
        if (!File.Exists(localizationPath))
        {
            localizationPath = Path.Combine(localizationDirectory, "en_US.xaml");
        }

        if (!File.Exists(localizationPath))
        {
            return;
        }

        using var localizationStream = File.OpenRead(localizationPath);
        if (XamlReader.Load(localizationStream) is not ResourceDictionary resources)
        {
            throw new InvalidDataException($"Plugin localization is not a ResourceDictionary: {localizationPath}");
        }

        System.Windows.Application.Current.Resources.MergedDictionaries.Add(resources);
    }

    private static void AddFallbackResource(string key, object value)
    {
        if (!System.Windows.Application.Current.Resources.Contains(key))
        {
            System.Windows.Application.Current.Resources[key] = value;
        }
    }

    public static void Shutdown()
    {
        // Intentionally does not shut down the WPF Application. WPF forbids
        // creating a second System.Windows.Application per AppDomain even after
        // Shutdown(), so tearing it down here would break the next runtime host
        // (e.g. a Desktop/Fullscreen switch in the same process). The hidden
        // helper Application is a process-lifetime singleton and dies with it.
    }

    private sealed class LegacyValueConverterAdapter : IValueConverter
    {
        private readonly LegacyValueConverter converter;

        public LegacyValueConverterAdapter(LegacyValueConverter converter)
        {
            this.converter = converter;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            Normalize(converter.Convert(value, targetType, parameter, culture));

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Normalize(converter.ConvertBack(value, targetType, parameter, culture));

        private static object Normalize(object value) =>
            ReferenceEquals(value, DependencyProperty.UnsetValue) ? AvaloniaProperty.UnsetValue : value;
    }
}
