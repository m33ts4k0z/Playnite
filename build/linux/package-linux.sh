#!/usr/bin/env bash
set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
repo_root=$(realpath "$script_dir/../..")
output_dir=$(realpath -m "${OUTPUT_DIR:-$repo_root/artifacts/linux}")
configuration=${CONFIGURATION:-Release}
runtime=${RUNTIME_IDENTIFIER:-linux-x64}
dotnet_command=${DOTNET_COMMAND:-dotnet}
source_date_epoch=${SOURCE_DATE_EPOCH:-$(git -C "$repo_root" show -s --format=%ct HEAD)}
version=${PLAYNITE_VERSION:-$(git -C "$repo_root" rev-parse --short=12 HEAD)}

if [[ -n "${APPIMAGE_TOOL:-}" ]]; then
    if [[ ! -f "$APPIMAGE_TOOL" || ! -x "$APPIMAGE_TOOL" ]]; then
        echo "APPIMAGE_TOOL must point to an executable appimagetool file: $APPIMAGE_TOOL" >&2
        exit 2
    fi
fi
if [[ -n "${APPIMAGE_RUNTIME_FILE:-}" ]]; then
    if [[ -z "${APPIMAGE_TOOL:-}" ]]; then
        echo "APPIMAGE_RUNTIME_FILE requires APPIMAGE_TOOL." >&2
        exit 2
    fi
    if [[ ! -f "$APPIMAGE_RUNTIME_FILE" || ! -r "$APPIMAGE_RUNTIME_FILE" ]]; then
        echo "APPIMAGE_RUNTIME_FILE must point to a readable type-2 runtime: $APPIMAGE_RUNTIME_FILE" >&2
        exit 2
    fi
fi

work_root=$(mktemp -d)
cleanup() {
    rm -rf -- "$work_root"
}
trap cleanup EXIT

desktop_publish="$work_root/desktop"
fullscreen_publish="$work_root/fullscreen"
payload_dir="$work_root/payload"
portable_root="$work_root/Playnite-linux-x64"
app_dir="$work_root/Playnite.AppDir"

mkdir -p "$output_dir" "$desktop_publish" "$fullscreen_publish" "$payload_dir"

common_publish_args=(
    -c "$configuration"
    -f net10.0
    -r "$runtime"
    --self-contained true
    -p:Platform=x64
    -p:EnableWindowsTargeting=true
    -p:ContinuousIntegrationBuild=true
    -p:Deterministic=true
    -p:DebugType=None
    -p:DebugSymbols=false
)

"$dotnet_command" publish \
    "$repo_root/source/Playnite.DesktopApp.Avalonia/Playnite.DesktopApp.Avalonia.csproj" \
    "${common_publish_args[@]}" --disable-build-servers \
    --artifacts-path "$work_root/dotnet-artifacts/desktop" -o "$desktop_publish"
"$dotnet_command" publish \
    "$repo_root/source/Playnite.FullscreenApp.Avalonia/Playnite.FullscreenApp.Avalonia.csproj" \
    "${common_publish_args[@]}" --disable-build-servers \
    --artifacts-path "$work_root/dotnet-artifacts/fullscreen" -o "$fullscreen_publish"

find "$desktop_publish" "$fullscreen_publish" -type f -name '*.pdb' -delete

bash "$script_dir/merge-publish.sh" "$desktop_publish" "$fullscreen_publish" "$payload_dir"

mkdir -p "$portable_root/bin" "$portable_root/lib/playnite" "$portable_root/share/applications" \
    "$portable_root/share/icons/hicolor/256x256/apps" "$portable_root/share/metainfo"
cp -a "$payload_dir"/. "$portable_root/lib/playnite"/
install -m 0755 "$script_dir/assets/playnite" "$portable_root/bin/playnite"
install -m 0755 "$script_dir/assets/playnite-fullscreen" "$portable_root/bin/playnite-fullscreen"
install -m 0644 "$script_dir/assets/io.github.m33ts4k0z.Playnite.desktop" \
    "$portable_root/share/applications/io.github.m33ts4k0z.Playnite.desktop"
install -m 0644 "$script_dir/assets/io.github.m33ts4k0z.Playnite.appdata.xml" \
    "$portable_root/share/metainfo/io.github.m33ts4k0z.Playnite.appdata.xml"
install -m 0644 "$payload_dir/desktop/Assets/applogo.png" \
    "$portable_root/share/icons/hicolor/256x256/apps/io.github.m33ts4k0z.Playnite.png"

mkdir -p "$app_dir/usr/bin" "$app_dir/usr/lib/playnite" "$app_dir/usr/share/applications" \
    "$app_dir/usr/share/icons/hicolor/256x256/apps" "$app_dir/usr/share/metainfo"
cp -a "$payload_dir"/. "$app_dir/usr/lib/playnite"/
install -m 0755 "$script_dir/assets/AppRun" "$app_dir/AppRun"
install -m 0755 "$script_dir/assets/playnite" "$app_dir/usr/bin/playnite"
install -m 0755 "$script_dir/assets/playnite-fullscreen" "$app_dir/usr/bin/playnite-fullscreen"
install -m 0644 "$script_dir/assets/io.github.m33ts4k0z.Playnite.desktop" \
    "$app_dir/io.github.m33ts4k0z.Playnite.desktop"
install -m 0644 "$script_dir/assets/io.github.m33ts4k0z.Playnite.desktop" \
    "$app_dir/usr/share/applications/io.github.m33ts4k0z.Playnite.desktop"
install -m 0644 "$script_dir/assets/io.github.m33ts4k0z.Playnite.appdata.xml" \
    "$app_dir/usr/share/metainfo/io.github.m33ts4k0z.Playnite.appdata.xml"
install -m 0644 "$payload_dir/desktop/Assets/applogo.png" \
    "$app_dir/io.github.m33ts4k0z.Playnite.png"
install -m 0644 "$payload_dir/desktop/Assets/applogo.png" \
    "$app_dir/usr/share/icons/hicolor/256x256/apps/io.github.m33ts4k0z.Playnite.png"
ln -s io.github.m33ts4k0z.Playnite.png "$app_dir/.DirIcon"

archive_path="$output_dir/Playnite-$version-linux-x64.tar.gz"
appdir_archive_path="$output_dir/Playnite-$version-x86_64.AppDir.tar.gz"
rm -f -- "$archive_path" "$appdir_archive_path"
tar --sort=name --mtime="@$source_date_epoch" --owner=0 --group=0 --numeric-owner \
    -C "$work_root" -cf - "$(basename "$portable_root")" | gzip -n > "$archive_path"
tar --sort=name --mtime="@$source_date_epoch" --owner=0 --group=0 --numeric-owner \
    -C "$work_root" -cf - "$(basename "$app_dir")" | gzip -n > "$appdir_archive_path"

bash "$script_dir/verify-linux-package.sh" "$payload_dir" "$app_dir" "$archive_path"

checksum_files=("$(basename "$archive_path")" "$(basename "$appdir_archive_path")")
if [[ -n "${APPIMAGE_TOOL:-}" ]]; then
    appimage_tool=$(realpath "$APPIMAGE_TOOL")
    appimage_path="$output_dir/Playnite-$version-x86_64.AppImage"
    appimage_sort_file="$work_root/appimage.sort"
    (
        cd "$app_dir"
        find . -type f -printf '%P\n' | LC_ALL=C sort |
            sed 's/\\/\\\\/g; s/ /\\ /g' |
            awk '{ printf "%s %d\n", $0, 32768 - NR }'
    ) > "$appimage_sort_file"
    appimage_options=(
        --mksquashfs-opt=-sort
        "--mksquashfs-opt=$appimage_sort_file"
    )
    if [[ -n "${APPIMAGE_RUNTIME_FILE:-}" ]]; then
        appimage_options+=(--runtime-file "$(realpath "$APPIMAGE_RUNTIME_FILE")")
    fi
    rm -f -- "$appimage_path"
    SOURCE_DATE_EPOCH="$source_date_epoch" ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 \
        "$appimage_tool" "${appimage_options[@]}" "$app_dir" "$appimage_path"
    chmod 0755 "$appimage_path"
    checksum_files+=("$(basename "$appimage_path")")
fi

(
    cd "$output_dir"
    sha256sum "${checksum_files[@]}" > SHA256SUMS
)

echo "Linux packages written to $output_dir"
