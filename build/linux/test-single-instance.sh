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
pipe_path=
forward_timeout_seconds=${SINGLE_INSTANCE_FORWARD_TIMEOUT_SECONDS:-30}
if [[ ! "$forward_timeout_seconds" =~ ^[1-9][0-9]*$ ]]; then
    echo "SINGLE_INSTANCE_FORWARD_TIMEOUT_SECONDS must be a positive integer." >&2
    exit 2
fi
primary_is_running() {
    [[ -n "$primary_pid" ]] || return 1
    kill -0 "$primary_pid" 2>/dev/null || return 1

    local process_state
    process_state=$(ps -o stat= -p "$primary_pid" 2>/dev/null | tr -d '[:space:]')
    [[ -n "$process_state" && "$process_state" != Z* ]]
}
cleanup() {
    if [[ -n "$primary_pid" ]]; then
        if primary_is_running; then
            kill "$primary_pid" 2>/dev/null || true
        fi
        wait "$primary_pid" 2>/dev/null || true
    fi
    if [[ -n "$pipe_path" ]]; then
        rm -f -- "$pipe_path"
    fi
    rm -rf -- "$test_root"
}
trap cleanup EXIT

profile_dir="$test_root/profile"
mkdir -p "$profile_dir"

if [[ -n "${DISPLAY:-}" ]]; then
    primary_command=("$desktop_executable")
elif command -v xvfb-run >/dev/null 2>&1; then
    primary_command=(xvfb-run --auto-servernum "$desktop_executable")
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

for ((attempt = 0; attempt < forward_timeout_seconds * 10; attempt++)); do
    if ! primary_is_running; then
        cat "$test_root/primary.log" >&2
        echo "The primary desktop process exited before accepting commands." >&2
        exit 1
    fi

    if [[ -S "$pipe_path" ]]; then
        break
    fi
    sleep 0.1
done

if [[ ! -S "$pipe_path" ]] || ! primary_is_running; then
    cat "$test_root/primary.log" >&2
    echo "The primary desktop process did not create its command pipe." >&2
    exit 1
fi

forward_command() {
    local label=$1
    shift
    set +e
    timeout --signal=TERM --kill-after=5s "${forward_timeout_seconds}s" \
        "$desktop_executable" --userdatadir "$profile_dir" "$@"
    local status=$?
    set -e
    if [[ $status -ne 0 ]]; then
        cat "$test_root/primary.log" >&2
        if [[ $status -eq 124 ]]; then
            echo "Single-instance $label forwarding exceeded ${forward_timeout_seconds}s." >&2
        else
            echo "Single-instance $label forwarding exited with code $status." >&2
        fi
        return 1
    fi
}

forward_command focus
forward_command shutdown --shutdown

for ((attempt = 0; attempt < forward_timeout_seconds * 10; attempt++)); do
    if ! primary_is_running; then
        set +e
        wait "$primary_pid"
        primary_status=$?
        set -e
        primary_pid=
        if [[ $primary_status -ne 0 ]]; then
            cat "$test_root/primary.log" >&2
            echo "The primary desktop process exited with code $primary_status after shutdown forwarding." >&2
            exit 1
        fi
        echo "Single-instance focus and shutdown forwarding passed."
        exit 0
    fi
    sleep 0.1
done

cat "$test_root/primary.log" >&2
echo "The primary desktop process did not shut down within ${forward_timeout_seconds}s after command forwarding." >&2
exit 1
