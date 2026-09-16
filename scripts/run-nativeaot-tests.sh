#!/usr/bin/env bash
# Publishes and runs the NativeAOT xUnit tests, and fails when the publish reports
# AOT warning IDs outside the reviewed baseline.
#
# Usage: scripts/run-nativeaot-tests.sh [CONFIG] [RUNTIME_IDENTIFIER] [FRAMEWORK]
#   CONFIG defaults to "Release".
#   RUNTIME_IDENTIFIER defaults to the host OS/architecture.
#   FRAMEWORK (net10.0 or net11.0) defaults to "net10.0". Requires a physical
#   TargetFrameworkOverride.props (see scripts/write-target-framework-override.sh) to already be
#   in place so the project evaluates as single-targeted for FRAMEWORK — this script does not
#   generate or clear that file itself.
set -euo pipefail

CONFIG="${1:-Release}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/tests/EntityFrameworkCore.DynamoDb.NativeAotTests/EntityFrameworkCore.DynamoDb.NativeAotTests.csproj"
OUTPUT="$ROOT_DIR/artifacts/nativeaot-tests"
LOG="$ROOT_DIR/artifacts/nativeaot-tests.log"

# Baseline AOT trimmer/AOT-compiler warning IDs observed with the pinned EF/SDK/ILCompiler
# versions. New warning IDs mean new reflection/dynamic surface (often provider code) and
# must be reviewed and either fixed or consciously added here. Occurrence counts are not
# pinned because MSBuild output duplication makes them non-deterministic.
BASELINE='IL2026 IL2055 IL2060 IL2067 IL2072 IL2075 IL2091 IL2104 IL3050 IL3053'

os=$(uname -s)
arch=$(uname -m)
case "$os" in
    Darwin) rid="osx" ;;
    Linux) rid="linux" ;;
    *) echo "Unsupported OS for NativeAOT tests: $os" >&2; exit 1 ;;
esac
case "$arch" in
    arm64|aarch64) rid="$rid-arm64" ;;
    x86_64) rid="$rid-x64" ;;
    *) echo "Unsupported architecture for NativeAOT tests: $arch" >&2; exit 1 ;;
esac
RID="${2:-$rid}"
FRAMEWORK="${3:-net10.0}"

mkdir -p "$ROOT_DIR/artifacts"

dotnet restore "$PROJECT" -p:TargetFrameworks="$FRAMEWORK" --runtime "$RID"

if ! dotnet publish "$PROJECT" --configuration "$CONFIG" --framework "$FRAMEWORK" --runtime "$RID" --no-restore --output "$OUTPUT" 2>&1 | tee "$LOG"; then
    echo "NativeAOT publish failed."
    exit 1
fi

found=$(grep -oE 'warning IL[0-9]+' "$LOG" | sort -u | awk '{print $2}' | sort || true)
new=$(comm -23 <(printf '%s\n' "$found") <(printf '%s\n' "$BASELINE" | tr ' ' '\n' | sort))
if [[ -n "$new" ]]; then
    echo "New NativeAOT warning IDs outside reviewed baseline: $new" >&2
    exit 1
fi
echo "NativeAOT warning IDs match reviewed baseline: ${found:-none}"

"$OUTPUT/EntityFrameworkCore.DynamoDb.NativeAotTests"
