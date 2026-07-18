# Phase 5 Avalonia Desktop pilot

This project is a side-by-side Desktop shell. It opens the same `Playnite.Core` database as the Fullscreen pilot, presents a virtualized library, live search and filters, and a details pane through the Phase 3 loose-theme runtime. It does not replace or modify `Playnite.DesktopApp`; the WPF application remains the production Desktop path.

Run the real library with `Playnite.DesktopApp.Avalonia.exe`, optionally using `--userdatadir` or `--library-path`. Run the isolated contract harness with `--self-test`; it creates and removes a temporary 1,000-game Core database.

This first Phase 5 slice deliberately stops before plugin UI/settings, metadata editing, imports, updates, web views, tray integration, and window-chrome parity. Those are the next Desktop workstreams, now that the shell/library/theme/virtualization boundary is executable.
