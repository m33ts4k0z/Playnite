# Avalonia Desktop theme API 3.0

Avalonia Desktop themes are restart-applied packages. Pass the package directory (or its `theme.yaml`) to `--theme`, or persist the same path as `ThemePath` in `avaloniaDesktop.json`.

```yaml
Id: My_Avalonia_Desktop_Theme
Name: My theme
Author: Theme author
Version: 1.0.0
Mode: Desktop
ThemeApiVersion: 3.0.0
Framework: Avalonia
EntryPoint: Theme.axaml
Styles:
  - Styles.axaml
```

`Theme.axaml` is a loose Avalonia `ResourceDictionary`. It can override resources and the `ControlTheme` contracts for `DesktopMainView` and `DesktopWindowChrome`. Optional Avalonia selector styles live in the files named by `Styles`. Package-relative paths must stay inside the theme directory. The app parses every dictionary and style before replacing active resources, so an invalid custom package leaves the built-in theme active.

The stable Desktop resource keys are `DesktopBackgroundBrush`, `DesktopPanelBrush`, `DesktopPanelStrongBrush`, `DesktopAccentBrush`, `DesktopTextBrush`, and `DesktopMutedBrush`. The current stable template parts are `PART_GridGameList`, `PART_ListGameList`, and `PART_SearchBox` on `DesktopMainView`, plus `PART_TitleBar`, `PART_MinimizeButton`, `PART_MaximizeButton`, and `PART_CloseButton` on `DesktopWindowChrome`.

WPF theme API 2.x XAML cannot be loaded directly. Migrate namespaces, setters, templates, triggers, and WPF-only controls to Avalonia equivalents, then set `Framework: Avalonia`, `Mode: Desktop`, and `ThemeApiVersion: 3.0.0`. Existing SDK v6 plugin controls can be hosted with `xmlns:foundation="using:Playnite.Avalonia.Controls"` and `<foundation:PluginElementHost Plugin="SourceName" Element="ElementName" GameContext="{Binding Game}" />`; this embeds the registered WPF control on Windows while native Avalonia controls remain the future SDK surface.

Use the cross-platform tool to create, validate, and package a theme:

```text
Playnite.Avalonia.ThemeTool new desktop "My theme" path/to/MyTheme
Playnite.Avalonia.ThemeTool validate path/to/MyTheme desktop
Playnite.Avalonia.ThemeTool pack path/to/MyTheme path/to/packages desktop
```

Validation covers the manifest, compatibility version, contained paths, XML safety, and required root elements. The target app performs final semantic Avalonia-XAML validation against its control assembly. For quick iteration, `--theme path/to/Overrides.axaml` remains available as a raw resource override; a manifest is required for distributable `.pthm` packages.
