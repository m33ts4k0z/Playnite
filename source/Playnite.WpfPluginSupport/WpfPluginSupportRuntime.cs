using Avalonia;
using Avalonia.Data.Converters;
using Playnite.Plugins;
using System.Globalization;
using System.Windows;
using LegacyValueConverter = System.Windows.Data.IValueConverter;

namespace Playnite.WpfPluginSupport;

public static class WpfPluginSupportRuntime
{
    private static System.Windows.Application ownedApplication;

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

    internal static void EnsureApplication()
    {
        if (System.Windows.Application.Current == null)
        {
            ownedApplication = new System.Windows.Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }
    }

    public static void Shutdown()
    {
        if (ownedApplication == null)
        {
            return;
        }

        ownedApplication.Shutdown();
        ownedApplication = null;
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
