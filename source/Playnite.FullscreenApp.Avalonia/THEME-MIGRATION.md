# Avalonia Fullscreen theme API 3.0

Avalonia Fullscreen themes are loose, restart-applied packages. Pass the package directory (or its `theme.yaml`) to `--theme`, or persist the same path in `avaloniaFullscreen.json`.

```yaml
Id: My_Avalonia_Fullscreen_Theme
Name: My theme
Author: Theme author
Version: 1.0.0
Mode: Fullscreen
ThemeApiVersion: 3.0.0
Framework: Avalonia
EntryPoint: Theme.axaml
Styles:
  - Styles.axaml
```

`Theme.axaml` is a loose Avalonia `ResourceDictionary`. It normally overrides brushes and the `ControlTheme` for `FullscreenMainView`. Optional selector styles live in the files named by `Styles`. Package-relative paths may not escape the theme directory. The loader parses every dictionary and style before replacing active resources, so an invalid package leaves the default theme active.

WPF theme API 2.x XAML cannot be loaded directly. Migrate namespaces, setters, control templates, triggers, and WPF-only controls to Avalonia equivalents, then change the manifest to `Framework: Avalonia` and `ThemeApiVersion: 3.0.0`. The stable Fullscreen template parts currently are `PART_GameList`, `PART_SearchBox`, `PART_FilterList`, `PART_FirstSetting`, `PART_NotificationsList`, `PART_ActionList`, `PART_DialogOptions`, `PART_MenuFirstButton`, and `PART_DetailsPrimaryButton`.

Registered plugin game-view controls have an explicit Avalonia placeholder. With `xmlns:foundation="using:Playnite.Avalonia.Controls"`, replace the old `SourceName_ElementName` WPF `ContentControl` convention with `<foundation:PluginElementHost Plugin="SourceName" Element="ElementName" GameContext="{Binding SelectedGame.Game}" />`. The placeholder can load before its plugin, resolves when the registration arrives, and on Windows embeds the existing WPF control in an owned native child window. `PluginUserControl.GameContext` remains live as selection changes. This bridge is for SDK v6 compatibility; native Avalonia controls are the long-term SDK v7 surface.

Optional audio assets go in `audio/navigation.wav`, `audio/activation.wav`, and `audio/background.ogg` (WAV, OGG, MP3, and FLAC are supported). Missing audio assets are valid and silently fall back to a quiet theme.

For quick iteration, `--theme path/to/Overrides.axaml` remains supported as a raw resource override without a manifest. Packaged themes are the compatibility contract intended for distribution.
