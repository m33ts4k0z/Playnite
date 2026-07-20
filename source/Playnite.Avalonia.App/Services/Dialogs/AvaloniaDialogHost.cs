using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Playnite.Avalonia.Controls;
using Playnite.SDK;

namespace Playnite.Avalonia.App.Services;

/// <summary>
/// Shared native-Avalonia dialog primitives used by both application shells.
/// The public SDK v6 surface is synchronous, so modal windows pump a nested
/// Avalonia dispatcher frame while work and rendering remain responsive.
/// </summary>
public sealed class AvaloniaDialogHost
{
    private readonly Func<Window> currentWindow;
    private readonly Func<Window, IDisposable> redirectInput;

    public AvaloniaDialogHost(
        Func<Window> currentWindow,
        Func<Window, IDisposable> redirectInput = null)
    {
        this.currentWindow = currentWindow ?? throw new ArgumentNullException(nameof(currentWindow));
        this.redirectInput = redirectInput ?? (_ => null);
    }

    public StringSelectionDialogResult ShowInput(
        string message,
        string caption,
        string defaultInput,
        IReadOnlyList<MessageBoxToggle> toggleOptions = null) =>
        Invoke(() => ShowInputCore(message, caption, defaultInput, toggleOptions));

    public void ShowSelectableString(string message, string caption, string value) =>
        Invoke(() => ShowSelectableStringCore(message, caption, value));

    public GenericItemOption ChooseItemWithSearch(
        IReadOnlyList<GenericItemOption> items,
        Func<string, List<GenericItemOption>> searchFunction,
        string defaultSearch = null,
        string caption = null) => Invoke(() => ChooseItemWithSearchCore(
            items,
            searchFunction,
            defaultSearch,
            caption));

    public ImageFileOption ChooseImageFile(
        IReadOnlyList<ImageFileOption> files,
        string caption = null,
        double itemWidth = 240,
        double itemHeight = 180) => Invoke(() => ChooseImageFileCore(
            files,
            caption,
            itemWidth,
            itemHeight));

    public GlobalProgressResult ActivateGlobalProgress(
        Action<GlobalProgressActionArgs> progressAction,
        GlobalProgressOptions options)
    {
        ArgumentNullException.ThrowIfNull(progressAction);
        return ActivateGlobalProgress(args =>
        {
            progressAction(args);
            return Task.CompletedTask;
        }, options);
    }

    public GlobalProgressResult ActivateGlobalProgress(
        Func<GlobalProgressActionArgs, Task> progressAction,
        GlobalProgressOptions options)
    {
        ArgumentNullException.ThrowIfNull(progressAction);
        return Invoke(() => ActivateGlobalProgressCore(progressAction, options));
    }

    public AvaloniaSelectionResult<T> SelectSingle<T>(
        string caption,
        string message,
        IReadOnlyList<AvaloniaSelectionItem<T>> items) =>
        Invoke(() => SelectItemsCore(caption, message, items, false));

    public AvaloniaSelectionResult<T> SelectMultiple<T>(
        string caption,
        string message,
        IReadOnlyList<AvaloniaSelectionItem<T>> items) =>
        Invoke(() => SelectItemsCore(caption, message, items, true));

    private StringSelectionDialogResult ShowInputCore(
        string message,
        string caption,
        string defaultInput,
        IReadOnlyList<MessageBoxToggle> toggleOptions)
    {
        var input = new TextBox
        {
            Text = defaultInput ?? string.Empty,
            MinWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var content = CreateVerticalContent(message);
        content.Children.Add(input);

        var toggleBindings = new List<(MessageBoxToggle Model, CheckBox CheckBox)>();
        foreach (var option in toggleOptions ?? Array.Empty<MessageBoxToggle>())
        {
            if (option == null)
            {
                continue;
            }

            var checkBox = new CheckBox
            {
                Content = option.Title ?? string.Empty,
                IsChecked = option.Selected,
                Margin = new global::Avalonia.Thickness(0, 8, 0, 0)
            };
            toggleBindings.Add((option, checkBox));
            content.Children.Add(checkBox);
        }

        var confirmed = false;
        var window = CreateDialogWindow(caption, 560, 300);
        var buttons = CreateButtonRow(
            ("OK", true, false, () =>
            {
                confirmed = true;
                foreach (var binding in toggleBindings)
                {
                    binding.Model.Selected = binding.CheckBox.IsChecked == true;
                }
                window.Close();
            }),
            ("Cancel", false, true, window.Close));
        window.Content = CreateDialogRoot(content, buttons);
        window.Opened += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };
        ShowModal(window);
        return new StringSelectionDialogResult(confirmed, input.Text ?? string.Empty);
    }

    private void ShowSelectableStringCore(string message, string caption, string value)
    {
        var content = CreateVerticalContent(message);
        var input = new TextBox
        {
            Text = value ?? string.Empty,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 180,
            MinWidth = 520
        };
        ScrollViewer.SetVerticalScrollBarVisibility(input, ScrollBarVisibility.Auto);
        content.Children.Add(input);
        var window = CreateDialogWindow(caption, 680, 420);
        window.Content = CreateDialogRoot(
            content,
            CreateButtonRow(("OK", true, true, window.Close)));
        window.Opened += (_, _) => input.Focus();
        ShowModal(window);
    }

    private GenericItemOption ChooseItemWithSearchCore(
        IReadOnlyList<GenericItemOption> items,
        Func<string, List<GenericItemOption>> searchFunction,
        string defaultSearch,
        string caption)
    {
        ArgumentNullException.ThrowIfNull(searchFunction);
        var results = new ListBox
        {
            ItemsSource = (items ?? Array.Empty<GenericItemOption>()).Where(item => item != null).ToList(),
            SelectionMode = SelectionMode.Single,
            MinHeight = 300,
            ItemTemplate = CreateGenericItemTemplate()
        };
        var search = new TextBox
        {
            Text = defaultSearch ?? string.Empty,
            PlaceholderText = "Search",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var status = new TextBlock
        {
            Foreground = Brushes.IndianRed,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false
        };
        var searchButton = new Button { Content = "Search", MinWidth = 100 };
        var searchRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        searchRow.Children.Add(search);
        Grid.SetColumn(searchButton, 1);
        searchButton.Margin = new global::Avalonia.Thickness(8, 0, 0, 0);
        searchRow.Children.Add(searchButton);

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(results);
        content.Children.Add(searchRow);
        content.Children.Add(status);

        var window = CreateDialogWindow(caption ?? "Select item", 760, 620);
        GenericItemOption selected = null;
        void Confirm()
        {
            selected = results.SelectedItem as GenericItemOption;
            if (selected != null)
            {
                window.Close();
            }
        }

        async Task SearchAsync()
        {
            searchButton.IsEnabled = false;
            status.IsVisible = false;
            try
            {
                var term = search.Text ?? string.Empty;
                var found = await Task.Run(() => searchFunction(term));
                results.ItemsSource = found?.Where(item => item != null).ToList() ?? new List<GenericItemOption>();
                results.SelectedIndex = results.ItemCount > 0 ? 0 : -1;
            }
            catch (Exception exception)
            {
                status.Text = $"Search failed: {exception.Message}";
                status.IsVisible = true;
            }
            finally
            {
                searchButton.IsEnabled = true;
            }
        }

        searchButton.Click += async (_, _) => await SearchAsync();
        search.KeyDown += async (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                args.Handled = true;
                await SearchAsync();
            }
        };
        results.DoubleTapped += (_, _) => Confirm();
        window.Content = CreateDialogRoot(
            content,
            CreateButtonRow(
                ("Select", true, false, Confirm),
                ("Cancel", false, true, window.Close)));
        window.Opened += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(defaultSearch))
            {
                await SearchAsync();
            }
            search.Focus();
        };
        ShowModal(window);
        return selected;
    }

    private ImageFileOption ChooseImageFileCore(
        IReadOnlyList<ImageFileOption> files,
        string caption,
        double itemWidth,
        double itemHeight)
    {
        var images = (files ?? Array.Empty<ImageFileOption>()).Where(file => file != null).ToList();
        var list = new ListBox
        {
            ItemsSource = images,
            SelectionMode = SelectionMode.Single,
            ItemsPanel = new FuncTemplate<Panel>(() => new WrapPanel()),
            ItemTemplate = new FuncDataTemplate<ImageFileOption>((item, _) =>
            {
                var panel = new StackPanel
                {
                    Width = Math.Max(80, itemWidth),
                    Margin = new global::Avalonia.Thickness(6),
                    Spacing = 4
                };
                panel.Children.Add(new GameCoverImage
                {
                    SourcePath = item?.Path,
                    Width = Math.Max(80, itemWidth),
                    Height = Math.Max(60, itemHeight),
                    Stretch = Stretch.Uniform
                });
                if (!string.IsNullOrWhiteSpace(item?.Description))
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = item.Description,
                        TextWrapping = TextWrapping.Wrap,
                        MaxLines = 2
                    });
                }
                return panel;
            }),
            MinHeight = 360
        };
        var window = CreateDialogWindow(caption ?? "Select image", 900, 680);
        ImageFileOption selected = null;
        void Confirm()
        {
            selected = list.SelectedItem as ImageFileOption;
            if (selected != null)
            {
                window.Close();
            }
        }

        list.DoubleTapped += (_, _) => Confirm();
        window.Content = CreateDialogRoot(
            list,
            CreateButtonRow(
                ("Select", true, false, Confirm),
                ("Cancel", false, true, window.Close)));
        window.Opened += (_, _) =>
        {
            if (images.Count > 0)
            {
                list.SelectedIndex = 0;
            }
            list.Focus();
        };
        ShowModal(window);
        return selected;
    }

    private GlobalProgressResult ActivateGlobalProgressCore(
        Func<GlobalProgressActionArgs, Task> progressAction,
        GlobalProgressOptions options)
    {
        options ??= new GlobalProgressOptions(string.Empty);
        using var cancellation = new CancellationTokenSource();
        var progressArgs = new GlobalProgressActionArgs(
            SynchronizationContext.Current,
            null,
            cancellation.Token)
        {
            Text = options.Text ?? string.Empty,
            IsIndeterminate = options.IsIndeterminate
        };
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var progress = new ProgressBar
        {
            MinWidth = 440,
            MinHeight = 14,
            IsIndeterminate = options.IsIndeterminate
        };
        var cancel = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            IsVisible = options.Cancelable,
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 100
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(text);
        content.Children.Add(progress);
        content.Children.Add(cancel);
        var window = CreateDialogWindow("Playnite", 560, 230);
        window.CanResize = false;
        window.Content = new Border
        {
            Padding = new global::Avalonia.Thickness(20),
            Child = content
        };

        var wasCanceled = false;
        Exception failure = null;
        bool? result = null;
        var completed = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            text.Text = progressArgs.Text ?? string.Empty;
            progress.IsIndeterminate = progressArgs.IsIndeterminate;
            if (!progressArgs.IsIndeterminate)
            {
                progress.Maximum = progressArgs.ProgressMaxValue > 0 ? progressArgs.ProgressMaxValue : 100;
                progress.Value = Math.Clamp(progressArgs.CurrentProgressValue, progress.Minimum, progress.Maximum);
            }
            cancel.IsEnabled = options.Cancelable && !cancellation.IsCancellationRequested && !completed;
        };
        cancel.Click += (_, _) =>
        {
            if (!options.Cancelable || cancellation.IsCancellationRequested)
            {
                return;
            }

            wasCanceled = true;
            cancellation.Cancel();
            cancel.IsEnabled = false;
        };
        window.Closing += (_, args) =>
        {
            if (!completed)
            {
                args.Cancel = true;
                if (options.Cancelable && !cancellation.IsCancellationRequested)
                {
                    wasCanceled = true;
                    cancellation.Cancel();
                }
            }
        };
        window.Opened += (_, _) =>
        {
            timer.Start();
            _ = Task.Run(async () =>
            {
                try
                {
                    await progressAction(progressArgs).ConfigureAwait(false);
                    result = true;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    result = false;
                }
                finally
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        completed = true;
                        timer.Stop();
                        window.Close();
                    });
                }
            });
        };
        ShowModal(window);
        return new GlobalProgressResult(result, wasCanceled, failure);
    }

    private AvaloniaSelectionResult<T> SelectItemsCore<T>(
        string caption,
        string message,
        IReadOnlyList<AvaloniaSelectionItem<T>> items,
        bool multiple)
    {
        var materialized = (items ?? Array.Empty<AvaloniaSelectionItem<T>>())
            .Where(item => item != null)
            .ToList();
        var list = new ListBox
        {
            ItemsSource = materialized,
            SelectionMode = multiple ? SelectionMode.Multiple : SelectionMode.Single,
            MinHeight = 300,
            ItemTemplate = new FuncDataTemplate<AvaloniaSelectionItem<T>>((item, _) =>
                CreateItemContent(item?.Name, item?.Description))
        };
        var content = CreateVerticalContent(message);
        content.Children.Add(list);
        var window = CreateDialogWindow(caption ?? "Select item", 680, 560);
        var confirmed = false;
        void Confirm()
        {
            if (multiple || list.SelectedItem != null)
            {
                confirmed = true;
                window.Close();
            }
        }
        list.DoubleTapped += (_, _) => Confirm();
        window.Content = CreateDialogRoot(
            content,
            CreateButtonRow(
                ("Select", true, false, Confirm),
                ("Cancel", false, true, window.Close)));
        window.Opened += (_, _) =>
        {
            foreach (var item in materialized.Where(item => item.Selected))
            {
                list.SelectedItems?.Add(item);
                if (!multiple)
                {
                    break;
                }
            }
            if (!multiple && list.SelectedItem == null && materialized.Count > 0)
            {
                list.SelectedIndex = 0;
            }
            list.Focus();
        };
        ShowModal(window);
        if (!confirmed)
        {
            return new AvaloniaSelectionResult<T>(false, Array.Empty<T>());
        }

        var selected = multiple
            ? list.SelectedItems?.OfType<AvaloniaSelectionItem<T>>().Select(item => item.Value).ToList()
                ?? new List<T>()
            : list.SelectedItem is AvaloniaSelectionItem<T> item
                ? new List<T> { item.Value }
                : new List<T>();
        return new AvaloniaSelectionResult<T>(true, selected);
    }

    private void ShowModal(Window window)
    {
        var owner = currentWindow();
        var frame = new DispatcherFrame();
        window.Closed += (_, _) => frame.Continue = false;
        using (redirectInput(window))
        {
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
    }

    private static Window CreateDialogWindow(string caption, double width, double height) => new()
    {
        Title = string.IsNullOrWhiteSpace(caption) ? "Playnite" : caption,
        Width = width,
        Height = height,
        MinWidth = Math.Min(420, width),
        MinHeight = Math.Min(220, height),
        CanResize = true,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
    };

    private static StackPanel CreateVerticalContent(string message)
    {
        var panel = new StackPanel { Spacing = 12 };
        if (!string.IsNullOrWhiteSpace(message))
        {
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap
            });
        }
        return panel;
    }

    private static Control CreateDialogRoot(Control content, Control buttons)
    {
        var grid = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        var body = new Border
        {
            Padding = new global::Avalonia.Thickness(18),
            Child = content
        };
        grid.Children.Add(body);
        Grid.SetRow(buttons, 1);
        grid.Children.Add(buttons);
        return grid;
    }

    private static Control CreateButtonRow(
        params (string Label, bool IsDefault, bool IsCancel, Action Action)[] definitions)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new global::Avalonia.Thickness(18, 8, 18, 18)
        };
        foreach (var definition in definitions)
        {
            var button = new Button
            {
                Content = definition.Label,
                IsDefault = definition.IsDefault,
                IsCancel = definition.IsCancel,
                MinWidth = 100
            };
            button.Click += (_, _) => definition.Action();
            row.Children.Add(button);
        }
        return row;
    }

    private static IDataTemplate CreateGenericItemTemplate() =>
        new FuncDataTemplate<GenericItemOption>((item, _) =>
            CreateItemContent(item?.Name, item?.Description));

    private static Control CreateItemContent(string name, string description)
    {
        var panel = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(6),
            Spacing = 3
        };
        panel.Children.Add(new TextBlock
        {
            Text = name ?? string.Empty,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(description))
        {
            panel.Children.Add(new TextBlock
            {
                Text = description,
                FontStyle = FontStyle.Italic,
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap
            });
        }
        return panel;
    }

    private static T Invoke<T>(Func<T> action) => Dispatcher.UIThread.CheckAccess()
        ? action()
        : Dispatcher.UIThread.Invoke(action);

    private static void Invoke(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Invoke(action);
        }
    }
}
