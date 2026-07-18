# Playnite 11 ecosystem changes (DRAFT)

Draft changelog for the Avalonia-based Playnite release, collecting every
change that affects extension developers, theme authors, and power users.
Not published; wording and version references get finalized at release.

## Runtime and platforms

- The applications now run on .NET 10. Windows builds are x64 and x86; the
  WebView2 runtime is required on Windows (preinstalled on Windows 11,
  delivered by Windows Update on Windows 10).
- Linux support is planned on the same codebase; SDK v6/WPF compatibility
  features are Windows-only and will not exist on Linux.

## Plugin developers

- **SDK v6 plugins keep working** on Windows through the compatibility host,
  including settings views, custom controls, converters, and web views.
  No recompile is required for the transition.
- **SDK v7** is the new Avalonia-native contract for compiled plugins:
  Avalonia controls and windows instead of WPF, asynchronous host APIs
  (dialogs, game operations, web views, settings), `.axaml` markup, and
  `net10.0` as the target framework. Plugins run in isolated load contexts
  and must not bundle `Playnite.SDK.dll` or Avalonia runtime assemblies.
  See `SDK7-MIGRATION.md` and the Toolbox `migration-check` command.
- Plugins compiled against a newer SDK 7.x than the installed Playnite are
  refused at load with a clear error; SDK 7 minor versions are additive.
- **Web views** use each operating system's native engine (WebView2,
  WKWebView, WebKit) instead of bundled Chromium. Not supported and failing
  with explicit errors: response metadata/body capture, per-view JavaScript
  disabling, and non-default SameSite/Priority cookie writes.
- **PowerShell script extensions** run under PowerShell 7.x (`pwsh`), not
  Windows PowerShell 5.1. Scripts relying on 5.1-only behavior need updates.
- `System.Windows.Interactivity` shipped with Playnite is gone; WPF-era
  code referencing that assembly breaks at runtime and should move to
  `Microsoft.Xaml.Behaviors`.
- Several Playnite-internal types moved from `Playnite.dll` to
  `Playnite.Core.dll`. Referencing Playnite-internal assemblies from plugins
  was never supported and SDK v7 loading now rejects it explicitly.
- Plugins bundling AngleSharp should rebuild against a patched release
  (1.5.2 or newer); Playnite no longer restores the vulnerable 0.9.9 line.

## Theme authors

- WPF themes (theme API 2.x) do not load in the Avalonia applications.
  Theme API 3 uses loose Avalonia `.axaml` packages, validated before apply;
  see `THEME-API-3.md` for the package model, stable contracts, and the
  cross-platform ThemeTool / Toolbox 3 workflows.
- Legacy WPF themes remain supported by the WPF applications during the
  transition. WPF themes that reference moved types via
  `clr-namespace:...;assembly=Playnite` need `assembly=Playnite.Core`, and
  themes referencing `System.Windows.Interactivity` break as above.
- Registered SDK v6 plugin controls are placed in Avalonia themes with
  `PluginElementHost` (Windows-only bridge) instead of the old
  `SourceName_ElementName` content-control convention.

## Users

- Existing libraries, extensions, and settings carry over; the WPF
  applications remain available side by side until the Avalonia applications
  reach parity sign-off.
