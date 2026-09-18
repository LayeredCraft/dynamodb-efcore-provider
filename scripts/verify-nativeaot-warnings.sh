#!/usr/bin/env bash
# Fails when NativeAOT publishing emits an unreviewed warning ID.
set -euo pipefail

LOG="$1"
BASELINE='IL2026 IL2055 IL2060 IL2067 IL2072 IL2075 IL2091 IL2104 IL3050 IL3053'

found=$(grep -oE 'warning IL[0-9]+' "$LOG" | sort -u | awk '{print $2}' | sort || true)
new=$(comm -23 <(printf '%s\n' "$found") <(printf '%s\n' "$BASELINE" | tr ' ' '\n' | sort))

if [[ -n "$new" ]]; then
    echo "New NativeAOT warning IDs outside reviewed baseline: $new" >&2
    exit 1
fi

echo "NativeAOT warning IDs match reviewed baseline: ${found:-none}"
