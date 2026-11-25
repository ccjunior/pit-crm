#!/usr/bin/env bash
set -Eeuo pipefail

if (($# != 1)); then
  echo "usage: $0 <ConnectionProbe.dll>" >&2
  exit 2
fi
SANITIZER_DLL="$1"
[[ -f "$SANITIZER_DLL" && ! -L "$SANITIZER_DLL" ]] || { echo 'sanitizer assembly is missing or irregular' >&2; exit 2; }

TMP_DIR="$(mktemp -d)"
trap 'rm -rf -- "$TMP_DIR"' EXIT
chmod 700 "$TMP_DIR"
DB_SECRET="$(printf '%064d' 0)"
printf '%s\n' "$DB_SECRET" > "$TMP_DIR/secret"
chmod 600 "$TMP_DIR/secret"
cat > "$TMP_DIR/test.log" <<'EOF'
Failed PitCrm.Api.Tests.CommercialTrackingTests.AllocatesNumber [41 ms]
  Error Message:
   System.InvalidOperationException: Expected one commercial record, found zero.
  Stack Trace:
     at PitCrm.Api.Tests.CommercialTrackingTests.AllocatesNumber() in /work/backend/tests/CommercialTrackingTests.cs:line 123
ConnectionStrings__DefaultConnection=Host=db;Username=test;Password=syntheticConnectionSecret
PITCRM_CONNECTION_STRING=Host=db;Database=p;Password=syntheticConnectionSecret
Connection diagnostic: Host=db.internal;Database=pitcrm_test;Port=5432
{"client_secret":"syntheticJsonSecret"}
Authorization: Bearer syntheticBearerToken
AccountKey=syntheticAccountKey
access_token: syntheticAccessToken
https://test:syntheticUrlPassword@db.example.invalid/path
Password=syntheticConnectionSecret
Unlabelled secret: 0000000000000000000000000000000000000000000000000000000000000000
Unlabelled long key: ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff
EOF

dotnet "$SANITIZER_DLL" sanitize-test-log "$TMP_DIR/test.log" "$TMP_DIR/secret" > "$TMP_DIR/sanitized.log"
grep -Fq 'Failed PitCrm.Api.Tests.CommercialTrackingTests.AllocatesNumber' "$TMP_DIR/sanitized.log"
grep -Fq 'System.InvalidOperationException: Expected one commercial record, found zero.' "$TMP_DIR/sanitized.log"
grep -Fq 'CommercialTrackingTests.cs:line 123' "$TMP_DIR/sanitized.log"
if grep -Eq 'syntheticConnectionSecret|syntheticJsonSecret|syntheticBearerToken|syntheticUrlPassword|0000000000000000000000000000000000000000000000000000000000000000|ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff' "$TMP_DIR/sanitized.log"; then
  echo 'sanitized log contains a synthetic credential' >&2
  exit 1
fi
if grep -Eq 'syntheticAccountKey|syntheticAccessToken' "$TMP_DIR/sanitized.log"; then
  echo 'sanitized log contains an account key or access token' >&2
  exit 1
fi
[[ "$(grep -Fc '[REDACTED CREDENTIAL OR CONNECTION STRING LINE]' "$TMP_DIR/sanitized.log")" -ge 9 ]]
grep -Fq '[REDACTED]' "$TMP_DIR/sanitized.log"

printf 'invalid-secret\n' > "$TMP_DIR/invalid-secret"
if dotnet "$SANITIZER_DLL" sanitize-test-log "$TMP_DIR/test.log" "$TMP_DIR/invalid-secret" > "$TMP_DIR/invalid-output" 2> "$TMP_DIR/invalid-error"; then
  echo 'sanitizer accepted an invalid secret file' >&2
  exit 1
fi
[[ ! -s "$TMP_DIR/invalid-output" ]]

echo 'sanitizer checks passed: failure name, exception, and stack retained; common credentials redacted; invalid secret fails closed'
