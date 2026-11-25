#!/usr/bin/env bash

fail() { printf 'AC-T02 runner preflight blocked: %s\n' "$*" >&2; exit 1; }

check_readonly_tree_access() {
  local path="$1" description="$2" unsafe_path
  local -a exclude_generated=()
  case "${3:-}" in
    '') ;;
    exclude-generated) exclude_generated=( \( -type d \( -name bin -o -name obj \) -prune \) -o ) ;;
    *) fail 'unknown read-only tree inspection option' ;;
  esac
  unsafe_path="$(find "$path" -xdev "${exclude_generated[@]}" \
    \( -type l -o \( -type d \( ! -readable -o ! -executable \) \) -o \( -type f ! -readable \) \) \
    -print -quit 2>/dev/null)" || fail "$description could not be fully traversed"
  [[ -z "$unsafe_path" ]] || fail "$description contains symlinks or paths unreadable/untraversable by the operator"
}

check_private_snapshot() {
  local path="$1" description="$2" unsafe_path
  unsafe_path="$(find "$path" -xdev \
    \( -type l -o -perm /0077 -o \( -type d \( ! -readable -o ! -executable \) \) -o \( -type f ! -readable \) \) \
    -print -quit 2>/dev/null)" || fail "$description could not be fully traversed"
  [[ -z "$unsafe_path" ]] || fail "$description contains symlinks or paths accessible by group/other"
}

check_no_committed_build_outputs() {
  local repository="$1" commit="$2"
  if ! git -C "$repository" ls-tree -r -z --name-only "$commit" backend \
    | python3 -c 'import sys; paths=sys.stdin.buffer.read().split(b"\0"); sys.exit(1 if any(part in (b"bin", b"obj") for path in paths for part in path.split(b"/")) else 0)'; then
    fail 'approved commit contains a bin/obj path or its tree could not be inspected'
  fi
}

create_committed_source_snapshot() {
  local repository="$1" commit="$2" destination="$3"
  check_no_committed_build_outputs "$repository" "$commit"
  [[ ! -e "$destination" && ! -L "$destination" ]] || fail 'source snapshot destination already exists'
  mkdir -m 700 -- "$destination"
  ( umask 077
    git -C "$repository" archive --format=tar "$commit" backend \
      | tar --extract --file=- --directory="$destination" --strip-components=1 \
        --no-same-owner --no-same-permissions
  ) || fail 'private source snapshot from the approved commit could not be created'
  [[ -f "$destination/PitCrm.sln" ]] || fail 'committed source snapshot is missing the backend solution'
  check_private_snapshot "$destination" 'committed source snapshot'
}
