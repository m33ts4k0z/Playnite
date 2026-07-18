# Playnite Avalonia Fullscreen pilot

This project is the Phase 4 side-by-side Fullscreen port. It does not replace or
modify `Playnite.FullscreenApp`; the WPF executable remains the production path
until feature parity is reached.

The pilot runs against the real `Playnite.Core` database and shared Avalonia
application host. It includes loose API 3 themes and localization, virtualized
game tiles, controller-native search and filters, details/menu/settings/dialog
overlays, installed plugin loading, real play/install/uninstall orchestration,
notifications, SDL controller input and theme audio, legacy plugin converters,
and Windows-hosted compatibility for registered WPF game-view controls.

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

Remaining parity work includes legacy script/HDR and client shutdown policy in
the shared action runner, broader real-plugin testing, and final SDK/theme
tooling. Plugin web views are now supplied by the shared Avalonia application
host, with explicit SDK-v6 errors for its WPF-only window property and response-
body interception contract. The current Playnite SDK/Core and WPF compatibility
bridge are Windows-only; native Avalonia surfaces remain isolated from that
bridge so it can be removed with the future SDK v7 UI contract.
