using Playnite.SDK;
using Playnite.SDK.Data;

namespace _namespace_;

public sealed class _name_Settings : ObservableObject
{
    private string accountName = string.Empty;

    public string AccountName
    {
        get => accountName;
        set => SetValue(ref accountName, value);
    }
}

public sealed class _name_SettingsViewModel : ObservableObject, ISettings
{
    private readonly _name_ plugin;
    private _name_Settings? editingClone;
    private _name_Settings settings;

    public _name_Settings Settings
    {
        get => settings;
        private set => SetValue(ref settings, value);
    }

    public _name_SettingsViewModel(_name_ plugin)
    {
        this.plugin = plugin;
        settings = plugin.LoadPluginSettings<_name_Settings>() ?? new _name_Settings();
    }

    public void BeginEdit() => editingClone = Serialization.GetClone(Settings);

    public void CancelEdit()
    {
        if (editingClone != null)
        {
            Settings = editingClone;
        }
    }

    public void EndEdit() => plugin.SavePluginSettings(Settings);

    public bool VerifySettings(out List<string> errors)
    {
        errors = [];
        return true;
    }
}
