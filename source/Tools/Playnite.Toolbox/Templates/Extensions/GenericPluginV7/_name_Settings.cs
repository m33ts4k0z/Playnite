using Playnite.SDK;
using Playnite.SDK.Data;

namespace _namespace_;

public sealed class _name_Settings : ObservableObject
{
    private string option1 = string.Empty;
    private bool option2;

    public string Option1
    {
        get => option1;
        set => SetValue(ref option1, value);
    }

    public bool Option2
    {
        get => option2;
        set => SetValue(ref option2, value);
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
