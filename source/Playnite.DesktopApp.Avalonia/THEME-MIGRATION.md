# Avalonia Desktop theme API 3.0

The theme API 3 author guide, including this shell's stable resource keys and
template parts, lives in the consolidated document:
[THEME-API-3.md](../Tools/Playnite.Toolbox/THEME-API-3.md).

Desktop specifics in short: `Mode: Desktop`, restart-applied packages via
`--theme` or `ThemePath` in `avaloniaDesktop.json`; the stable contract covers
the `DesktopMainView` and `DesktopWindowChrome` control themes. Theme packages
can declare an ordered `Resources` list in `theme.yaml`; those loose resource
dictionaries load before `EntryPoint`, allowing independently maintained view
templates without giving up runtime-editable theme markup.

## Track W appearance compatibility notes

The Avalonia Desktop shell applies the selected-game window background through
Skia's portable blur effect. `HighQualityBackgroundBlur` has no separate
Avalonia/Skia equivalent, so it is intentionally not persisted or presented as
a second quality mode; the configured blur radius is the complete runtime
control. WPF-only `TextRenderingMode` and `TextFormattingMode` are likewise not
portable Avalonia settings and are not emulated.

`UsedFieldsOnlyOnFilterLists`, `ShowHiddenInQuickLaunch`, and
`QuickLaunchItems` are deferred until their corresponding filter-field and
quick-launch surfaces exist in the Avalonia Desktop shell. Filter/explorer
panel position and width, details-list position and width, and the legacy
built-in top-panel visibility toggles are not applicable to the current layout:
filter navigation is in the sidebar, details has one live split layout, and
built-in navigation remains in the sidebar. `PluginTopPanelAlignment` is still
supported because plugin-provided top-panel items are a live SDK surface.
