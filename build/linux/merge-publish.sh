#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
    echo "usage: merge-publish.sh DESKTOP_DIR FULLSCREEN_DIR DESTINATION_DIR" >&2
    exit 2
fi

desktop_source=$(realpath "$1")
fullscreen_source=$(realpath "$2")
destination_root=$(realpath -m "$3")
desktop_destination="$destination_root/desktop"
fullscreen_destination="$destination_root/fullscreen"
common_destination="$destination_root/common"

mkdir -p "$desktop_destination" "$fullscreen_destination" "$common_destination"

mapfile -d '' relative_paths < <(
    {
        find "$desktop_source" -type f -printf '%P\0'
        find "$fullscreen_source" -type f -printf '%P\0'
    } | sort -zu
)

for relative_path in "${relative_paths[@]}"; do
    desktop_file="$desktop_source/$relative_path"
    fullscreen_file="$fullscreen_source/$relative_path"
    if [[ -f "$desktop_file" && -f "$fullscreen_file" ]] &&
       cmp --silent "$desktop_file" "$fullscreen_file"; then
        common_file="$common_destination/$relative_path"
        mkdir -p "$(dirname "$common_file")" \
            "$(dirname "$desktop_destination/$relative_path")" \
            "$(dirname "$fullscreen_destination/$relative_path")"
        cp -a "$desktop_file" "$common_file"
        desktop_target=$(realpath --relative-to="$(dirname "$desktop_destination/$relative_path")" "$common_file")
        fullscreen_target=$(realpath --relative-to="$(dirname "$fullscreen_destination/$relative_path")" "$common_file")
        ln -s "$desktop_target" "$desktop_destination/$relative_path"
        ln -s "$fullscreen_target" "$fullscreen_destination/$relative_path"
        continue
    fi

    if [[ -f "$desktop_file" ]]; then
        mkdir -p "$(dirname "$desktop_destination/$relative_path")"
        cp -a "$desktop_file" "$desktop_destination/$relative_path"
    fi
    if [[ -f "$fullscreen_file" ]]; then
        mkdir -p "$(dirname "$fullscreen_destination/$relative_path")"
        cp -a "$fullscreen_file" "$fullscreen_destination/$relative_path"
    fi
done

test -x "$desktop_destination/Playnite.DesktopApp.Avalonia"
test -x "$fullscreen_destination/Playnite.FullscreenApp.Avalonia"
