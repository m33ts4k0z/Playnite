#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 || $# -gt 3 ]]; then
    echo "usage: verify-linux-package.sh PAYLOAD_DIR APPDIR [TAR_ARCHIVE]" >&2
    exit 2
fi

payload_dir=$(realpath "$1")
app_dir=$(realpath "$2")
archive=${3:-}

require_minimum_size() {
    local relative_path=$1
    local minimum_bytes=$2
    local actual_bytes
    actual_bytes=$(stat --dereference --format='%s' "$payload_dir/$relative_path")
    if [[ "$actual_bytes" -lt "$minimum_bytes" ]]; then
        echo "payload file is too small for the full UI: $relative_path ($actual_bytes < $minimum_bytes bytes)" >&2
        exit 1
    fi
}

require_asset_corpus() {
    local label=$1
    local relative_directory=$2
    local pattern=$3
    local minimum_count=$4
    local minimum_bytes=$5
    local count=0
    local bytes=0
    local asset

    while IFS= read -r -d '' asset; do
        count=$((count + 1))
        bytes=$((bytes + $(stat --dereference --format='%s' "$asset")))
    done < <(find "$payload_dir/$relative_directory" -xtype f -name "$pattern" -print0)

    if [[ "$count" -lt "$minimum_count" || "$bytes" -lt "$minimum_bytes" ]]; then
        echo "$label corpus is incomplete: $count files/$bytes bytes; expected at least $minimum_count/$minimum_bytes." >&2
        exit 1
    fi
}

required_payload=(
    desktop/Playnite.DesktopApp.Avalonia
    fullscreen/Playnite.FullscreenApp.Avalonia
    desktop/Playnite.DesktopApp.Avalonia.dll
    fullscreen/Playnite.FullscreenApp.Avalonia.dll
    desktop/Playnite.Avalonia.App.dll
    fullscreen/Playnite.Avalonia.App.dll
    desktop/Playnite.Avalonia.WebView.dll
    fullscreen/Playnite.Avalonia.WebView.dll
    desktop/Avalonia.Controls.WebView.dll
    fullscreen/Avalonia.Controls.WebView.dll
    desktop/Playnite.Core.dll
    fullscreen/Playnite.Core.dll
    desktop/Playnite.SDK.dll
    fullscreen/Playnite.SDK.dll
    desktop/Playnite.SDL.dll
    fullscreen/Playnite.SDL.dll
    desktop/SdkV7Host/Playnite.SDK.V7.Host.dll
    desktop/SdkV7Host/Playnite.SDK.dll
    fullscreen/SdkV7Host/Playnite.SDK.V7.Host.dll
    fullscreen/SdkV7Host/Playnite.SDK.dll
    desktop/gamecontrollerdb.txt
    fullscreen/gamecontrollerdb.txt
    desktop/Assets/applogo.png
    desktop/Assets/tray-default.png
    desktop/Assets/tray-bright.png
    desktop/Assets/tray-dark.png
    desktop/Localization/english.axaml
    desktop/Localization/Languages/LocSource.axaml
    fullscreen/Localization/english.axaml
    fullscreen/Localization/Languages/LocSource.axaml
    desktop/Themes/Desktop/Default/Theme.axaml
    desktop/Themes/Desktop/Default/Styles.axaml
    desktop/Themes/Desktop/Default/theme.yaml
    desktop/Themes/Desktop/Default/Views/AddonStore.axaml
    desktop/Themes/Desktop/Default/Views/DatabaseFields.axaml
    desktop/Themes/Desktop/Default/Views/EmulatorConfig.axaml
    fullscreen/Themes/Fullscreen/Default/Theme.axaml
    fullscreen/Themes/Fullscreen/Default/Styles.axaml
    fullscreen/Themes/Fullscreen/Default/theme.yaml
    fullscreen/Assets/Prompts/xbox-a.svg
    fullscreen/Assets/Prompts/ps-cross.svg
)
for relative_path in "${required_payload[@]}"; do
    test -e "$payload_dir/$relative_path" || {
        echo "missing payload file: $relative_path" >&2
        exit 1
    }
done

require_minimum_size desktop/Playnite.DesktopApp.Avalonia.dll 750000
require_minimum_size fullscreen/Playnite.FullscreenApp.Avalonia.dll 250000
require_minimum_size desktop/Playnite.Avalonia.App.dll 180000
require_minimum_size fullscreen/Playnite.Avalonia.App.dll 180000
require_asset_corpus "Desktop localization" desktop/Localization '*.axaml' 46 4000000
require_asset_corpus "Fullscreen localization" fullscreen/Localization '*.axaml' 46 4000000
require_asset_corpus "Desktop full theme" desktop/Themes/Desktop/Default '*' 16 250000
require_asset_corpus "Fullscreen full theme" fullscreen/Themes/Fullscreen/Default '*' 3 50000
require_asset_corpus "Fullscreen controller prompts" fullscreen/Assets/Prompts '*.svg' 6 1500

while IFS= read -r -d '' link; do
    resolved_link=$(realpath "$link")
    if [[ "$resolved_link" != "$payload_dir"/* ]]; then
        echo "payload symlink escapes its package root: $link -> $resolved_link" >&2
        exit 1
    fi
done < <(find "$payload_dir" -type l -print0)

for forbidden in Playnite.WpfPluginSupport.dll PresentationFramework.dll SDL2.dll SDL3.dll; do
    if find "$payload_dir" -xtype f -name "$forbidden" -print -quit | grep -q .; then
        echo "Windows-only payload leaked into Linux package: $forbidden" >&2
        exit 1
    fi
done

test -x "$app_dir/AppRun"
test -x "$app_dir/usr/bin/playnite"
test -x "$app_dir/usr/bin/playnite-fullscreen"
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

echo "Linux full-shell payload, AppDir, desktop metadata, and archive checks passed."
