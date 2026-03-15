#!/usr/bin/env bash
set -euo pipefail

repo_url="${RGBMATRIX_REPO_URL:-https://github.com/hzeller/rpi-rgb-led-matrix.git}"
repo_ref="${RGBMATRIX_REF:-master}"

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "This script must be run on Linux (ideally directly on the Raspberry Pi)." >&2
  exit 1
fi

for cmd in git make; do
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    echo "Missing required command: ${cmd}" >&2
    exit 1
  fi
done

tmp_dir="$(mktemp -d /tmp/rpi-rgb-led-matrix-XXXXXX)"
cleanup() {
  rm -rf "${tmp_dir}"
}
trap cleanup EXIT

echo "Cloning ${repo_url} (${repo_ref}) ..."
git clone --depth 1 --branch "${repo_ref}" "${repo_url}" "${tmp_dir}/src"

echo "Building native library ..."
make -C "${tmp_dir}/src/lib"

if [[ ! -f "${tmp_dir}/src/lib/librgbmatrix.so.1" ]]; then
  echo "Build finished, but librgbmatrix.so.1 was not produced." >&2
  exit 1
fi

cp "${tmp_dir}/src/lib/librgbmatrix.so.1" "${project_root}/librgbmatrix.so"
echo "Updated ${project_root}/librgbmatrix.so"
