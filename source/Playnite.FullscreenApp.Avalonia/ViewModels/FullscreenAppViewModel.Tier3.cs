using System.Windows.Input;
using Playnite.Common;

namespace Playnite.FullscreenApp.Avalonia.ViewModels;

public sealed partial class FullscreenAppViewModel
{
    public event EventHandler RestartApplicationRequested;

    private string focusedSettingsDescription = string.Empty;

    public bool HasNotifications => NotificationCount > 0;
    public string NotificationBadgeText => NotificationCount > 99 ? "99+" : NotificationCount.ToString();
    public bool HasUpdateNotification => Notifications.Any(notification =>
        notification.Id.Contains("update", StringComparison.OrdinalIgnoreCase));
    public bool IsLibraryEmpty => Games.Count == 0;
    public string ConfirmPromptImagePath => PromptImagePath(
        settings.ButtonPrompts == Services.FullscreenButtonPrompts.PlayStation ? "ps-cross.svg" : "xbox-a.svg");
    public string ActionPromptImagePath => PromptImagePath(
        settings.ButtonPrompts == Services.FullscreenButtonPrompts.PlayStation ? "ps-square.svg" : "xbox-x.svg");
    public string MenuPromptImagePath => PromptImagePath(
        settings.ButtonPrompts == Services.FullscreenButtonPrompts.PlayStation ? "ps-options.svg" : "xbox-menu.svg");
    public string DetailsPromptImagePath => settings.SwapStartDetailsAction
        ? ActionPromptImagePath
        : ConfirmPromptImagePath;
    public string PlayPromptImagePath => settings.SwapStartDetailsAction
        ? ConfirmPromptImagePath
        : ActionPromptImagePath;
    public string FocusedSettingsDescription
    {
        get => focusedSettingsDescription;
        private set
        {
            if (SetField(ref focusedSettingsDescription, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(HasFocusedSettingsDescription));
            }
        }
    }
    public bool HasFocusedSettingsDescription => !string.IsNullOrWhiteSpace(FocusedSettingsDescription);

    public ICommand OpenHelpCommand { get; private set; }
    public ICommand ClearNotificationsCommand { get; private set; }
    public ICommand OpenUpdatesCommand { get; private set; }
    public ICommand RestartApplicationCommand { get; private set; }

    private void InitializeTier3Commands()
    {
        OpenHelpCommand = new RelayCommand(() => ProcessStarter.StartUrl(global::Playnite.UrlConstants.Wiki));
        ClearNotificationsCommand = new RelayCommand(
            () => runtimeHost?.Notifications.RemoveAll(),
            () => HasNotifications);
        OpenUpdatesCommand = new RelayCommand(() =>
        {
            CloseOverlays();
            IsNotificationsVisible = true;
            StatusText = HasUpdateNotification
                ? "Available updates are listed in notifications."
                : "No update notification is currently available.";
        });
        RestartApplicationCommand = new RelayCommand(() =>
            RestartApplicationRequested?.Invoke(this, EventArgs.Empty));
    }

    private static string PromptImagePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "Prompts", name);

    internal void SetFocusedSettingsDescription(string description) =>
        FocusedSettingsDescription = description;

    private void ShowSettingsRestartPrompt()
    {
        if (!Settings.RestartRequired)
        {
            return;
        }

        OpenDialog(
            Localize("LOCRestartRequired", "Restart required"),
            Localize("LOCSettingsRestartPrompt", "Restart Playnite now to apply these settings?"),
            new[]
            {
                Localize("LOCRestartNow", "Restart now"),
                Localize("LOCLater", "Later")
            },
            0,
            1,
            result =>
            {
                if (result == Localize("LOCRestartNow", "Restart now"))
                {
                    RestartApplicationRequested?.Invoke(this, EventArgs.Empty);
                }
            });
    }

    private static string Localize(string key, string fallback)
    {
        var value = Playnite.SDK.ResourceProvider.GetString(key);
        return string.IsNullOrWhiteSpace(value) || value == key || value == $"<!{key}!>"
            ? fallback
            : value;
    }

    private static string LocalizeFormat(string key, string fallback, params object[] values) =>
        string.Format(Localize(key, fallback), values);

    private void RaiseTier3Properties()
    {
        OnPropertyChanged(nameof(HasNotifications));
        OnPropertyChanged(nameof(NotificationBadgeText));
        OnPropertyChanged(nameof(HasUpdateNotification));
        OnPropertyChanged(nameof(IsLibraryEmpty));
        OnPropertyChanged(nameof(ConfirmPromptImagePath));
        OnPropertyChanged(nameof(ActionPromptImagePath));
        OnPropertyChanged(nameof(MenuPromptImagePath));
        OnPropertyChanged(nameof(DetailsPromptImagePath));
        OnPropertyChanged(nameof(PlayPromptImagePath));
        (ClearNotificationsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
