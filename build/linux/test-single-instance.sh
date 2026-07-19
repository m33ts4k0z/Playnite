#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
    echo "Usage: $0 <playnite-desktop-executable>" >&2
    exit 2
fi

desktop_executable=$(realpath "$1")
if [[ ! -x "$desktop_executable" ]]; then
    echo "Desktop executable is not runnable: $desktop_executable" >&2
    exit 2
fi

test_root=$(mktemp -d)
primary_pid=
cleanup() {
    if [[ -n "$primary_pid" ]] && kill -0 "$primary_pid" 2>/dev/null; then
        kill "$primary_pid" 2>/dev/null || true
        wait "$primary_pid" 2>/dev/null || true
    fi
    rm -rf -- "$test_root"
}
trap cleanup EXIT

profile_dir="$test_root/profile"
mkdir -p "$profile_dir"

if command -v xvfb-run >/dev/null 2>&1; then
    primary_command=(xvfb-run --auto-servernum "$desktop_executable")
elif [[ -n "${DISPLAY:-}" ]]; then
    primary_command=("$desktop_executable")
else
    echo "Single-instance UI testing requires xvfb-run or an active DISPLAY." >&2
    exit 2
fi

"${primary_command[@]}" \
    --userdatadir "$profile_dir" --startclosedtotray >"$test_root/primary.log" 2>&1 &
primary_pid=$!

endpoint="playnite-avalonia|desktop|$(realpath "$profile_dir")"
endpoint_hash=$(printf '%s' "$endpoint" | tr '[:upper:]' '[:lower:]' | \
    sha256sum | cut -d ' ' -f 1 | tr '[:lower:]' '[:upper:]')
pipe_path="${TMPDIR:-/tmp}/CoreFxPipe_Playnite_$endpoint_hash"

for _ in $(seq 1 100); do
    if ! kill -0 "$primary_pid" 2>/dev/null; then
        cat "$test_root/primary.log" >&2
        echo "The primary desktop process exited before accepting commands." >&2
        exit 1
    fi

    if [[ -S "$pipe_path" ]]; then
        break
    fi
    sleep 0.1
done

if [[ ! -S "$pipe_path" ]]; then
    cat "$test_root/primary.log" >&2
    echo "The primary desktop process did not create its command pipe." >&2
    exit 1
fi

"$desktop_executable" --userdatadir "$profile_dir"
"$desktop_executable" --userdatadir "$profile_dir" --shutdown

for _ in $(seq 1 100); do
    if ! kill -0 "$primary_pid" 2>/dev/null; then
        wait "$primary_pid"
        primary_pid=
        echo "Single-instance focus and shutdown forwarding passed."
        exit 0
    fi
    sleep 0.1
done

cat "$test_root/primary.log" >&2
echo "The primary desktop process did not shut down after command forwarding." >&2
exit 1
