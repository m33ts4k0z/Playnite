# Linux / Steam Deck manual test checklist (L-7)

Manual validation matrix for the Avalonia shells on Linux hardware. CI already covers
builds, self-tests, packaging, reproducibility, and Flatpak/AppImage smoke tests —
this list is only what needs human eyes and real hardware. Companion to the Windows
matrix in `source/Playnite.DesktopApp.Avalonia/PHASE7-PARITY-CHECKLIST.md`.

Legend: [ ] untested · [x] pass · [!] fail (file an issue and link it here).

## Target rigs

- [ ] Steam Deck (SteamOS, gaming mode + desktop mode)
- [ ] A mainstream systemd distribution (Ubuntu/Fedora/Arch) on X11
- [ ] Same distribution on Wayland (XWayland path)
- [ ] Flatpak install AND extracted tar/AppImage on at least one rig

## Controller (Fullscreen, Deck priority)

- [ ] Full navigation with the Deck's built-in controls: D-pad, sticks, A/B/X/Y,
      shoulders, triggers, Start/Select, hold-repeat feel
- [ ] Steam Input quirks: shell behaves with Steam running (double-input check)
      and without Steam running (raw SDL path)
- [ ] Hotplug: connect/disconnect an external pad mid-session — no stuck buttons,
      device list in Settings → Input updates live
- [ ] On-screen keyboard: full text entry via controller in search and text dialogs
- [ ] Guide button restores/refocuses the fullscreen window
- [ ] Per-device enable/disable in settings persists across restart

## Web views and authentication (the L-4 validation)

- [ ] Steam login end-to-end with the SDK v7 Steam plugin (WebKitGTK cookie
      bridge: login persists, library import succeeds afterwards)
- [ ] Cookie clear-on-startup option works (Settings → Advanced)
- [ ] A metadata plugin web view renders and scrapes correctly

## Power and session (Fullscreen menus)

- [ ] Suspend and resume with Playnite running — window recovers, controller
      input still works after resume
- [ ] Suspend/resume with a RUNNING GAME — game state tracking recovers
- [ ] Shutdown/restart/hibernate/lock/logout actions fire the systemd/loginctl
      paths (confirmations appear, Cancel is default)
- [ ] Battery percentage and charging state on Deck match reality; updates within 30 s

## Desktop shell UX

- [ ] Tray icon on a StatusNotifier desktop (KDE/GNOME w/ extension): icon shows,
      menu works (recent games, favorites, open, exit), minimize/close-to-tray
- [ ] Game launch end-to-end: native Linux game, Proton/Steam game, emulated game
- [ ] Create shortcut → .desktop file appears on the desktop and launches the game
      via playnite:// URI
- [ ] Drag-and-drop installs a .pext/.pthm; dropping an executable adds a game
- [ ] File/folder pickers open the portal dialogs (Flatpak) and native (tar) paths
- [ ] Open install directory uses the file manager (xdg-open)
- [ ] Fonts: default UI renders with a real face (fontconfig Sans/Monospace);
      CJK and RTL language selection renders correctly
- [ ] HiDPI: 125 %/200 % scale factors render crisp on X11 and Wayland

## Packaging-format behavior differences

- [ ] Flatpak: game launches escape the sandbox (flatpak-spawn --host), power
      actions work, controller udev access works, tray DBus permission works
- [ ] AppImage: same feature pass; self-test report lands in runtime storage,
      not the mount point
- [ ] Portable tar: settings/library live next to the install when portable
      layout is expected
- [ ] Single-instance forwarding works per format (second launch focuses the first)

## Mode switching

- [ ] Fullscreen → "Switch to Desktop" quits fullscreen and opens the desktop shell
      with the same library
- [ ] Desktop → start-in-fullscreen setting hands off to the fullscreen shell
- [ ] Both shells honor the same profile (settings written by one visible in the other
      where shared: language, library path)

## Add-ons on Linux

- [ ] Store shows SDK v7 add-ons installable; SDK v6 add-ons greyed with a clear reason
- [ ] Installing a v7 add-on end-to-end (license, download, queue, restart, load)
- [ ] Add-on updates apply on restart
