#!/bin/sh
set -eu

EXPECTED_DB_ADDRESS="${1:?expected database address is required}"
EXPECTED_DB_PORT="${2:?expected database port is required}"
SECRET_FILE=/run/secrets/pg_test_password
SOURCE_DIR=/source
WORK_DIR=/work
PROBE_DIR="$WORK_DIR/connection-probe"
BACKEND_DIR="$WORK_DIR/backend"

case "$EXPECTED_DB_ADDRESS" in
  *[!0-9.]*|'') echo "Runner received an invalid expected database address." >&2; exit 2 ;;
esac
case "$EXPECTED_DB_PORT" in
  *[!0-9]*|'') echo "Runner received an invalid expected database port." >&2; exit 2 ;;
esac
case "$EXPECTED_DB_PORT" in
  5432) ;;
  *) echo "Runner received an unexpected database port." >&2; exit 2 ;;
esac

if [ ! -f "$SECRET_FILE" ] || [ -L "$SECRET_FILE" ] || [ "$(wc -c < "$SECRET_FILE" | tr -d ' ')" != 65 ]; then
  echo "Runner secret file is missing or has an unexpected format." >&2
  exit 2
fi
IFS= read -r DB_PASSWORD < "$SECRET_FILE"
case "$DB_PASSWORD" in
  *[!a-f0-9]*|'') echo "Runner secret file has an unexpected format." >&2; exit 2 ;;
esac
if [ "${#DB_PASSWORD}" -ne 64 ]; then
  echo "Runner secret file has an unexpected format." >&2
  exit 2
fi

DB_CONNECTION="Host=pitcrm-test-db;Port=$EXPECTED_DB_PORT;Database=pitcrm_test;Username=pitcrm_test;Password=$DB_PASSWORD;Pooling=false;Timeout=5;Command Timeout=30;SSL Mode=Disable"
unset DB_PASSWORD
export NUGET_PACKAGES=/nuget-cache
export DOTNET_CLI_HOME=/tmp/dotnet-home
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export HOME=/tmp
unset ConnectionStrings__DefaultConnection connectionstrings__defaultconnection

mkdir -p "$PROBE_DIR" "$BACKEND_DIR" "$DOTNET_CLI_HOME"
cp -a "$SOURCE_DIR"/. "$BACKEND_DIR"/
find "$BACKEND_DIR" -type d \( -name bin -o -name obj \) -prune -exec rm -rf -- {} +
cp -a /opt/ac-t02/connection-probe/. "$PROBE_DIR"/

if [ "$(dotnet --version)" != 10.0.302 ]; then
  echo "Runner SDK version does not match the approved version." >&2
  exit 2
fi

if ! dotnet restore "$PROBE_DIR/ConnectionProbe.csproj" \
  --configfile /opt/ac-t02/NuGet.Config --verbosity quiet >/dev/null 2>&1; then
  echo "Offline restore for the DB identity probe failed." >&2
  exit 1
fi
if ! dotnet build "$PROBE_DIR/ConnectionProbe.csproj" --no-restore \
  --configuration Release --verbosity quiet >/dev/null 2>&1; then
  echo "Build for the DB identity probe failed." >&2
  exit 1
fi
PITCRM_TEST_CONNECTION="$DB_CONNECTION" PITCRM_CONNECTION_STRING="$DB_CONNECTION" \
  dotnet "$PROBE_DIR/bin/Release/net10.0/ConnectionProbe.dll" \
  pitcrm-test-db "$EXPECTED_DB_ADDRESS" "$EXPECTED_DB_PORT"

if ! dotnet restore "$BACKEND_DIR/PitCrm.sln" \
  --configfile /opt/ac-t02/NuGet.Config --verbosity quiet >/dev/null 2>&1; then
  echo "Offline restore for the backend solution failed." >&2
  exit 1
fi

TEST_LOG="$(mktemp /tmp/ac-t02-test.XXXXXX)"
set +e
PITCRM_TEST_CONNECTION="$DB_CONNECTION" PITCRM_CONNECTION_STRING="$DB_CONNECTION" \
  dotnet test "$BACKEND_DIR/PitCrm.sln" --no-restore --configuration Release \
  --filter 'FullyQualifiedName~PitCrm.Api.Tests.CustomersApiTests|FullyQualifiedName~PitCrm.Api.Tests.CommercialTrackingMigrationTests' \
  --logger 'console;verbosity=normal' --verbosity quiet >"$TEST_LOG" 2>&1
TEST_EXIT=$?
set -e
if [ "$TEST_EXIT" -eq 0 ]; then
  grep -E '^(Passed|Failed)!|^Test Run ' "$TEST_LOG" || true
else
  echo "Selected integration test diagnostics (known credentials and connection strings redacted):" >&2
  SANITIZED_LOG="$(mktemp /tmp/ac-t02-sanitized.XXXXXX)"
  if dotnet "$PROBE_DIR/bin/Release/net10.0/ConnectionProbe.dll" sanitize-test-log \
      "$TEST_LOG" "$SECRET_FILE" >"$SANITIZED_LOG"; then
    cat "$SANITIZED_LOG" >&2
  else
    echo "Diagnostic sanitization failed; test details were withheld." >&2
  fi
  rm -f -- "$SANITIZED_LOG"
fi
rm -f -- "$TEST_LOG"
unset DB_CONNECTION
if [ "$TEST_EXIT" -ne 0 ]; then
  echo "Selected integration tests failed; see the sanitized diagnostics above." >&2
fi
exit "$TEST_EXIT"
