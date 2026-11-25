#!/usr/bin/env bash
set -Eeuo pipefail
set +x

command -v realpath >/dev/null || { printf 'AC-T02 runner preflight blocked: realpath is required.\n' >&2; exit 1; }
command -v readlink >/dev/null || { printf 'AC-T02 runner preflight blocked: readlink is required.\n' >&2; exit 1; }
command -v openssl >/dev/null || { printf 'AC-T02 runner preflight blocked: openssl is required.\n' >&2; exit 1; }
command -v find >/dev/null || { printf 'AC-T02 runner preflight blocked: find is required.\n' >&2; exit 1; }
command -v findmnt >/dev/null || { printf 'AC-T02 runner preflight blocked: findmnt is required.\n' >&2; exit 1; }
command -v jq >/dev/null || { printf 'AC-T02 runner preflight blocked: jq is required.\n' >&2; exit 1; }
command -v python3 >/dev/null || { printf 'AC-T02 runner preflight blocked: python3 is required for private input snapshots.\n' >&2; exit 1; }
command -v tar >/dev/null || { printf 'AC-T02 runner preflight blocked: tar is required for the committed source snapshot.\n' >&2; exit 1; }
command -v du >/dev/null || { printf 'AC-T02 runner preflight blocked: du is required to estimate private snapshot storage.\n' >&2; exit 1; }
command -v df >/dev/null || { printf 'AC-T02 runner preflight blocked: df is required to check private snapshot storage.\n' >&2; exit 1; }
SCRIPT_DIR="$(cd -- "$(dirname -- "$(readlink -f -- "$0")")" && pwd -P)"
source "$SCRIPT_DIR/snapshot-preflight.sh"
PROJECT=pitcrm-ac-t02-da13f183
SERVICE=postgres-test
NETWORK="${PROJECT}_pitcrm-test"
POSTGRES_IMAGE=postgres:18.4-alpine@sha256:122c9942437efcbbb8d595fc578dee7d26ee1543c2a8634d183adfa4a1e55b4d
POSTGRES_DIGEST=sha256:122c9942437efcbbb8d595fc578dee7d26ee1543c2a8634d183adfa4a1e55b4d
SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.302-azurelinux3.0@sha256:dbcd91573287583286212aaa14ae39ccf04f80818b33e2770dbbd5d948d23230
BASE_COMMIT=da13f183ac8bbaaec6c4635ce888e7590fa0cdb4
RUNNER_MEMORY=1536m
RUNNER_CPUS=1.0
RUNNER_PIDS=128
MIN_AVAILABLE_KIB=2883584
HOST_HOME=/home/carlos
PHASE1_STATE="$HOST_HOME/.pitcrm-test-secret-$PROJECT"
PHASE2_STATE_ROOT="$HOST_HOME/.pitcrm-test-runner-$PROJECT"
PHASE1_RUN_ID_FILE="$PHASE1_STATE/run-id"
PHASE1_SECRET="$PHASE1_STATE/password"
NUGET_CACHE=/home/carlos/openbot/workspace/.tools/nuget-packages

check_no_submounts() {
  local path="$1" description="$2" mounts
  mounts="$(findmnt --json --list --output TARGET)" || fail "$description mount table could not be read"
  jq -e --arg path "$path" '
    (.filesystems | type == "array") and
    ([.filesystems[].target | select(type != "string")] | length == 0) and
    ([.filesystems[].target | select(startswith($path + "/"))] | length == 0)
  ' <<< "$mounts" >/dev/null || fail "$description contains a submount or mount table output is invalid"
}

if (($# != 2)); then
  fail 'usage: run-integration-tests.sh <clean-source-repo> <expected-commit-sha>'
fi
SOURCE_INPUT="$1"
SOURCE_REPO="$(realpath -e -- "$SOURCE_INPUT")"
EXPECTED_COMMIT="$2"
[[ "$EXPECTED_COMMIT" =~ ^[a-f0-9]{40}$ ]] || fail 'expected commit must be a full SHA-1'
[[ "$SOURCE_INPUT" == "$SOURCE_REPO" ]] || fail 'source path must be canonical and contain no symlink components'
[[ -d "$SOURCE_REPO/.git" || -f "$SOURCE_REPO/.git" ]] || fail 'source path is not a Git worktree'
[[ "$(git -C "$SOURCE_REPO" rev-parse --show-toplevel)" == "$SOURCE_REPO" ]] || fail 'source path is not the repository root'
[[ "$(git -C "$SOURCE_REPO" rev-parse HEAD)" == "$EXPECTED_COMMIT" ]] || fail 'source HEAD does not match the requested commit'
git -C "$SOURCE_REPO" merge-base --is-ancestor "$BASE_COMMIT" "$EXPECTED_COMMIT" || fail 'source commit is not based on the approved main commit'
[[ -z "$(git -C "$SOURCE_REPO" status --porcelain --untracked-files=all)" ]] || fail 'source tree is not clean'
check_no_submounts "$SOURCE_REPO" 'source checkout'
[[ -d "$SOURCE_REPO/backend" && "$(realpath -e -- "$SOURCE_REPO/backend")" == "$SOURCE_REPO/backend" && -r "$SOURCE_REPO/backend" && -x "$SOURCE_REPO/backend" ]] || fail 'backend bind source is not canonical/readable/traversable'
check_no_submounts "$SOURCE_REPO/backend" 'backend source'
check_readonly_tree_access "$SOURCE_REPO/backend" 'backend source tree' exclude-generated

[[ -d "$HOST_HOME" && ! -L "$HOST_HOME" && "$(stat -c '%u' "$HOST_HOME")" == "$(id -u)" ]] || fail 'operator home owner/path is invalid'
(( (8#$(stat -c '%a' "$HOST_HOME") & 0022) == 0 )) || fail 'operator home is writable by group/other users'
[[ -d "$PHASE1_STATE" && ! -L "$PHASE1_STATE" ]] || fail 'Phase 1 credential state directory is missing or irregular'
[[ "$(stat -c '%u' "$PHASE1_STATE")" == "$(id -u)" && "$(stat -c '%a' "$PHASE1_STATE")" == 700 ]] || fail 'Phase 1 state directory owner/mode is invalid'
[[ -f "$PHASE1_RUN_ID_FILE" && ! -L "$PHASE1_RUN_ID_FILE" && "$(stat -c '%u' "$PHASE1_RUN_ID_FILE")" == "$(id -u)" && "$(stat -c '%a' "$PHASE1_RUN_ID_FILE")" == 600 ]] || fail 'Phase 1 run-id file owner/mode is invalid'
[[ -f "$PHASE1_SECRET" && ! -L "$PHASE1_SECRET" && "$(stat -c '%u' "$PHASE1_SECRET")" == "$(id -u)" && "$(stat -c '%a' "$PHASE1_SECRET")" == 600 && "$(stat -c '%s' "$PHASE1_SECRET")" == 65 ]] || fail 'Phase 1 credential file owner/mode/size is invalid'
LC_ALL=C grep -xEq '[a-f0-9]{64}' "$PHASE1_SECRET" || fail 'Phase 1 credential format is invalid'
RUN_ID="$(<"$PHASE1_RUN_ID_FILE")"
[[ "$RUN_ID" =~ ^[a-f0-9]{48}$ ]] || fail 'Phase 1 run-id format is invalid'

[[ -d "$NUGET_CACHE" && "$(realpath -e -- "$NUGET_CACHE")" == "$NUGET_CACHE" && -r "$NUGET_CACHE" && -x "$NUGET_CACHE" ]] || fail 'host offline NuGet package cache is missing, noncanonical, unreadable, or untraversable'
check_no_submounts "$NUGET_CACHE" 'host offline NuGet package cache'
check_readonly_tree_access "$NUGET_CACHE" 'host offline NuGet package cache'
[[ -d "$SOURCE_REPO/backend" ]] || fail 'backend source tree is missing'
MEM_AVAILABLE_KIB="$(awk '/^MemAvailable:/ {print $2}' /proc/meminfo)"
[[ "$MEM_AVAILABLE_KIB" =~ ^[0-9]+$ ]] || fail 'MemAvailable could not be read'
(( MEM_AVAILABLE_KIB >= MIN_AVAILABLE_KIB )) || fail "MemAvailable below ${MIN_AVAILABLE_KIB} KiB"

# The runner is attached only after the Phase 1 network is proven to contain PostgreSQL alone.
BASE_IMAGE_ARCH="$(docker image inspect --format '{{.Os}}/{{.Architecture}}' "$SDK_IMAGE" 2>/dev/null)" || fail 'pinned .NET SDK image is not preloaded'
[[ "$BASE_IMAGE_ARCH" == linux/arm64 ]] || fail 'pinned .NET SDK image architecture is not linux/arm64'
mapfile -t DB_MATCHING < <(docker ps --no-trunc -q \
  --filter "label=com.docker.compose.project=$PROJECT" \
  --filter "label=com.docker.compose.service=$SERVICE")
((${#DB_MATCHING[@]} == 1)) || fail 'expected exactly one active Phase 1 postgres-test container'
DB_CID="${DB_MATCHING[0]}"
[[ "$(docker inspect --format '{{index .Config.Labels "com.projectit.ac-t02.run-id"}}' "$DB_CID")" == "$RUN_ID" ]] || fail 'active PostgreSQL container does not match Phase 1 run-id'
[[ "$(docker inspect --format '{{.State.Status}}' "$DB_CID")" == running ]] || fail 'Phase 1 PostgreSQL container is not running'
[[ "$(docker inspect --format '{{.State.Health.Status}}' "$DB_CID")" == healthy ]] || fail 'Phase 1 PostgreSQL healthcheck is not healthy'
[[ "$(docker inspect --format '{{.Config.Image}}' "$DB_CID")" == "$POSTGRES_IMAGE" ]] || fail 'Phase 1 PostgreSQL image reference differs'
DB_IMAGE_ID="$(docker inspect --format '{{.Image}}' "$DB_CID")"
[[ "$(docker image inspect --format '{{.Os}}/{{.Architecture}}' "$DB_IMAGE_ID")" == linux/arm64 ]] || fail 'Phase 1 PostgreSQL image is not linux/arm64'
docker image inspect "$DB_IMAGE_ID" --format '{{json .RepoDigests}}' \
  | jq -e --arg digest "$POSTGRES_DIGEST" 'any(.[]; endswith("@" + $digest))' >/dev/null || fail 'Phase 1 PostgreSQL digest does not match'
[[ "$(docker inspect --format '{{.HostConfig.Memory}}' "$DB_CID")" == 805306368 ]] || fail 'Phase 1 PostgreSQL memory cap differs'
[[ "$(docker inspect --format '{{.HostConfig.NanoCpus}}' "$DB_CID")" == 500000000 ]] || fail 'Phase 1 PostgreSQL CPU cap differs'
[[ "$(docker inspect --format '{{.HostConfig.PidsLimit}}' "$DB_CID")" == 128 ]] || fail 'Phase 1 PostgreSQL PID cap differs'
[[ "$(docker inspect --format '{{.HostConfig.ShmSize}}' "$DB_CID")" == 67108864 ]] || fail 'Phase 1 PostgreSQL shared-memory cap differs'
TMPFS_OPTIONS="$(docker inspect --format '{{index .HostConfig.Tmpfs "/var/lib/postgresql"}}' "$DB_CID")"
[[ -n "$TMPFS_OPTIONS" && "$TMPFS_OPTIONS" != '<no value>' ]] || fail 'Phase 1 PGDATA is not configured as tmpfs'
TMPFS_SIZE="$(tr ',' '\n' <<< "$TMPFS_OPTIONS" | awk -F= '$1 == "size" {print $2}')"
[[ "$TMPFS_SIZE" == 268435456 || "$TMPFS_SIZE" == 256m || "$TMPFS_SIZE" == 256M ]] || fail 'Phase 1 PGDATA tmpfs limit differs from 256 MiB'
for REQUIRED_OPTION in rw noexec nosuid nodev; do
  tr ',' '\n' <<< "$TMPFS_OPTIONS" | grep -Fxq "$REQUIRED_OPTION" || fail "Phase 1 PGDATA tmpfs option missing: $REQUIRED_OPTION"
done
[[ "$(docker network inspect "$NETWORK" --format '{{.Internal}}')" == true ]] || fail 'Phase 1 database network is not internal'
[[ "$(docker inspect --format '{{.HostConfig.NetworkMode}}' "$DB_CID")" == "$NETWORK" ]] || fail 'Phase 1 PostgreSQL is attached to an unexpected network'
[[ "$(docker inspect --format '{{len .NetworkSettings.Networks}}' "$DB_CID")" == 1 ]] || fail 'Phase 1 PostgreSQL is attached to multiple networks'
DB_ALIASES="$(docker inspect --format "{{with index .NetworkSettings.Networks \"$NETWORK\"}}{{range .Aliases}}{{println .}}{{end}}{{end}}" "$DB_CID")"
grep -Fxq pitcrm-test-db <<< "$DB_ALIASES" || fail 'Phase 1 PostgreSQL internal DNS alias is missing'
ATTACHED="$(docker network inspect "$NETWORK" --format '{{range $id, $container := .Containers}}{{println $id}}{{end}}' | sed '/^$/d')"
[[ "$(wc -l <<< "$ATTACHED" | tr -d ' ')" == 1 && "$ATTACHED" == "$DB_CID" ]] || fail 'Phase 1 network must contain only the expected PostgreSQL before runner start'
[[ -z "$(docker inspect --format '{{range $port, $bindings := .NetworkSettings.Ports}}{{range $bindings}}{{.HostIp}}:{{.HostPort}}{{end}}{{end}}' "$DB_CID")" ]] || fail 'Phase 1 PostgreSQL has a published host port'
[[ -z "$(docker inspect --format '{{range .Mounts}}{{if eq .Type "volume"}}{{.Destination}}{{end}}{{end}}' "$DB_CID")" ]] || fail 'Phase 1 PostgreSQL has a persistent Docker volume'
[[ "$(docker inspect --format '{{range .Mounts}}{{if eq .Type "bind"}}{{.Destination}}{{end}}{{end}}' "$DB_CID")" == /run/secrets/pg_test_password ]] || fail 'Phase 1 PostgreSQL has an unexpected bind mount'
DB_ADDRESS="$(docker inspect --format "{{with index .NetworkSettings.Networks \"$NETWORK\"}}{{.IPAddress}}{{end}}" "$DB_CID")"
[[ "$DB_ADDRESS" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail 'Phase 1 PostgreSQL internal address is invalid'
DB_PORT=5432

command -v timeout >/dev/null || fail 'GNU timeout is required'
command -v flock >/dev/null || fail 'flock is required to prevent concurrent runner instances'
[[ ! -e "$PHASE2_STATE_ROOT" ]] || [[ -d "$PHASE2_STATE_ROOT" && ! -L "$PHASE2_STATE_ROOT" && "$(stat -c '%u' "$PHASE2_STATE_ROOT")" == "$(id -u)" && "$(stat -c '%a' "$PHASE2_STATE_ROOT")" == 700 ]] || fail 'Phase 2 runner state root has unsafe ownership or mode'

umask 077
mkdir -m 700 -- "$PHASE2_STATE_ROOT" 2>/dev/null || [[ -d "$PHASE2_STATE_ROOT" ]]
LOCK_FILE="$PHASE2_STATE_ROOT/runner.lock"
[[ ! -e "$LOCK_FILE" ]] || [[ -f "$LOCK_FILE" && ! -L "$LOCK_FILE" && "$(stat -c '%u' "$LOCK_FILE")" == "$(id -u)" && "$(stat -c '%a' "$LOCK_FILE")" == 600 ]] || fail 'Phase 2 runner lock file has unsafe ownership or mode'
exec 9>"$LOCK_FILE"
chmod 600 "$LOCK_FILE"
flock -n 9 || fail 'another AC-T02 Phase 2 runner is active'
RUNNER_RUN_ID="$(openssl rand -hex 24)"
RUNNER_IMAGE="pitcrm-ac-t02-runner:$RUNNER_RUN_ID"
RUN_STATE="$PHASE2_STATE_ROOT/$RUNNER_RUN_ID"
mkdir -m 700 -- "$RUN_STATE"
SNAPSHOT_ROOT="$RUN_STATE/snapshots"
SOURCE_SNAPSHOT="$SNAPSHOT_ROOT/backend"
CACHE_SNAPSHOT="$SNAPSHOT_ROOT/nuget-cache"
printf '%s\n' "$RUNNER_RUN_ID" > "$RUN_STATE/run-id"
chmod 600 "$RUN_STATE/run-id"
printf '%s\n' "$RUNNER_IMAGE" > "$RUN_STATE/image-reference"
chmod 600 "$RUN_STATE/image-reference"
RUN_STATE_CREATED=1

cleanup_on_exit() {
  local exit_status=$?
  trap - EXIT INT TERM HUP
  if [[ "${RUN_STATE_CREATED:-0}" == 1 ]]; then
    if ! "$SCRIPT_DIR/cleanup-runner.sh" "$RUNNER_RUN_ID"; then
      printf 'Runner cleanup incomplete. Recovery: %s/cleanup-runner.sh %s\n' "$SCRIPT_DIR" "$RUNNER_RUN_ID" >&2
      (( exit_status != 0 )) || exit_status=1
    fi
  fi
  exit "$exit_status"
}
trap cleanup_on_exit EXIT
trap 'exit 130' INT
trap 'exit 129' HUP
trap 'exit 143' TERM

SOURCE_BYTES="$(git -C "$SOURCE_REPO" archive --format=tar "$EXPECTED_COMMIT" backend | wc -c | tr -d ' ')"
CACHE_BYTES="$(du -s --apparent-size --count-links -B1 "$NUGET_CACHE" | awk 'NR == 1 { print $1 }')"
AVAILABLE_BYTES="$(df -B1 --output=avail "$PHASE2_STATE_ROOT" | awk 'NR == 2 { print $1 }')"
[[ "$SOURCE_BYTES" =~ ^[0-9]+$ && "$CACHE_BYTES" =~ ^[0-9]+$ && "$AVAILABLE_BYTES" =~ ^[0-9]+$ ]] || fail 'could not estimate snapshot storage requirements'
REQUIRED_BYTES=$((SOURCE_BYTES + CACHE_BYTES + 128 * 1024 * 1024))
(( AVAILABLE_BYTES >= REQUIRED_BYTES )) || fail 'insufficient disk space for source/cache snapshots and reserve'

check_no_submounts "$SOURCE_REPO" 'source checkout before snapshot'
check_no_submounts "$SOURCE_REPO/backend" 'backend source before snapshot'
check_no_submounts "$NUGET_CACHE" 'NuGet cache before snapshot'
mkdir -m 700 -- "$SNAPSHOT_ROOT"
create_committed_source_snapshot "$SOURCE_REPO" "$EXPECTED_COMMIT" "$SOURCE_SNAPSHOT"
[[ -z "$(git -C "$SOURCE_REPO" status --porcelain --untracked-files=all)" ]] || fail 'source worktree changed while creating the committed snapshot'
python3 "$SCRIPT_DIR/snapshot-tree.py" \
  "$NUGET_CACHE" "$CACHE_SNAPSHOT" || fail 'private NuGet cache snapshot could not be verified'
check_no_submounts "$SOURCE_REPO" 'source checkout after snapshot'
check_no_submounts "$SOURCE_REPO/backend" 'backend source after snapshot'
check_no_submounts "$NUGET_CACHE" 'NuGet cache after snapshot'

printf 'AC-T02 Phase 2 runner run-id: %s\n' "$RUNNER_RUN_ID"
if docker image inspect "$RUNNER_IMAGE" >/dev/null 2>&1; then
  fail 'unique runner image tag already exists; refusing to overwrite it'
fi
docker build --pull=false --network=none --platform=linux/arm64 \
  --label "com.projectit.ac-t02.runner-image-run-id=$RUNNER_RUN_ID" \
  --tag "$RUNNER_IMAGE" --file "$SCRIPT_DIR/Dockerfile.runner" "$SCRIPT_DIR"
[[ "$(docker image inspect --format '{{.Os}}/{{.Architecture}}' "$RUNNER_IMAGE")" == linux/arm64 ]] || fail 'built runner image is not linux/arm64'
[[ "$(docker image inspect --format '{{index .Config.Labels "com.projectit.ac-t02.runner-image-run-id"}}' "$RUNNER_IMAGE")" == "$RUNNER_RUN_ID" ]] || fail 'runner image ownership label differs'
printf '%s\n' "$(docker image inspect --format '{{.Id}}' "$RUNNER_IMAGE")" > "$RUN_STATE/image-id"
chmod 600 "$RUN_STATE/image-id"

CONTAINER_NAME="pitcrm-ac-t02-runner-$RUNNER_RUN_ID"
RUNNER_CID="$(docker create \
  --name "$CONTAINER_NAME" \
  --label com.projectit.ac-t02.runner=true \
  --label com.projectit.ac-t02.project="$PROJECT" \
  --label com.projectit.ac-t02.service=integration-runner \
  --label com.projectit.ac-t02.run-id="$RUNNER_RUN_ID" \
  --log-driver=local --log-opt max-size=1m --log-opt max-file=1 --log-opt compress=false \
  --network "$NETWORK" \
  --memory "$RUNNER_MEMORY" --memory-swap "$RUNNER_MEMORY" --cpus "$RUNNER_CPUS" --pids-limit "$RUNNER_PIDS" \
  --cap-drop ALL --security-opt no-new-privileges \
  --user "$(id -u):$(id -g)" --workdir /work \
  --env DOTNET_CLI_TELEMETRY_OPTOUT=1 --env DOTNET_NOLOGO=1 \
  --mount "type=bind,src=$SOURCE_SNAPSHOT,dst=/source,readonly" \
  --mount "type=bind,src=$PHASE1_SECRET,dst=/run/secrets/pg_test_password,readonly" \
  --mount "type=bind,src=$CACHE_SNAPSHOT,dst=/nuget-cache,readonly" \
  "$RUNNER_IMAGE" "$DB_ADDRESS" "$DB_PORT")"
[[ "$RUNNER_CID" =~ ^[a-f0-9]{64}$ ]] || fail 'Docker did not return a valid runner container ID'
printf '%s\n' "$RUNNER_CID" > "$RUN_STATE/container-id"
chmod 600 "$RUN_STATE/container-id"

# Validate the effective Docker create configuration before any runner code can execute.
docker inspect "$RUNNER_CID" --format '{{json .}}' | jq -e \
  --arg network "$NETWORK" \
  --arg source "$SOURCE_SNAPSHOT" \
  --arg secret "$PHASE1_SECRET" \
  --arg cache "$CACHE_SNAPSHOT" \
  --argjson memory 1610612736 \
  --argjson nanoCpus 1000000000 \
  --argjson pids 128 \
  '(.HostConfig.NetworkMode == $network) and
   (.HostConfig.PortBindings == null or (.HostConfig.PortBindings | length == 0)) and
   (.HostConfig.Binds == null or (.HostConfig.Binds | length == 0)) and
   (.HostConfig.Mounts | length == 3) and
   ([.Mounts[] | select(.Type == "bind" and .RW == false)] | length == 3) and
   ([.Mounts[] | select(.Source == $source and .Destination == "/source")] | length == 1) and
   ([.Mounts[] | select(.Source == $secret and .Destination == "/run/secrets/pg_test_password")] | length == 1) and
   ([.Mounts[] | select(.Source == $cache and .Destination == "/nuget-cache")] | length == 1) and
   (.HostConfig.Memory == $memory) and (.HostConfig.MemorySwap == $memory) and
   (.HostConfig.NanoCpus == $nanoCpus) and (.HostConfig.PidsLimit == $pids) and
   (.HostConfig.Privileged == false) and (.HostConfig.CapDrop == ["ALL"]) and
   ((.HostConfig.CapAdd // []) | length == 0) and
   (any(.HostConfig.SecurityOpt[]?; startswith("no-new-privileges"))) and
   (.HostConfig.NetworkMode != "host") and
   ([.Mounts[] | select(.Source | test("docker\\.sock$"))] | length == 0) and
   (.NetworkSettings.Networks | keys == [$network]) and
   (.NetworkSettings.Ports == null or ([.NetworkSettings.Ports[]? | .[]?] | length == 0))' \
  >/dev/null || fail 'created runner configuration differs from the required isolated profile'

docker start "$RUNNER_CID" >/dev/null
RUNNER_EXIT="$(timeout --signal=TERM --kill-after=15s 1200s docker wait "$RUNNER_CID")" || fail 'runner exceeded its execution timeout or could not be observed'
docker logs "$RUNNER_CID"
[[ "$RUNNER_EXIT" == 0 ]] || fail "selected integration test runner exited with status $RUNNER_EXIT"
printf 'Selected AC-T02 integration checks completed. PostgreSQL Phase 1 container was left untouched.\n'
