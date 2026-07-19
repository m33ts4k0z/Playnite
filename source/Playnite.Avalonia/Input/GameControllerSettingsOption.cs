using System.ComponentModel;

namespace Playnite.Avalonia.Input;

public sealed class GameControllerSettingsOption : INotifyPropertyChanged
{
    private bool isEnabled;

    public event PropertyChangedEventHandler PropertyChanged;

    public string Id { get; }
    public string Name { get; }
    public bool IsEnabled
    {
        get => isEnabled;
        set
        {
            if (isEnabled == value)
            {
                return;
            }

            isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
        }
    }

    public GameControllerSettingsOption(string id, string name, bool isEnabled)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = string.IsNullOrWhiteSpace(name) ? "Controller" : name;
        this.isEnabled = isEnabled;
    }
}
