#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd -- "${script_dir}/.." && pwd)"

if [[ "$(uname -s)" != "Linux" ]]; then
  echo "deploy-on-pi.sh must run on Linux (the Pi self-hosted runner)." >&2
  exit 1
fi

for cmd in dotnet git tar sudo systemctl; do
  if ! command -v "${cmd}" >/dev/null 2>&1; then
    echo "Missing required command: ${cmd}" >&2
    exit 1
  fi
done

if ! sudo -n systemctl --version >/dev/null 2>&1; then
  echo "Passwordless sudo for systemctl is required for the runner user." >&2
  exit 1
fi

runtime="${DOTNET_RUNTIME:-linux-arm64}"
configuration="${DOTNET_CONFIGURATION:-Release}"
service_unit="${ADVENT_SERVICE_UNIT:-advent.service}"
led_args="${LED_ARGS:---led-gpio-mapping=adafruit-hat-pwm --led-slowdown-gpio=4}"
env_file="${ADVENT_ENV_FILE:-/etc/advent/advent.env}"
service_user="${ADVENT_SERVICE_USER:-$USER}"
service_group="${ADVENT_SERVICE_GROUP:-$(id -gn "${service_user}")}"
service_supplementary_groups="${ADVENT_SERVICE_SUPPLEMENTARY_GROUPS:-$(id -Gn "${service_user}" | tr ' ' '\n' | grep -vx "${service_group}" | paste -sd' ' -)}"
supplementary_groups_line=""
if [[ -n "${service_supplementary_groups}" ]]; then
  supplementary_groups_line="SupplementaryGroups=${service_supplementary_groups}"
fi

next_src_dir="${ADVENT_NEXT_SRC_DIR:-$HOME/advent-next-src}"
stable_src_dir="${ADVENT_STABLE_SRC_DIR:-$HOME/advent-src}"
next_app_dir="${ADVENT_NEXT_APP_DIR:-$HOME/advent-next-app}"
stable_app_dir="${ADVENT_STABLE_APP_DIR:-$HOME/advent-app}"

if [[ "${service_unit}" != *.service ]]; then
  service_unit="${service_unit}.service"
fi

echo "Publishing app (${configuration}, ${runtime}) ..."
dotnet publish "${project_root}/advent.csproj" -c "${configuration}" -r "${runtime}" --self-contained false

publish_dir="${project_root}/bin/${configuration}/net10.0/${runtime}/publish"
if [[ ! -d "${publish_dir}" ]]; then
  echo "Publish output not found at ${publish_dir}" >&2
  exit 1
fi

rm -rf "${next_src_dir}" "${next_app_dir}"
mkdir -p "${next_src_dir}" "${next_app_dir}"

echo "Staging source snapshot to ${next_src_dir} ..."
git -C "${project_root}" archive HEAD | tar -x -C "${next_src_dir}"

echo "Staging published app to ${next_app_dir} ..."
cp -a "${publish_dir}/." "${next_app_dir}/"

ts="$(date +%Y%m%d-%H%M%S)"

if [[ -d "${stable_app_dir}" ]]; then
  mv "${stable_app_dir}" "${stable_app_dir}.backup-${ts}"
fi
mv "${next_app_dir}" "${stable_app_dir}"

if [[ -d "${stable_src_dir}" ]]; then
  mv "${stable_src_dir}" "${stable_src_dir}.backup-${ts}"
fi
mv "${next_src_dir}" "${stable_src_dir}"

sudo tee "/etc/systemd/system/${service_unit}" >/dev/null <<UNIT
[Unit]
Description=Advent LED Matrix
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=${service_user}
Group=${service_group}
${supplementary_groups_line}
EnvironmentFile=-${env_file}
WorkingDirectory=${stable_app_dir}
ExecStart=${stable_app_dir}/advent ${led_args}
Restart=always
RestartSec=2
KillSignal=SIGINT

[Install]
WantedBy=multi-user.target
UNIT

sudo systemctl daemon-reload
sudo systemctl enable "${service_unit}"
sudo systemctl restart "${service_unit}"
sudo systemctl status "${service_unit}" --no-pager -l

echo "Deploy complete."
