#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 || $# -gt 3 ]]; then
    echo "usage: verify-linux-package.sh PAYLOAD_DIR APPDIR [TAR_ARCHIVE]" >&2
    exit 2
fi

payload_dir=$(realpath "$1")
app_dir=$(realpath "$2")
archive=${3:-}

required_payload=(
    desktop/Playnite.DesktopApp.Avalonia
    fullscreen/Playnite.FullscreenApp.Avalonia
    desktop/Playnite.Core.dll
    fullscreen/Playnite.Core.dll
    desktop/Playnite.SDK.dll
    fullscreen/Playnite.SDK.dll
    fullscreen/gamecontrollerdb.txt
    desktop/Assets/applogo.png
    desktop/Localization/english.axaml
    desktop/Localization/Languages/LocSource.axaml
    fullscreen/Localization/english.axaml
)
for relative_path in "${required_payload[@]}"; do
    test -e "$payload_dir/$relative_path" || {
        echo "missing payload file: $relative_path" >&2
        exit 1
    }
done

for forbidden in Playnite.WpfPluginSupport.dll PresentationFramework.dll SDL2.dll SDL3.dll; do
    if find "$payload_dir" -type f -name "$forbidden" -print -quit | grep -q .; then
        echo "Windows-only payload leaked into Linux package: $forbidden" >&2
        exit 1
    fi
done

test -x "$app_dir/AppRun"
test -f "$app_dir/io.github.m33ts4k0z.Playnite.desktop"
test -f "$app_dir/io.github.m33ts4k0z.Playnite.png"
test -f "$app_dir/usr/share/metainfo/io.github.m33ts4k0z.Playnite.appdata.xml"

if command -v desktop-file-validate >/dev/null 2>&1; then
    desktop-file-validate "$app_dir/io.github.m33ts4k0z.Playnite.desktop"
fi
if command -v appstreamcli >/dev/null 2>&1; then
    appstreamcli validate --no-net "$app_dir/usr/share/metainfo/io.github.m33ts4k0z.Playnite.appdata.xml"
fi

if [[ -n "$archive" ]]; then
    archive=$(realpath "$archive")
    while IFS= read -r entry; do
        if [[ "$entry" = /* || "$entry" == *"../"* ]]; then
            echo "unsafe archive entry: $entry" >&2
            exit 1
        fi
    done < <(tar -tzf "$archive")
fi

echo "Linux payload, AppDir, desktop metadata, and archive checks passed."
