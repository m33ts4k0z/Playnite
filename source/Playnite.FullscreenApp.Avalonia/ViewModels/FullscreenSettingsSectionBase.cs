using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public abstract class FullscreenSettingsSectionBase : IFullscreenSettingsSection, INotifyPropertyChanged
{
    public abstract string Key { get; }
    public abstract string Title { get; }
    public abstract global::Avalonia.Controls.Control Content { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    public abstract void Open();
    public abstract FullscreenSettingsSectionSaveResult Save();
    public abstract FullscreenSettingsSectionSelfCheckResult SelfCheck();

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
