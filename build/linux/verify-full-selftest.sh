#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 3 ]]; then
    echo "usage: verify-full-selftest.sh LABEL MINIMUM_CHECKS COMMAND [ARGUMENTS...]" >&2
    exit 2
fi

label=$1
minimum_checks=$2
timeout_seconds=${SELFTEST_TIMEOUT_SECONDS:-300}
shift 2

if [[ ! "$minimum_checks" =~ ^[0-9]+$ ]]; then
    echo "MINIMUM_CHECKS must be a non-negative integer: $minimum_checks" >&2
    exit 2
fi
if [[ ! "$timeout_seconds" =~ ^[1-9][0-9]*$ ]]; then
    echo "SELFTEST_TIMEOUT_SECONDS must be a positive integer: $timeout_seconds" >&2
    exit 2
fi

report=$(mktemp)
cleanup() {
    rm -f -- "$report"
}
trap cleanup EXIT

set +e
timeout --signal=TERM --kill-after=15s "${timeout_seconds}s" "$@" 2>&1 | tee "$report"
command_status=${PIPESTATUS[0]}
set -e
if [[ $command_status -ne 0 ]]; then
    if [[ $command_status -eq 124 ]]; then
        echo "$label self-test exceeded ${timeout_seconds}s." >&2
        exit 1
    fi
    echo "$label self-test exited with code $command_status." >&2
    exit "$command_status"
fi

verdict=$(grep -E '^VERDICT: [0-9]+/[0-9]+ checks passed\.$' "$report" | tail -n 1 || true)
if [[ -z "$verdict" ]]; then
    echo "$label self-test did not emit a Playnite full-shell verdict." >&2
    exit 1
fi

passed=${verdict#VERDICT: }
passed=${passed%%/*}
total=${verdict#*/}
total=${total%% *}
if [[ "$passed" -ne "$total" ]]; then
    echo "$label self-test reported only $passed/$total passing checks." >&2
    exit 1
fi
if [[ "$total" -lt "$minimum_checks" ]]; then
    echo "$label emitted $total checks; at least $minimum_checks full-shell checks are required." >&2
    exit 1
fi

echo "$label full-shell self-test gate passed ($passed/$total)."
