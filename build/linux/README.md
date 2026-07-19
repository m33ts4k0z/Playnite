# Linux packaging

`package-linux.sh` publishes the portable .NET 10 Desktop and Fullscreen shells,
merges their shared files only when byte-identical, and creates deterministic
`linux-x64` tar and AppDir archives. Set `APPIMAGE_TOOL` to a verified
`appimagetool-x86_64.AppImage` to also produce a type-2 AppImage.

Runtime requirements for the tar/AppImage builds are WebKitGTK 4.1 and SDL2.
The Flatpak build uses the GNOME runtime for WebKitGTK and delegates game
processes to the host through `flatpak-spawn --host`.

SDK v6 plugins that contain WPF settings or theme controls remain Windows-only
and are reported as such by the Linux shells. Controller input uses SDL2. Most
desktop distributions grant `/dev/input` access through the active logind seat;
if a controller is not detected, verify that the user can read its input device
and install the distribution's controller/Steam udev rules. The Flatpak grants
device access so SDL can receive the same controller events inside the sandbox.

The committed Flatpak manifest consumes the deterministic self-contained tar
staging prepared by `flatpak/prepare-source.sh`. This is the repository CI and
bundle manifest. A Flathub submission should replace the local `dir` source
with a tagged release archive and its SHA-256 checksum.

Examples:

```sh
bash ./build/linux/package-linux.sh
APPIMAGE_TOOL=/path/to/appimagetool-x86_64.AppImage bash ./build/linux/package-linux.sh
```

The process-level single-instance contract can be exercised against a native
desktop build with:

```sh
bash ./build/linux/test-single-instance.sh \
  ./source/Playnite.DesktopApp.Avalonia/bin/x64/Release/net10.0/Playnite.DesktopApp.Avalonia
```

Use `playnite --register-desktop` after extracting the tar archive when a
per-user `playnite://` handler is desired. Autostart can be managed with
`--enable-autostart`, `--enable-autostart-closed`, and `--disable-autostart`.
