using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Playnite.DesktopApp.Avalonia.ViewModels;

public abstract class SettingsSectionBase : ISettingsSection, INotifyPropertyChanged
{
    public abstract string Key { get; }
    public abstract string Title { get; }
    public abstract global::Avalonia.Controls.Control Content { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    public abstract void Open();
    public abstract SettingsSectionSaveResult Save();
    public abstract SettingsSectionSelfCheckResult SelfCheck();

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
