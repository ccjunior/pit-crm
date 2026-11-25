#!/usr/bin/env bash
set -Eeuo pipefail
set +x

PROJECT=pitcrm-ac-t02-da13f183
HOST_HOME=/home/carlos
STATE_ROOT="$HOST_HOME/.pitcrm-test-runner-$PROJECT"
fail() { printf 'Runner cleanup blocked: %s\n' "$*" >&2; exit 1; }

[[ $# == 1 && "$1" =~ ^[a-f0-9]{48}$ ]] || fail 'usage: cleanup-runner.sh <runner-run-id>'
RUN_ID="$1"
[[ -d "$HOST_HOME" && ! -L "$HOST_HOME" && "$(stat -c '%u' "$HOST_HOME")" == "$(id -u)" ]] || fail 'operator home owner/path is invalid'
[[ -d "$STATE_ROOT" && ! -L "$STATE_ROOT" && "$(stat -c '%u' "$STATE_ROOT")" == "$(id -u)" && "$(stat -c '%a' "$STATE_ROOT")" == 700 ]] || fail 'Phase 2 state root owner/mode is invalid'
RUN_STATE="$STATE_ROOT/$RUN_ID"
[[ -d "$RUN_STATE" && ! -L "$RUN_STATE" && "$(stat -c '%u' "$RUN_STATE")" == "$(id -u)" && "$(stat -c '%a' "$RUN_STATE")" == 700 ]] || fail 'run state owner/mode is invalid'
[[ -f "$RUN_STATE/run-id" && ! -L "$RUN_STATE/run-id" && "$(stat -c '%a' "$RUN_STATE/run-id")" == 600 && "$(<"$RUN_STATE/run-id")" == "$RUN_ID" ]] || fail 'run state does not match the requested runner ID'

SNAPSHOT_ROOT="$RUN_STATE/snapshots"
if [[ -e "$SNAPSHOT_ROOT" ]]; then
  [[ -d "$SNAPSHOT_ROOT" && ! -L "$SNAPSHOT_ROOT" && "$(stat -c '%u' "$SNAPSHOT_ROOT")" == "$(id -u)" && "$(stat -c '%a' "$SNAPSHOT_ROOT")" == 700 && "$(realpath -e -- "$SNAPSHOT_ROOT")" == "$SNAPSHOT_ROOT" ]] || fail 'private snapshot directory owner/mode/path is invalid'
fi

CONTAINER_ID=""
if [[ -e "$RUN_STATE/container-id" ]]; then
  [[ -f "$RUN_STATE/container-id" && ! -L "$RUN_STATE/container-id" && "$(stat -c '%a' "$RUN_STATE/container-id")" == 600 ]] || fail 'container ID file is irregular'
  CONTAINER_ID="$(<"$RUN_STATE/container-id")"
  [[ "$CONTAINER_ID" =~ ^[a-f0-9]{64}$ ]] || fail 'container ID format is invalid'
else
  docker info >/dev/null 2>&1 || fail 'Docker daemon unavailable; preserving runner recovery state'
  MATCHING_OUTPUT="$(docker ps --all --quiet --no-trunc \
    --filter label=com.projectit.ac-t02.runner=true \
    --filter "label=com.projectit.ac-t02.project=$PROJECT" \
    --filter label=com.projectit.ac-t02.service=integration-runner \
    --filter "label=com.projectit.ac-t02.run-id=$RUN_ID")"
  MATCHING_CONTAINERS=()
  if [[ -n "$MATCHING_OUTPUT" ]]; then mapfile -t MATCHING_CONTAINERS <<< "$MATCHING_OUTPUT"; fi
  ((${#MATCHING_CONTAINERS[@]} <= 1)) || fail 'multiple containers match the requested runner run-id'
  if ((${#MATCHING_CONTAINERS[@]} == 1)); then CONTAINER_ID="${MATCHING_CONTAINERS[0]}"; fi
fi

if [[ -n "$CONTAINER_ID" ]]; then
  docker info >/dev/null 2>&1 || fail 'Docker daemon unavailable; preserving runner recovery state'
  if docker inspect "$CONTAINER_ID" >/dev/null 2>&1; then
    [[ "$(docker inspect --format '{{index .Config.Labels "com.projectit.ac-t02.runner"}}' "$CONTAINER_ID")" == true ]] || fail 'container is not an AC-T02 runner'
    [[ "$(docker inspect --format '{{index .Config.Labels "com.projectit.ac-t02.project"}}' "$CONTAINER_ID")" == "$PROJECT" ]] || fail 'container project label differs'
    [[ "$(docker inspect --format '{{index .Config.Labels "com.projectit.ac-t02.service"}}' "$CONTAINER_ID")" == integration-runner ]] || fail 'container service label differs'
    [[ "$(docker inspect --format '{{index .Config.Labels "com.projectit.ac-t02.run-id"}}' "$CONTAINER_ID")" == "$RUN_ID" ]] || fail 'container run-id label differs'
    docker rm --force "$CONTAINER_ID" >/dev/null
  else
    EXISTING_CONTAINER="$(docker ps --all --quiet --no-trunc --filter "id=$CONTAINER_ID")"
    [[ -z "$EXISTING_CONTAINER" ]] || fail 'existing container could not be inspected; preserving runner recovery state'
  fi
fi

if [[ -e "$RUN_STATE/image-reference" ]]; then
  [[ -f "$RUN_STATE/image-reference" && ! -L "$RUN_STATE/image-reference" && "$(stat -c '%a' "$RUN_STATE/image-reference")" == 600 ]] || fail 'runner image reference file is irregular'
  IMAGE_REFERENCE="$(<"$RUN_STATE/image-reference")"
  [[ "$IMAGE_REFERENCE" == "pitcrm-ac-t02-runner:$RUN_ID" ]] || fail 'runner image reference does not match run-id'
  if docker image inspect "$IMAGE_REFERENCE" >/dev/null 2>&1; then
    [[ "$(docker image inspect --format '{{index .Config.Labels "com.projectit.ac-t02.runner-image-run-id"}}' "$IMAGE_REFERENCE")" == "$RUN_ID" ]] || fail 'runner image ownership label differs; preserving image'
    IMAGE_ID="$(docker image inspect --format '{{.Id}}' "$IMAGE_REFERENCE")"
    if [[ -f "$RUN_STATE/image-id" ]]; then
      [[ ! -L "$RUN_STATE/image-id" && "$(stat -c '%a' "$RUN_STATE/image-id")" == 600 && "$(<"$RUN_STATE/image-id")" == "$IMAGE_ID" ]] || fail 'runner image ID differs from recorded run metadata'
    fi
    IMAGE_USERS="$(docker ps --all --quiet --no-trunc --filter "ancestor=$IMAGE_ID")"
    [[ -z "$IMAGE_USERS" ]] || fail 'runner image is still referenced by a container; preserving image and recovery state'
    docker image rm "$IMAGE_REFERENCE" >/dev/null
  fi
  rm -- "$RUN_STATE/image-reference"
fi
[[ ! -e "$RUN_STATE/image-id" ]] || rm -- "$RUN_STATE/image-id"

[[ ! -e "$RUN_STATE/container-id" ]] || rm -- "$RUN_STATE/container-id"
[[ ! -e "$SNAPSHOT_ROOT" ]] || rm -rf -- "$SNAPSHOT_ROOT"
rm -- "$RUN_STATE/run-id"
rmdir -- "$RUN_STATE"
printf 'Removed only runner state/container for run-id %s; Phase 1 PostgreSQL was not targeted.\n' "$RUN_ID"
