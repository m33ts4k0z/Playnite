# Phase 5 Avalonia Desktop pilot

This project is a side-by-side Desktop shell. It opens the same `Playnite.Core` database as the Fullscreen pilot and uses the Phase 3 loose-theme runtime. It does not replace or modify `Playnite.DesktopApp`; the WPF application remains the production Desktop path.

Run the real library with `Playnite.DesktopApp.Avalonia.exe`, optionally using `--userdatadir` or `--library-path`. Run the isolated contract harness with `--self-test`; it creates and removes a temporary 1,000-game Core database and validates 17 Desktop contracts.

The current Phase 5 tranche includes virtualized grid and list views, Core filter presets, search, installed/favorite filters, sorting, flat virtualized grouping, selection-driven details, persisted Desktop settings, plugin loading, real play/install/uninstall orchestration, notifications, and synchronous Avalonia dialogs. Fullscreen and Desktop share those host services through `Playnite.Avalonia.App`.

Remaining Desktop workstreams include plugin settings/custom elements/converters, metadata editing and downloads, library imports and update flows, cross-platform CEF web views, tray/window chrome, broader real-plugin testing, and the Desktop theme API 3 migration/tooling contract.
