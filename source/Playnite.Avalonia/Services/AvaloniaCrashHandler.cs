using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Diagnostics;

namespace Playnite.Avalonia.Services;

public sealed class AvaloniaCrashAttribution
{
    public bool IsAddonRelated { get; init; }
    public string PluginId { get; init; }
    public string PluginName { get; init; }
}

public sealed class AvaloniaCrashHandlerOptions
{
    public Func<Window> CurrentWindow { get; init; } = () => null;
    public string ExecutablePath { get; init; }
    public IReadOnlyList<string> RestartArguments { get; init; } = Array.Empty<string>();
    public Func<Exception, AvaloniaCrashAttribution> AttributeException { get; init; } =
        _ => new AvaloniaCrashAttribution();
    public Action<Exception, string> LogException { get; init; } = (_, _) => { };
    public Action<string> DisablePlugin { get; init; } = _ => { };
    public Action<string> SaveLogPackage { get; init; } = _ => { };
    public Action<string, string> SaveDiagnosticPackage { get; init; } = (_, _) => { };
    public Action ReportIssue { get; init; } = () => { };
    public Action RequestShutdown { get; init; } = () => { };
}

/// <summary>
/// Process-wide exception bridge and independent crash window. It deliberately
/// has no dependency on either shell's visual tree so it can still be shown
/// when the main window or its theme caused the failure.
/// </summary>
public static class AvaloniaCrashHandler
{
    private static readonly object optionsLock = new();
    private static AvaloniaCrashHandlerOptions options = new();
    private static int processHooksAttached;
    private static int dispatcherHookAttached;
    private static int handlingException;

    public static void AttachProcessHandlers()
    {
        if (Interlocked.Exchange(ref processHooksAttached, 1) != 0)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Present(exception, "Unhandled application exception", true);
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            args.SetObserved();
            if (IsExpectedLinuxTrayCancellation(args.Exception))
            {
                Trace.WriteLine(
                    $"Linux tray watcher stopped during shutdown: {args.Exception.Message}");
                return;
            }

            Present(args.Exception, "Unobserved background-task exception", false);
        };
    }

    public static void AttachDispatcherHandler()
    {
        if (Interlocked.Exchange(ref dispatcherHookAttached, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            args.Handled = true;
            if (IsExpectedLinuxTrayCancellation(args.Exception))
            {
                Trace.WriteLine($"Linux tray watcher stopped during shutdown: {args.Exception.Message}");
                return;
            }

            Present(args.Exception, "Unhandled user-interface exception", false);
        };
    }

    public static void Configure(AvaloniaCrashHandlerOptions newOptions)
    {
        ArgumentNullException.ThrowIfNull(newOptions);
        lock (optionsLock)
        {
            options = newOptions;
        }
    }

    public static Window CreateWindowForTest(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return new AvaloniaCrashWindow(
            exception,
            "Crash-window construction test",
            GetOptions(),
            new AvaloniaCrashAttribution());
    }

    private static void Present(Exception exception, string source, bool terminating)
    {
        if (exception == null || Interlocked.CompareExchange(ref handlingException, 1, 0) != 0)
        {
            return;
        }

        try
        {
            Trace.WriteLine($"{source}: {exception}");
            var snapshot = GetOptions();
            try
            {
                snapshot.LogException(exception, source);
            }
            catch (Exception loggingException)
            {
                Trace.WriteLine($"Crash logging failed: {loggingException}");
            }
            var attribution = ResolveAttribution(snapshot, exception);
            if (Application.Current == null)
            {
                return;
            }
            void Show()
            {
                if (Application.Current == null)
                {
                    return;
                }

                var window = new AvaloniaCrashWindow(exception, source, snapshot, attribution);
                var owner = SafeCurrentWindow(snapshot);
                var frame = new DispatcherFrame();
                window.Closed += (_, _) => frame.Continue = false;
                if (owner != null && owner != window)
                {
                    window.ShowInTaskbar = false;
                    _ = window.ShowDialog(owner);
                }
                else
                {
                    window.Show();
                }
                Dispatcher.UIThread.PushFrame(frame);
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Show();
            }
            else
            {
                Dispatcher.UIThread.Invoke(Show);
            }
        }
        catch (Exception handlerException)
        {
            Trace.WriteLine($"Crash handler failed: {handlerException}");
        }
        finally
        {
            if (!terminating)
            {
                Interlocked.Exchange(ref handlingException, 0);
            }
        }
    }

    private static bool IsExpectedLinuxTrayCancellation(Exception exception)
    {
        if (exception is AggregateException aggregate)
        {
            var innerExceptions = aggregate.Flatten().InnerExceptions;
            return innerExceptions.Count > 0 &&
                innerExceptions.All(IsExpectedLinuxTrayCancellation);
        }

        if (!OperatingSystem.IsLinux() || exception is not TaskCanceledException)
        {
            return false;
        }

        var stackTrace = exception.StackTrace;
        return stackTrace?.Contains(
                   "Tmds.DBus.Protocol.InnerConnection.Watcher.WaitForOwnerAsync",
                   StringComparison.Ordinal) == true &&
               stackTrace.Contains(
                   "Avalonia.FreeDesktop.DBusTrayIconImpl.WatchAsync",
                   StringComparison.Ordinal);
    }

    private static AvaloniaCrashHandlerOptions GetOptions()
    {
        lock (optionsLock)
        {
            return options;
        }
    }

    private static AvaloniaCrashAttribution ResolveAttribution(
        AvaloniaCrashHandlerOptions snapshot,
        Exception exception)
    {
        try
        {
            return snapshot.AttributeException(exception) ?? new AvaloniaCrashAttribution();
        }
        catch (Exception attributionException)
        {
            Trace.WriteLine($"Crash attribution failed: {attributionException}");
            return new AvaloniaCrashAttribution();
        }
    }

    private static Window SafeCurrentWindow(AvaloniaCrashHandlerOptions snapshot)
    {
        try
        {
            return snapshot.CurrentWindow();
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class AvaloniaCrashWindow : Window
{
    private readonly AvaloniaCrashHandlerOptions options;
    private readonly AvaloniaCrashAttribution attribution;
    private readonly TextBox description;
    private readonly TextBlock status;
    private readonly CheckBox disablePlugin;

    public AvaloniaCrashWindow(
        Exception exception,
        string source,
        AvaloniaCrashHandlerOptions options,
        AvaloniaCrashAttribution attribution)
    {
        this.options = options;
        this.attribution = attribution;
        Title = "Playnite encountered an error";
        Width = 760;
        Height = 680;
        MinWidth = 560;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var heading = new TextBlock
        {
            Text = "Playnite encountered an unexpected error.",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        };
        var origin = new TextBlock
        {
            Text = source,
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap
        };
        var exceptionText = new TextBox
        {
            Text = exception.ToString(),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            MinHeight = 150,
            FontFamily = new FontFamily("Consolas,DejaVu Sans Mono,monospace")
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(exceptionText, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(exceptionText, ScrollBarVisibility.Auto);
        description = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            PlaceholderText = "Describe what you were doing before the error occurred.",
            MinHeight = 90
        };
        disablePlugin = new CheckBox { IsVisible = false };
        if (!string.IsNullOrWhiteSpace(attribution.PluginId))
        {
            disablePlugin.Content =
                $"Disable {attribution.PluginName ?? attribution.PluginId} before restarting";
            disablePlugin.IsChecked = true;
            disablePlugin.IsVisible = true;
        }
        else if (attribution.IsAddonRelated)
        {
            disablePlugin.Content =
                "The error appears add-on or theme related, but the specific package could not be identified.";
            disablePlugin.IsEnabled = false;
            disablePlugin.IsVisible = true;
        }

        status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.IndianRed,
            MinHeight = 22
        };
        var saveLog = CreateButton("Save log", async () => await SavePackageAsync(false));
        var saveDiagnostics = CreateButton(
            "Create diagnostics package",
            async () => await SavePackageAsync(true));
        var report = CreateButton("Report issue", () =>
        {
            RunAction(options.ReportIssue, "The issue-report page could not be opened.");
            return Task.CompletedTask;
        });
        var restart = CreateButton("Restart", () =>
        {
            Restart(false);
            return Task.CompletedTask;
        });
        var restartSafe = CreateButton("Restart in safe mode", () =>
        {
            Restart(true);
            return Task.CompletedTask;
        });
        var close = CreateButton("Close", () =>
        {
            Close();
            return Task.CompletedTask;
        });
        close.IsCancel = true;

        var packageButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        packageButtons.Children.Add(saveLog);
        packageButtons.Children.Add(saveDiagnostics);
        packageButtons.Children.Add(report);
        var lifecycleButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        lifecycleButtons.Children.Add(restart);
        lifecycleButtons.Children.Add(restartSafe);
        lifecycleButtons.Children.Add(close);
        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                heading,
                origin,
                exceptionText,
                new TextBlock { Text = "What happened?", FontWeight = FontWeight.SemiBold },
                description,
                disablePlugin,
                status,
                packageButtons,
                lifecycleButtons
            }
        };
        Content = new ScrollViewer
        {
            Content = new Border
            {
                Padding = new Thickness(20),
                Child = content
            },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private Button CreateButton(string label, Func<Task> action)
    {
        var button = new Button { Content = label, MinWidth = 105 };
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                status.Text = exception.Message;
            }
            finally
            {
                button.IsEnabled = true;
            }
        };
        return button;
    }

    private async Task SavePackageAsync(bool diagnostics)
    {
        var fileType = new FilePickerFileType("ZIP archive") { Patterns = new[] { "*.zip" } };
        var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = diagnostics ? "Save diagnostics package" : "Save log package",
            DefaultExtension = "zip",
            FileTypeChoices = new[] { fileType },
            ShowOverwritePrompt = true,
            SuggestedFileName = diagnostics ? "playnite-diagnostics.zip" : "playnite-logs.zip"
        });
        var path = target?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        status.Foreground = Brushes.Gray;
        status.Text = diagnostics ? "Creating diagnostics package…" : "Creating log package…";
        await Task.Run(() =>
        {
            if (diagnostics)
            {
                options.SaveDiagnosticPackage(path, description.Text ?? string.Empty);
            }
            else
            {
                options.SaveLogPackage(path);
            }
        });
        status.Text = $"Saved {path}";
    }

    private void Restart(bool safeMode)
    {
        if (string.IsNullOrWhiteSpace(options.ExecutablePath))
        {
            status.Text = "The application executable could not be resolved.";
            return;
        }

        if (disablePlugin.IsChecked == true && !string.IsNullOrWhiteSpace(attribution.PluginId))
        {
            options.DisablePlugin(attribution.PluginId);
        }

        var start = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };
        foreach (var argument in options.RestartArguments ?? Array.Empty<string>())
        {
            start.ArgumentList.Add(argument);
        }
        if (safeMode && !start.ArgumentList.Contains("--safestartup", StringComparer.OrdinalIgnoreCase))
        {
            start.ArgumentList.Add("--safestartup");
        }

        Process.Start(start);
        Close();
        options.RequestShutdown();
    }

    private void RunAction(Action action, string failureMessage)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            status.Text = $"{failureMessage} {exception.Message}";
        }
    }
}
