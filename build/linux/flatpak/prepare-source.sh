#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "usage: prepare-source.sh LINUX_TAR_ARCHIVE" >&2
    exit 2
fi

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
payload_dir=$(realpath -m "$script_dir/payload")
expected_payload_dir=$(realpath -m "$script_dir/payload")
if [[ "$payload_dir" != "$expected_payload_dir" || "$payload_dir" == / ]]; then
    echo "refusing to replace unexpected payload path: $payload_dir" >&2
    exit 1
fi

rm -rf -- "$payload_dir"
mkdir -p "$payload_dir"
tar -xzf "$(realpath "$1")" --strip-components=1 -C "$payload_dir"

test -x "$payload_dir/bin/playnite"
test -x "$payload_dir/lib/playnite/desktop/Playnite.DesktopApp.Avalonia"
test -x "$payload_dir/lib/playnite/fullscreen/Playnite.FullscreenApp.Avalonia"
