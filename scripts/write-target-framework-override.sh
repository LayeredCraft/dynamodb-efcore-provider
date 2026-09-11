#!/usr/bin/env bash
# Writes or clears the repo-root TargetFrameworkOverride.props file, which forces the solution's
# projects to evaluate as genuinely single-targeted (rather than multi-targeted net10.0;net11.0)
# for version-specific NativeAOT/release operations. See
# docs/internal/ef11-native-aot-implementation-plan.md for why this file, not an externally
# supplied -p:TargetFramework/-p:TargetFrameworks property, is the correctness mechanism.
#
# Usage:
#   scripts/write-target-framework-override.sh net10.0
#   scripts/write-target-framework-override.sh net11.0
#   scripts/write-target-framework-override.sh --clear
#
# Deliberately has no knowledge of EF versions, NuGet, or release logic — just writes/removes a
# two-line MSBuild props file for the given TFM.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FILE="$ROOT_DIR/TargetFrameworkOverride.props"

usage() {
    echo "Usage: $(basename "$0") <net10.0|net11.0|--clear>" >&2
    exit 1
}

if [ $# -ne 1 ]; then
    usage
fi

if [ "$1" = "--clear" ]; then
    rm -f "$FILE"
    exit 0
fi

case "$1" in
    net10.0|net11.0)
        TFM="$1"
        ;;
    *)
        echo "Unsupported target framework: $1 (expected net10.0 or net11.0)" >&2
        usage
        ;;
esac

cat > "$FILE" <<EOF
<Project>
  <PropertyGroup>
    <TargetFramework>$TFM</TargetFramework>
    <TargetFrameworks></TargetFrameworks>
  </PropertyGroup>
</Project>
EOF
