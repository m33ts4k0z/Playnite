using Playnite.SDK;
using Playnite.SDK.Plugins;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Playnite.WpfPluginSupport;

public sealed record PluginSettingsHostResult(
    bool Saved,
    bool ViewAvailable,
    bool Failed,
    string Message);

internal enum PluginSettingsAutomation
{
    None,
    Save,
    Cancel,
    VerifyFailureThenCancel
}

public sealed class WpfPluginSettingsHost
{
    internal PluginSettingsAutomation Automation { get; set; }

    public PluginSettingsHostResult Show(Plugin plugin, string displayName, IntPtr ownerHandle)
    {
        if (plugin == null)
        {
            return new PluginSettingsHostResult(false, false, true, "The plugin is unavailable.");
        }

        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            return new PluginSettingsHostResult(
                false,
                false,
                true,
                "Legacy plugin settings must be opened on the application's STA thread.");
        }

        ISettings settings = null;
        var editStarted = false;
        var editCompleted = false;
        try
        {
            WpfPluginSupportRuntime.EnsureApplication();
            settings = plugin.GetSettings(false);
            var settingsView = plugin.GetSettingsView(false);
            if (settings == null || settingsView == null)
            {
                return new PluginSettingsHostResult(
                    false,
                    false,
                    false,
                    $"{displayName} does not provide a legacy settings model and view.");
            }

            settingsView.DataContext = settings;
            settings.BeginEdit();
            editStarted = true;

            var errorText = new TextBlock
            {
                Foreground = Brushes.IndianRed,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            var saveButton = new Button
            {
                Content = "Save",
                MinWidth = 100,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 100,
                IsCancel = true
            };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(saveButton);
            buttons.Children.Add(cancelButton);

            var footer = new StackPanel
            {
                Margin = new Thickness(18, 10, 18, 16)
            };
            footer.Children.Add(errorText);
            footer.Children.Add(buttons);
            DockPanel.SetDock(footer, Dock.Bottom);

            var root = new DockPanel();
            root.Children.Add(footer);
            root.Children.Add(new ScrollViewer
            {
                Content = settingsView,
                Margin = new Thickness(18, 18, 18, 0),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });

            var window = new Window
            {
                Title = $"{displayName} settings",
                Width = 820,
                Height = 720,
                MinWidth = 520,
                MinHeight = 420,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = root,
                ShowInTaskbar = false
            };
            if (ownerHandle != IntPtr.Zero)
            {
                new WindowInteropHelper(window).Owner = ownerHandle;
            }

            var saved = false;
            void CancelEdit()
            {
                if (editCompleted)
                {
                    return;
                }

                settings.CancelEdit();
                editCompleted = true;
            }

            void SaveAndClose()
            {
                if (!settings.VerifySettings(out var errors))
                {
                    var messages = errors?.Where(error => !string.IsNullOrWhiteSpace(error)).ToList() ??
                        new List<string>();
                    errorText.Text = messages.Count == 0
                        ? "The plugin rejected its current settings."
                        : string.Join(Environment.NewLine, messages);
                    errorText.Visibility = Visibility.Visible;
                    return;
                }

                settings.EndEdit();
                editCompleted = true;
                saved = true;
                window.DialogResult = true;
            }

            saveButton.Click += (_, _) => SaveAndClose();
            cancelButton.Click += (_, _) =>
            {
                CancelEdit();
                window.DialogResult = false;
            };
            window.Closing += (_, _) => CancelEdit();

            var automation = Automation;
            Automation = PluginSettingsAutomation.None;
            if (automation != PluginSettingsAutomation.None)
            {
                window.ContentRendered += (_, _) => window.Dispatcher.BeginInvoke(() =>
                {
                    switch (automation)
                    {
                        case PluginSettingsAutomation.Save:
                            SaveAndClose();
                            break;
                        case PluginSettingsAutomation.Cancel:
                            CancelEdit();
                            window.DialogResult = false;
                            break;
                        case PluginSettingsAutomation.VerifyFailureThenCancel:
                            SaveAndClose();
                            window.Dispatcher.BeginInvoke(() =>
                            {
                                if (window.IsVisible)
                                {
                                    CancelEdit();
                                    window.DialogResult = false;
                                }
                            }, DispatcherPriority.Background);
                            break;
                    }
                }, DispatcherPriority.Background);
            }

            window.ShowDialog();
            return new PluginSettingsHostResult(
                saved,
                true,
                false,
                saved ? $"Saved settings for {displayName}." : $"Settings for {displayName} were cancelled.");
        }
        catch (Exception exception)
        {
            if (editStarted && !editCompleted)
            {
                try
                {
                    settings.CancelEdit();
                }
                catch (Exception cancelException)
                {
                    return new PluginSettingsHostResult(
                        false,
                        false,
                        true,
                        $"Plugin settings failed: {exception.Message} Cancel also failed: {cancelException.Message}");
                }
            }

            return new PluginSettingsHostResult(
                false,
                false,
                true,
                $"Plugin settings failed: {exception.Message}");
        }
    }
}
