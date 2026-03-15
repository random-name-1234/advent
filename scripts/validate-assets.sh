#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"

cd "${project_root}"
dotnet test advent.Tests/advent.Tests.csproj -c Release --filter "FullyQualifiedName~AssetValidationTests" "$@"
