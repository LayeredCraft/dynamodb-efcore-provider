#!/usr/bin/env bash
# Publishes and runs the NativeAOT smoke app against DynamoDB Local, and fails
# when the publish reports AOT warning IDs outside the reviewed baseline.
#
# Usage: scripts/run-nativeaot-smoke.sh [CONFIG] [RUNTIME_IDENTIFIER]
#   CONFIG defaults to "Release EF10" (EF11 native publish is broken; see
#   testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/AGENTS.md).
#   RUNTIME_IDENTIFIER defaults to the host OS/architecture.
set -euo pipefail

CONFIG="${1:-Release EF10}"
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT_DIR/testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/EntityFrameworkCore.DynamoDb.NativeAotSmoke.csproj"
OUTPUT="$ROOT_DIR/artifacts/nativeaot-smoke"
LOG="$ROOT_DIR/artifacts/nativeaot-smoke.log"

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
    *) echo "Unsupported OS for NativeAOT smoke: $os" >&2; exit 1 ;;
esac
case "$arch" in
    arm64|aarch64) rid="$rid-arm64" ;;
    x86_64) rid="$rid-x64" ;;
    *) echo "Unsupported architecture for NativeAOT smoke: $arch" >&2; exit 1 ;;
esac
RID="${2:-$rid}"

mkdir -p "$ROOT_DIR/artifacts"

dotnet restore "$PROJECT" -p:Configuration="$CONFIG" --runtime "$RID"

if ! dotnet publish "$PROJECT" --configuration "$CONFIG" --runtime "$RID" --no-restore --output "$OUTPUT" 2>&1 | tee "$LOG"; then
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

if ! command -v docker >/dev/null 2>&1; then
    echo "Docker is required for NativeAOT smoke execution but was not found on PATH." >&2
    exit 1
fi
if ! docker info >/dev/null 2>&1; then
    echo "Docker is required for NativeAOT smoke execution but is unavailable or not running." >&2
    exit 1
fi

container_id=$(docker run --rm -d --label dynamodb-efcore.nativeaot-smoke=true -p 127.0.0.1::8000 amazon/dynamodb-local:3.3.0)
cleanup() { docker rm -f "$container_id" >/dev/null 2>&1 || true; }
trap cleanup EXIT

mapped_port=$(docker port "$container_id" 8000/tcp | awk -F: 'NR == 1 {print $NF}') || true
if [ -z "$mapped_port" ]; then
    echo "Could not discover mapped DynamoDB Local port for container $container_id." >&2
    exit 1
fi

export DYNAMO_AOT_SMOKE_URL="http://127.0.0.1:$mapped_port"
ready=false
for attempt in $(seq 1 30); do
    status=$(curl --silent --output /dev/null --write-out '%{http_code}' \
        --request POST \
        --header 'Content-Type: application/x-amz-json-1.0' \
        --header 'X-Amz-Target: DynamoDB_20120810.ListTables' \
        --data '{"Limit":1}' "$DYNAMO_AOT_SMOKE_URL" || true)
    if [ "$status" = 200 ] || [ "$status" = 400 ]; then
        ready=true
        break
    fi
    sleep 1
done
if [ "$ready" != true ]; then
    echo "DynamoDB Local did not become ready: $DYNAMO_AOT_SMOKE_URL" >&2
    exit 1
fi

"$OUTPUT/EntityFrameworkCore.DynamoDb.NativeAotSmoke"