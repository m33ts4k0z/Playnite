# Playnite Toolbox 3

Toolbox 3 scaffolds SDK 7 plugins and Avalonia theme API 3 packages by default. Existing SDK 6 plugins and WPF themes remain supported through explicit compatibility switches.

```powershell
# SDK 7 + Avalonia 12 plugin templates (default)
Toolbox.exe new GenericPlugin MyPlugin C:\source\addons
Toolbox.exe new LibraryPlugin MyLibrary C:\source\addons --sdk V7
Toolbox.exe new MetadataPlugin MyMetadata C:\source\addons --sdk V7

# Existing SDK 6 plugin workflow
Toolbox.exe new GenericPlugin MyLegacyPlugin C:\source\addons --sdk V6

# Avalonia theme API 3 (default) or legacy WPF theme
Toolbox.exe new DesktopTheme MyTheme C:\source\themes
Toolbox.exe new FullscreenTheme MyTheme C:\source\themes --framework Avalonia
Toolbox.exe new DesktopTheme MyLegacyTheme --framework Wpf
```

`pack` detects an Avalonia theme from `Framework: Avalonia` in `theme.yaml`, validates its mode and API contract, and creates a `.pthm` package. `update` validates current Avalonia API 3 themes; legacy WPF themes retain the existing changelog-based updater.

Generated SDK 7 projects target .NET 10, enable nullable analysis, treat warnings as errors, keep NuGet auditing enabled, compile settings views as typed Avalonia AXAML, and reference `PlayniteSDK` 7.0.0 plus Avalonia 12.1.0 as host-supplied compile assets.

`migration-check` analyzes an existing compiled-plugin source tree without modifying it. It reports SDK/package targets, host-asset isolation, strict build policy, WPF source/markup dependencies, and legacy synchronous API calls as text or stable JSON diagnostics:

```powershell
Toolbox.exe migration-check C:\source\MyPlugin
Toolbox.exe migration-check C:\source\MyPlugin --format Json --output migration-report.json
```

See [SDK7-MIGRATION.md](SDK7-MIGRATION.md) for the migration map, current native web-view limitations, and the required build/runtime completion gate.
