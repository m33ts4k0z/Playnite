# Shared Avalonia application host

`Playnite.Avalonia.App` contains application-level services shared by the side-by-side Avalonia Fullscreen and Desktop pilots. It adapts `Playnite.Core` game actions, extensions, notifications, dialogs, settings, and the legacy plugin API without depending on either shell's view models or theme.

Shell-specific behavior is supplied through `AvaloniaHostCallbacks`, `IAvaloniaHostSettings`, and `IAvaloniaDialogService`. The WPF applications remain the production paths while these adapters are hardened.

The legacy SDK web-view surface intentionally reports the remaining cross-platform CEF adapter gap. Plugin-provided WPF settings views, custom controls, and converters also still require Avalonia-specific host implementations.
