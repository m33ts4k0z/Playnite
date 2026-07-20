# Linux packaging

`package-linux.sh` publishes the complete native .NET 10 Desktop and Fullscreen
shells, merges their shared files only when byte-identical, and creates
deterministic `linux-x64` tar and AppDir archives. The package verifier requires
the full compiled shell sizes, both default themes, the complete language
corpus, controller prompts, tray assets, and the isolated SDK v7 host. Set
`APPIMAGE_TOOL` to a verified `appimagetool-x86_64.AppImage` to also produce a
type-2 AppImage. CI also sets `APPIMAGE_RUNTIME_FILE` to a separately
checksum-pinned runtime, preventing the tool from downloading a moving runtime
during packaging.

Runtime requirements for the tar/AppImage builds are WebKitGTK 4.1, SDL2, and
SDL2_mixer.
The Flatpak build uses the GNOME runtime for WebKitGTK and delegates game
processes and Fullscreen power/session commands to the host through
`flatpak-spawn --host`.

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

The Flatpak grants the Desktop StatusNotifier item permission explicitly. It
does not request a global-hotkey portal because system-wide hotkeys are marked
unsupported by the Linux shell. Host filesystem access is required for game
libraries and launch targets; controller access uses `--device=all`.

CI builds the tar, AppDir, and AppImage twice with the commit timestamp in
`SOURCE_DATE_EPOCH` and requires byte-identical artifacts. The same check can be
run locally by packaging to two output directories and passing them to
`verify-reproducible-packages.sh`.

Native, AppImage, and Flatpak smoke runs pass through
`verify-full-selftest.sh`. It requires a complete verdict of at least 100
Desktop checks or 40 Fullscreen checks and terminates a hung run after five
minutes, so the retired minimal pilots cannot satisfy the release workflow.

Examples:

```sh
bash ./build/linux/package-linux.sh
APPIMAGE_TOOL=/path/to/appimagetool-x86_64.AppImage bash ./build/linux/package-linux.sh
APPIMAGE_TOOL=/path/to/appimagetool-x86_64.AppImage \
APPIMAGE_RUNTIME_FILE=/path/to/runtime-x86_64 bash ./build/linux/package-linux.sh
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
