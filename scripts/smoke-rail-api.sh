#!/usr/bin/env bash
set -euo pipefail

base_url="${ADVENT_RAIL_LDB_BASE_URL:-https://api1.raildata.org.uk/1010-live-arrival-and-departure-boards---staff-version1_0/LDBSVWS}"
crs="${1:-${ADVENT_RAIL_ORIGIN_CRS:-${ADVENT_RAIL_CAMBRIDGE_CRS:-CBG}}}"
board_time="${2:-$(TZ=Europe/London date +%Y%m%dT%H%M%S)}"
upper_crs="$(printf '%s' "${crs}" | tr '[:lower:]' '[:upper:]')"
temp_body="$(mktemp)"
trap 'rm -f "${temp_body}"' EXIT

header_name="${ADVENT_RAIL_LDB_AUTH_HEADER_NAME:-}"
header_value="${ADVENT_RAIL_LDB_AUTH_HEADER_VALUE:-}"

if [[ -z "${header_name}" && -n "${ADVENT_RAIL_LDB_CONSUMER_KEY:-}" ]]; then
  header_name="x-apikey"
  header_value="${ADVENT_RAIL_LDB_CONSUMER_KEY}"
fi

url="${base_url%/}/api/20220120/GetArrDepBoardWithDetails/${upper_crs}/${board_time}?numRows=5&timeWindow=90&services=P"

echo "Requesting ${url}" >&2

run_curl() {
  local -a curl_args=("$@")
  local status
  status="$(
    curl -sS \
      -o "${temp_body}" \
      -w '%{http_code}' \
      "${curl_args[@]}"
  )"

  echo "HTTP ${status}" >&2
  cat "${temp_body}"

  if [[ "${status}" != 2* ]]; then
    echo >&2
    echo "Rail API request failed with HTTP ${status}." >&2
    exit 1
  fi
}

if [[ -n "${header_name}" && -n "${header_value}" ]]; then
  run_curl \
    -H "${header_name}: ${header_value}" \
    -H "Accept: application/json" \
    "${url}"
  exit 0
fi

if [[ -n "${ADVENT_RAIL_LDB_USERNAME:-}" && -n "${ADVENT_RAIL_LDB_PASSWORD:-}" ]]; then
  run_curl \
    -u "${ADVENT_RAIL_LDB_USERNAME}:${ADVENT_RAIL_LDB_PASSWORD}" \
    -H "Accept: application/json" \
    "${url}"
  exit 0
fi

echo "Missing rail auth environment variables." >&2
echo "Set ADVENT_RAIL_LDB_CONSUMER_KEY or ADVENT_RAIL_LDB_AUTH_HEADER_NAME/ADVENT_RAIL_LDB_AUTH_HEADER_VALUE." >&2
exit 1
