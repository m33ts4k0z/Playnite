using Playnite.SDK;
using Playnite.SDK.Plugins;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Playnite.WpfPluginSupport;

internal sealed class WpfPluginSettingsContract : ISettings, INotifyPropertyChanged
{
    private string value = "Pilot setting";

    public event PropertyChangedEventHandler PropertyChanged;
    public int BeginCount { get; private set; }
    public int EndCount { get; private set; }
    public int CancelCount { get; private set; }
    public int VerifyCount { get; private set; }
    public bool IsValid { get; set; } = true;
    public string Value
    {
        get => value;
        set
        {
            if (this.value == value)
            {
                return;
            }

            this.value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
        }
    }

    public void BeginEdit() => BeginCount++;
    public void EndEdit() => EndCount++;
    public void CancelEdit() => CancelCount++;

    public bool VerifySettings(out List<string> errors)
    {
        VerifyCount++;
        errors = IsValid ? null : new List<string> { "Pilot settings validation failed." };
        return IsValid;
    }
}

internal sealed class WpfPluginSettingsContractPlugin : GenericPlugin
{
    internal static readonly Guid PluginId = Guid.Parse("af5a11aa-84ea-4f2b-a9bb-89b7d25cf388");
    internal const string DisplayName = "Pilot legacy UI plugin";
    internal const string SourceName = "PilotLegacyUi";
    internal const string ElementName = "PilotStatus";

    public override Guid Id => PluginId;
    internal WpfPluginSettingsContract Settings { get; } = new();
    internal int ViewCreationCount { get; private set; }

    public WpfPluginSettingsContractPlugin(IPlayniteAPI playniteApi) : base(playniteApi)
    {
        Properties = new GenericPluginProperties { HasSettings = true };
        AddSettingsSupport(new AddSettingsSupportArgs
        {
            SourceName = SourceName,
            SettingsRoot = "PilotSettings"
        });
        AddCustomElementSupport(new AddCustomElementSupportArgs
        {
            SourceName = SourceName,
            ElementList = new List<string> { ElementName }
        });
        AddConvertersSupport(new AddConvertersSupportArgs
        {
            SourceName = SourceName,
            Converters = new List<IValueConverter> { new WpfPluginSettingsContractConverter() }
        });
    }

    public override ISettings GetSettings(bool firstRunSettings) => Settings;

    public override UserControl GetSettingsView(bool firstRunView)
    {
        ViewCreationCount++;
        var textBox = new TextBox { MinWidth = 320 };
        textBox.SetBinding(TextBox.TextProperty, new Binding(nameof(WpfPluginSettingsContract.Value))
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        return new UserControl { Content = textBox };
    }

    public override Control GetGameViewControl(GetGameViewControlArgs args) =>
        args.Name == ElementName ? new Label { Content = "Pilot custom element" } : null;
}

internal sealed class WpfPluginSettingsContractConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        $"legacy:{value}";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString()?.Replace("legacy:", string.Empty, StringComparison.Ordinal);
}
