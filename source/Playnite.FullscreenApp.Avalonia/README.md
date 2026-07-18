# Playnite Avalonia Fullscreen pilot

This project is the Phase 4 side-by-side Fullscreen port. It does not replace or
modify `Playnite.FullscreenApp`; the WPF executable remains the production path
until feature parity is reached.

The pilot currently proves a vertical slice through the real `Playnite.Core`
database, loose runtime theme and localization loading, virtualized game tiles,
details/menu overlays, and SDL controller input routed through Avalonia focus and
explicit commands.

Run against the normal Playnite library:

```powershell
Playnite.FullscreenApp.Avalonia.exe --windowed
```

Select another library or user-data directory with `--library-path <path>` or
`--userdatadir <path>`. Use `--theme <axaml-file>` for a loose resource override.

Automated pilot validation uses an isolated temporary Playnite.Core database:

```powershell
Playnite.FullscreenApp.Avalonia.exe --self-test
```

Not yet at parity: plugin loading, game launch/install/uninstall orchestration,
filters/search, settings, notifications, dialogs, audio, and community theme
migration. The current Playnite SDK/Core targets are also Windows-only; the new
shell and SDL input code avoid Win32 APIs so the Linux path remains open once
those shared assemblies are made portable.
