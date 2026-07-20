#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "usage: verify-flatpak-manifest.sh MANIFEST" >&2
    exit 2
fi

manifest=$(realpath "$1")
manifest_dir=$(dirname "$manifest")

required_finish_args=(
    --share=ipc
    --share=network
    --socket=x11
    --socket=pulseaudio
    --device=all
    --filesystem=host
    --talk-name=org.freedesktop.Flatpak
    --talk-name=org.kde.StatusNotifierWatcher
    --own-name=org.kde.StatusNotifierItem-\*
)
for argument in "${required_finish_args[@]}"; do
    grep -Fq -- "- $argument" "$manifest" || {
        echo "Flatpak manifest is missing required permission: $argument" >&2
        exit 1
    }
done

if grep -Fq -- '- --socket=system-bus' "$manifest"; then
    echo "Flatpak must not expose the complete system bus." >&2
    exit 1
fi

for command_name in systemctl loginctl; do
    wrapper="$manifest_dir/${command_name}-host"
    test -f "$wrapper" || {
        echo "missing Flatpak host command wrapper: $wrapper" >&2
        exit 1
    }
    grep -Fq "exec flatpak-spawn --host $command_name" "$wrapper" || {
        echo "$wrapper does not delegate through flatpak-spawn --host." >&2
        exit 1
    }
    grep -Fq "path: ${command_name}-host" "$manifest" || {
        echo "Flatpak manifest does not stage ${command_name}-host." >&2
        exit 1
    }
    grep -Fq "${command_name}-host /app/bin/$command_name" "$manifest" || {
        echo "Flatpak manifest does not install the $command_name host wrapper." >&2
        exit 1
    }
done

echo "Flatpak StatusNotifier, host-launch, controller, display, and filesystem permissions passed."
