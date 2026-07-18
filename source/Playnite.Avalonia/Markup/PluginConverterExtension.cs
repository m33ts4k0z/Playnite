using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using System.Globalization;

namespace Playnite.Avalonia.Markup;

public static class PluginConverterRuntime
{
    public static Func<string, string, IValueConverter> Resolver { get; set; } = (_, _) => null;
}

public sealed class PluginConverterExtension : MarkupExtension
{
    public string Plugin { get; set; }
    public string Converter { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new PluginConverterProvider(Plugin, Converter);
}

public sealed class PluginConverterProvider : IValueConverter
{
    private readonly string pluginSource;
    private readonly string converterName;
    private IValueConverter converter;

    public PluginConverterProvider(string pluginSource, string converterName)
    {
        this.pluginSource = pluginSource;
        this.converterName = converterName;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Resolve()?.Convert(value, targetType, parameter, culture) ?? AvaloniaProperty.UnsetValue;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Resolve()?.ConvertBack(value, targetType, parameter, culture) ?? AvaloniaProperty.UnsetValue;

    private IValueConverter Resolve() =>
        converter ??= PluginConverterRuntime.Resolver(pluginSource, converterName);
}
