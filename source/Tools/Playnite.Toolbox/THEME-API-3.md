# Playnite theme API 3 (Avalonia)

Theme API 3 is the theme model of the Avalonia-based Playnite applications. Themes are loose AXAML packages applied on restart; they replace WPF theme API 2.x, which the Avalonia applications cannot load.

## Package model

A theme is a directory with a `theme.yaml` manifest, an entry-point Avalonia `ResourceDictionary`, and optional selector-style files:

```yaml
Id: My_Avalonia_Theme
Name: My theme
Author: Theme author
Version: 1.0.0
Mode: Desktop            # or Fullscreen
ThemeApiVersion: 3.0.0
Framework: Avalonia
EntryPoint: Theme.axaml
Resources:
  - Views/GridItemTemplate.axaml
  - Views/DetailsPanel.axaml
Styles:
  - Styles.axaml
```

`Theme.axaml` overrides resources and the `ControlTheme` contracts of the target shell. Optional resource dictionaries named by `Resources` load in declaration order before the entry point, so large themes can keep independently maintained view templates in separate files. Optional Avalonia selector styles live in the files named by `Styles`. Resource, entry-point, and style paths must be unique within their respective lists, and every package-relative path must stay inside the theme directory. The application parses every dictionary and style **before** replacing active resources, so an invalid package leaves the built-in theme active instead of breaking the app.

For quick iteration, `--theme path/to/Overrides.axaml` is accepted as a raw resource override without a manifest. Packaged themes are the compatibility contract intended for distribution.

To apply a theme, pass the package directory (or its `theme.yaml`) to `--theme`, or persist the path as `ThemePath` in the shell's settings file (`avaloniaDesktop.json` / `avaloniaFullscreen.json`).

## Stable contracts

### Desktop (`Mode: Desktop`)

Stable resource keys: `DesktopBackgroundBrush`, `DesktopPanelBrush`, `DesktopPanelStrongBrush`, `DesktopAccentBrush`, `DesktopTextBrush`, `DesktopMutedBrush`.

Stable template parts: `PART_GridGameList`, `PART_ListGameList`, `PART_SearchBox` on `DesktopMainView`; `PART_TitleBar`, `PART_MinimizeButton`, `PART_MaximizeButton`, `PART_CloseButton` on `DesktopWindowChrome`.

### Fullscreen (`Mode: Fullscreen`)

Stable template parts on `FullscreenMainView`: `PART_GameList`, `PART_SearchBox`, `PART_FilterList`, `PART_FirstSetting`, `PART_NotificationsList`, `PART_ActionList`, `PART_DialogOptions`, `PART_MenuFirstButton`, `PART_DetailsPrimaryButton`.

Optional audio assets: `audio/navigation.wav`, `audio/activation.wav`, `audio/background.ogg` (WAV, OGG, MP3, and FLAC are supported). Missing audio assets are valid and fall back to a quiet theme.

## Migrating a WPF (API 2.x) theme

WPF theme XAML cannot be loaded directly. Migrate namespaces, setters, control templates, triggers, and WPF-only controls to their Avalonia equivalents, then set `Framework: Avalonia` and `ThemeApiVersion: 3.0.0` in the manifest.

Registered SDK v6 plugin controls keep an explicit placeholder. With `xmlns:foundation="using:Playnite.Avalonia.Controls"`, replace the old `SourceName_ElementName` WPF `ContentControl` convention with:

```xml
<!-- Desktop -->
<foundation:PluginElementHost Plugin="SourceName" Element="ElementName" GameContext="{Binding Game}" />
<!-- Fullscreen -->
<foundation:PluginElementHost Plugin="SourceName" Element="ElementName" GameContext="{Binding SelectedGame.Game}" />
```

The placeholder can load before its plugin, resolves when the registration arrives, and on Windows embeds the existing WPF control in an owned native child window; `PluginUserControl.GameContext` stays live as selection changes. This bridge exists for SDK v6 compatibility only — native Avalonia controls through SDK v7 are the long-term surface, and the bridge does not exist on non-Windows platforms.

## Tooling

The cross-platform theme tool scaffolds, validates, and packages both modes:

```text
Playnite.Avalonia.ThemeTool new desktop "My theme" path/to/MyTheme
Playnite.Avalonia.ThemeTool validate path/to/MyTheme desktop
Playnite.Avalonia.ThemeTool pack path/to/MyTheme path/to/packages desktop
```

Toolbox 3 scaffolds the same layout with `Toolbox.exe new DesktopTheme` / `new FullscreenTheme` (add `--framework Wpf` for a legacy theme), and `pack` recognizes Avalonia themes from `Framework: Avalonia` in the manifest.

Validation covers the manifest contract, compatibility version, contained paths, XML safety, and the required root elements. The target application performs the final semantic Avalonia-XAML parse against its concrete control assembly before applying a package.
