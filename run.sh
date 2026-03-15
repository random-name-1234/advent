#!/usr/bin/env bash
set -euo pipefail

export DOTNET_ROOT="/opt/dotnet"
cd /share/advent/bin/Release/net10.0
exec sudo -E ./advent "$@"
