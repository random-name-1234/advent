# advent

`advent` is a .NET LED matrix scene runner for Raspberry Pi (64x32 panel setup in the current defaults).

It renders:

- a clock overlay
- snow/rainbow effects by season
- special scenes (static + animated + procedural), including a full-cycle `--test-mode`
- live weather scene powered by Open-Meteo (current conditions + 3-day outlook)
- optional terminal simulator mode for development on macOS/Linux (`--simulator`)

## Matrix Library / Bindings Source

This project uses local C# bindings in [`MatrixApi/`](MatrixApi/) to call the native RGB matrix library (
`librgbmatrix.so`).

Upstream source for the underlying matrix library:

- https://github.com/hzeller/rpi-rgb-led-matrix

## Requirements

- Raspberry Pi with supported RGB matrix hardware
- .NET runtime compatible with this project target (`net10.0`)
- Native `librgbmatrix.so` available next to the app

## Rebuild Native Library (Upstream Latest)

Run this on the Raspberry Pi to rebuild from upstream and overwrite the vendored
`librgbmatrix.so` in this repo:

```bash
./scripts/rebuild-librgbmatrix.sh
```

Optional:

- `RGBMATRIX_REF=<branch-or-tag>` to build a specific ref
- `RGBMATRIX_REPO_URL=<git-url>` to use a fork

## Validate Assets

Run this locally (and in CI) to verify required scene assets and `advent-images/manifest.json` references:

```bash
./scripts/validate-assets.sh
```

## GitHub Actions Deploy (Pi Native)

Deployment is now handled by GitHub Actions running on a self-hosted runner on the Pi.

Included workflows:

- `.github/workflows/ci.yml`: build + test on PRs and pushes to `main`
- `.github/workflows/deploy-pi.yml`: deploy by manual dispatch only

One-time Pi setup:

1. Register the Pi as a self-hosted runner for this repo with labels:
   `self-hosted`, `linux`, `arm64`, `advent`.
2. Ensure runner user can run passwordless sudo for `systemctl` and writing unit files.
3. Ensure `dotnet` and `git` are installed on the Pi.
4. In GitHub, create environment `advent-pi` and require reviewer approval before deploy.
5. In GitHub Actions settings, keep workflow permissions at `Read repository contents`.

Public repo hardening notes:

- Avoid automatic deploy-on-push to self-hosted runners in public repos.
- Never store Pi SSH password in Actions secrets; this deploy path runs directly on the Pi runner and does not need SSH.
- Protect `main` with PR reviews and required status checks (`CI`).
- Keep `deploy-pi.yml` restricted to `workflow_dispatch` + protected environment approval.

Deploy behavior (from `scripts/deploy-on-pi.sh`):

- `dotnet publish -c Release -r linux-arm64` on Pi
- stage source to `~/advent-next-src` using `git archive`
- stage publish output to `~/advent-next-app`
- promote to `~/advent-src` and `~/advent-app` with timestamped backups
- write `/etc/systemd/system/advent.service`
- `systemctl daemon-reload`, `enable`, and `restart`

Optional repo variables for deploy customization:

- `ADVENT_LED_ARGS`
- `ADVENT_SERVICE_UNIT`
- `ADVENT_STABLE_APP_DIR`
- `ADVENT_STABLE_SRC_DIR`
- `ADVENT_NEXT_APP_DIR`
- `ADVENT_NEXT_SRC_DIR`

## LAN Control Web UI

The app now serves a simple control page from the Pi on:

- `http://<pi-hostname-or-ip>:8080`

Supported API endpoints:

- `GET /api/scenes`
- `GET /api/status`
- `POST /api/scene/play` with `{ "name": "Fireworks" }`
- `POST /api/scene/next`
- `POST /api/message/show` with `{ "text": "Dinner in 5", "durationSeconds": 8 }`
- `POST /api/mode` with `{ "mode": "normal" | "test" }`
- `POST /api/queue/clear`

Web control environment variables:

- `ADVENT_WEB_ENABLED` (`true` by default)
- `ADVENT_WEB_BIND` (`0.0.0.0` by default)
- `ADVENT_WEB_PORT` (`8080` by default)
- `ADVENT_WEB_TOKEN` (optional; if set, API requires `X-Advent-Token` header)

## Run

Important: pass app args after `--` so they are forwarded to the program/native matrix options.

Normal mode:

```bash
dotnet run -c Release --no-launch-profile -- --led-slowdown-gpio=4 --led-gpio-mapping=adafruit-hat
```

Test mode (cycles through all scenes in order):

```bash
dotnet run -c Release --no-launch-profile -- --led-slowdown-gpio=4 --led-gpio-mapping=adafruit-hat --test-mode
```

Simulator mode (no Pi hardware required):

```bash
dotnet run -c Release --no-launch-profile -- --simulator
```

Simulator + test mode:

```bash
dotnet run -c Release --no-launch-profile -- --simulator --test-mode
```

## Notes

- Scene selection is seasonal in normal mode (December gets the Christmas scene set).
- Image scenes are loaded automatically from `advent-images/`:
    - files in `advent-images/` are available all year
    - files in `advent-images/<month>/` (for example `advent-images/12/`) are only loaded in that month
    - `.gif` files use animated playback scenes
    - wide images use scrolling banner scenes
    - other static images use fade-in/out scenes
- Optional overrides are supported in `advent-images/manifest.json`:
    - `file`: relative image path (required)
    - `name`: scene name override
    - `type`: `auto`, `animated`/`gif`, `static`, or `scroll`
    - `months`: month whitelist (1-12)
    - `durationSeconds`: custom scene duration (must be `> 0`)
- Core scene assets (cat/error/santa/space-invaders sprites) are stored under `assets/`.
- `--test-mode` ignores random seasonal selection and continuously cycles the full scene catalog.
- `--simulator` renders a live ANSI/terminal preview of the 64x32 output, useful on macOS while developing.
- Hardware/driver flag details (`--led-*`) are defined by `rpi-rgb-led-matrix`; refer to upstream docs for full options.
- Weather scene configuration is optional via env vars:
    - `ADVENT_WEATHER_LATITUDE` (default `52.2053`, Cambridge UK)
    - `ADVENT_WEATHER_LONGITUDE` (default `0.1218`, Cambridge UK)
