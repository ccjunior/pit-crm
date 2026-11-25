#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "$(readlink -f -- "$0")")" && pwd -P)"
source "$SCRIPT_DIR/snapshot-preflight.sh"
TEST_ROOT="$(mktemp -d)"
REPOSITORY="$TEST_ROOT/repository"
cleanup() {
  for generated in "$REPOSITORY/backend/bin" "$REPOSITORY/backend/obj"; do
    [[ ! -d "$generated" ]] || chmod 700 "$generated"
  done
  rm -rf -- "$TEST_ROOT"
}
trap cleanup EXIT
mkdir -p "$REPOSITORY/backend/src"
git -C "$REPOSITORY" init -q
git -C "$REPOSITORY" config user.name 'AC-T02 snapshot test'
git -C "$REPOSITORY" config user.email 'snapshot-test@example.invalid'
printf 'bin/\nobj/\n' > "$REPOSITORY/.gitignore"
printf 'solution\n' > "$REPOSITORY/backend/PitCrm.sln"
printf 'class Source {}\n' > "$REPOSITORY/backend/src/Source.cs"
git -C "$REPOSITORY" add .
git -C "$REPOSITORY" commit -qm 'fixture without generated outputs'
GOOD_COMMIT="$(git -C "$REPOSITORY" rev-parse HEAD)"

mkdir -p "$REPOSITORY/backend/bin" "$REPOSITORY/backend/obj"
printf 'ignored output\n' > "$REPOSITORY/backend/bin/output.dll"
printf 'ignored output\n' > "$REPOSITORY/backend/obj/project.assets.json"
chmod 000 "$REPOSITORY/backend/bin" "$REPOSITORY/backend/obj"
check_readonly_tree_access "$REPOSITORY/backend" 'backend with inaccessible generated directories' exclude-generated
if (check_readonly_tree_access "$REPOSITORY/backend" 'cache-style full inspection' >/dev/null 2>&1); then
  echo 'full tree validator skipped inaccessible bin/obj paths' >&2
  exit 1
fi
create_committed_source_snapshot "$REPOSITORY" "$GOOD_COMMIT" "$TEST_ROOT/good-snapshot"
[[ ! -e "$TEST_ROOT/good-snapshot/bin" && ! -e "$TEST_ROOT/good-snapshot/obj" ]]

chmod 700 "$REPOSITORY/backend/bin" "$REPOSITORY/backend/obj"
rm -rf -- "$REPOSITORY/backend/bin" "$REPOSITORY/backend/obj"

printf 'unsafe source\n' > "$REPOSITORY/backend/src/unreadable.cs"
chmod 000 "$REPOSITORY/backend/src/unreadable.cs"
if [[ ! -r "$REPOSITORY/backend/src/unreadable.cs" ]]; then
  if (check_readonly_tree_access "$REPOSITORY/backend" 'backend with unreadable source' exclude-generated >/dev/null 2>&1); then
    echo 'source accessibility validator accepted an unreadable source file' >&2
    exit 1
  fi
else
  echo 'SKIP unreadable-file check: current user can read mode-000 files'
fi
chmod 600 "$REPOSITORY/backend/src/unreadable.cs"
rm -- "$REPOSITORY/backend/src/unreadable.cs"

ln -s Source.cs "$REPOSITORY/backend/src/unsafe-link.cs"
if (check_readonly_tree_access "$REPOSITORY/backend" 'backend with source symlink' exclude-generated >/dev/null 2>&1); then
  echo 'source accessibility validator accepted a source symlink' >&2
  exit 1
fi
rm -- "$REPOSITORY/backend/src/unsafe-link.cs"

mkdir -p "$REPOSITORY/backend/bin" "$REPOSITORY/backend/obj"
printf 'unexpected committed output\n' > "$REPOSITORY/backend/bin/committed.dll"
printf 'unexpected committed output\n' > "$REPOSITORY/backend/obj/committed.assets.json"
git -C "$REPOSITORY" add -f backend/bin/committed.dll backend/obj/committed.assets.json
git -C "$REPOSITORY" commit -qm 'fixture with committed generated outputs'
BAD_COMMIT="$(git -C "$REPOSITORY" rev-parse HEAD)"
if (create_committed_source_snapshot "$REPOSITORY" "$BAD_COMMIT" "$TEST_ROOT/rejected-snapshot" >/dev/null 2>&1); then
  echo 'source snapshot accepted committed bin/obj content' >&2
  exit 1
fi
[[ ! -e "$TEST_ROOT/rejected-snapshot" ]]

echo 'snapshot preflight checks passed: inaccessible untracked bin/obj ignored and excluded; symlink/unreadable source rejected; committed bin/obj rejected before snapshot creation'
