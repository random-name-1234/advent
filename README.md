# advent

`advent` is a .NET LED matrix scene runner for Raspberry Pi (64x32 panel setup in the current defaults).

It renders:

- a clock overlay
- snow/rainbow effects by season
- special scenes (static + animated + procedural), including a full-cycle `--test-mode`
- live weather scene powered by Open-Meteo (three full-screen forecast cards)
- optional UK rail board scene for Cambridge and King's Cross
- optional terminal simulator mode for development on macOS/Linux (`--simulator`)

## Matrix Library / Bindings Source

This project uses:

- local C# bindings in [`MatrixApi/`](MatrixApi/) for the Pi 4 `rpi-rgb-led-matrix` path
- the published [`Pi5MatrixSharp`](https://github.com/random-name-1234/Pi5MatrixSharp) NuGet package for the Pi 5 path

Upstream source for the underlying matrix library:

- https://github.com/hzeller/rpi-rgb-led-matrix
- https://github.com/random-name-1234/Pi5MatrixSharp

## Requirements

- Raspberry Pi with supported RGB matrix hardware
- .NET runtime compatible with this project target (`net10.0`)
- Native `librgbmatrix.so` available next to the app when using the Pi 4 backend
- `Pi5MatrixSharp` provides the bundled Pi 5 native runtime when using the Pi 5 backend

## Rebuild Native Library (Upstream Latest)

Run this on the Raspberry Pi to rebuild from upstream and overwrite the vendored
`librgbmatrix.so` in this repo:

```bash
./scripts/rebuild-librgbmatrix.sh
```

Optional:

- `RGBMATRIX_REF=<branch-or-tag>` to build a specific ref
- `RGBMATRIX_REPO_URL=<git-url>` to use a fork

## Pi 5 Binding (Adafruit PioMatter)

Pi 5 support now comes from the published [`Pi5MatrixSharp`](https://www.nuget.org/packages/Pi5MatrixSharp/)
package rather than a vendored local binding.

Current scope in `advent`:

- simple framebuffer + `show()` workflow
- Adafruit Matrix Bonnet / Active3 pinouts
- RGB565 / RGB888 / packed RGB888 framebuffers
- simple geometry constructor (`width`, `height`, address lines, serpentine, rotation, planes)
- backend selection in `advent` via `--backend=pi5` or `ADVENT_MATRIX_BACKEND=pi5`

Useful Pi 5 environment variables:

- `ADVENT_MATRIX_BACKEND=pi5`
- `ADVENT_PI5_PINOUT=AdafruitMatrixBonnet` or `Active3`
- `ADVENT_PI5_ADDR_LINES=4`
- `ADVENT_PI5_SERPENTINE=true`
- `ADVENT_PI5_ORIENTATION=Normal`
- `ADVENT_PI5_PLANES=10`
- `ADVENT_PI5_TEMPORAL_PLANES=2`

Current limitation: `advent` itself still renders a fixed `64x32` scene, so while the Pi 5 binding can support other geometries, this app currently assumes `64x32`.

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
6. Put machine-local secrets in `/etc/advent/advent.env` on the Pi; deploys do not overwrite this file.

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
- `ADVENT_ENV_FILE`
- `ADVENT_STABLE_APP_DIR`
- `ADVENT_STABLE_SRC_DIR`
- `ADVENT_NEXT_APP_DIR`
- `ADVENT_NEXT_SRC_DIR`

Recommended Pi secret file setup:

```bash
sudo install -d -m 700 -o root -g root /etc/advent
sudo sh -c 'umask 077; cat > /etc/advent/advent.env <<EOF
ADVENT_RAIL_LDB_CONSUMER_KEY=put-the-api-key-here
ADVENT_RAIL_LDB_CONSUMER_SECRET=optional-consumer-secret
ADVENT_RAIL_CAMBRIDGE_CRS=CBG
ADVENT_RAIL_KINGS_CROSS_CRS=KGX
EOF'
sudo chown root:root /etc/advent/advent.env
sudo chmod 600 /etc/advent/advent.env
```

The app will use `ADVENT_RAIL_LDB_CONSUMER_KEY` as the `x-apikey` header by default.

If you need to override that, you can set:

- `ADVENT_RAIL_LDB_AUTH_HEADER_NAME`
- `ADVENT_RAIL_LDB_AUTH_HEADER_VALUE`

If your Rail Data Marketplace subscription gives you a consumer key / consumer secret pair, put them in the same file as:

- `ADVENT_RAIL_LDB_CONSUMER_KEY`
- `ADVENT_RAIL_LDB_CONSUMER_SECRET`

The app also accepts the equivalent basic-auth names:

- `ADVENT_RAIL_LDB_USERNAME`
- `ADVENT_RAIL_LDB_PASSWORD`

## LAN Control Web UI

The app now serves a simple control page from the Pi on:

- `http://<pi-hostname-or-ip>:8080`

Supported API endpoints:

- `GET /api/scenes`
- `GET /api/status`
- `POST /api/scene/play` with `{ "name": "Fireworks" }`
- `POST /api/scene/next`
- `POST /api/message/show` with `{ "text": "Dinner in 5", "durationSeconds": 8 }` (`durationSeconds` max `20`)
- `POST /api/mode` with `{ "mode": "normal" | "test" }`
- `POST /api/queue/clear`

Web control environment variables:

- `ADVENT_WEB_ENABLED` (`true` by default)
- `ADVENT_WEB_BIND` (`0.0.0.0` by default)
- `ADVENT_WEB_PORT` (`8080` by default)
- `ADVENT_WEB_TOKEN` (optional; if set, API requires `X-Advent-Token` header)

## Run

Important: pass app args after `--`. Pi 4-specific `--led-*` flags are still forwarded to `rpi-rgb-led-matrix` when using the Pi 4 backend.

Pi 4 normal mode:

```bash
dotnet run -c Release --no-launch-profile -- --led-slowdown-gpio=4 --led-gpio-mapping=adafruit-hat
```

Pi 4 test mode (cycles through all scenes in order):

```bash
dotnet run -c Release --no-launch-profile -- --led-slowdown-gpio=4 --led-gpio-mapping=adafruit-hat --test-mode
```

Pi 5 mode:

```bash
ADVENT_MATRIX_BACKEND=pi5 \
ADVENT_PI5_PINOUT=AdafruitMatrixBonnet \
dotnet run -c Release --no-launch-profile -- --backend=pi5
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
    - `durationSeconds`: custom scene duration (must be `> 0`; runtime capped to `20` seconds)
- Core scene assets (cat/error/santa/space-invaders sprites) are stored under `assets/`.
- `--test-mode` ignores random seasonal selection and continuously cycles the full scene catalog.
- `--simulator` renders a live ANSI/terminal preview of the 64x32 output, useful on macOS while developing.
- `--backend=pi4|pi5|simulator` selects the output backend. `--simulator` remains a shorthand for the simulator backend.
- Hardware/driver flag details (`--led-*`) are defined by `rpi-rgb-led-matrix`; refer to upstream docs for full options.
- Random scene requests in normal mode are capped at two per rolling minute.
- Weather scene configuration is optional via env vars:
    - `ADVENT_WEATHER_LATITUDE` (default `52.2053`, Cambridge UK)
    - `ADVENT_WEATHER_LONGITUDE` (default `0.1218`, Cambridge UK)
- UK rail board scene is optional and only appears when credentials are configured:
    - `ADVENT_RAIL_ENABLED` (`true` by default)
    - `ADVENT_RAIL_LDB_BASE_URL` (default `https://api1.raildata.org.uk/1010-live-arrival-and-departure-boards---staff-version1_0/LDBSVWS`)
    - `ADVENT_RAIL_LDB_CONSUMER_KEY` (used as `x-apikey` by default)
    - `ADVENT_RAIL_LDB_CONSUMER_SECRET` (stored only if your subscription issues one)
    - `ADVENT_RAIL_LDB_AUTH_HEADER_NAME` and `ADVENT_RAIL_LDB_AUTH_HEADER_VALUE`
    - or `ADVENT_RAIL_LDB_USERNAME` and `ADVENT_RAIL_LDB_PASSWORD`
    - `ADVENT_RAIL_CAMBRIDGE_CRS` (default `CBG`)
    - `ADVENT_RAIL_KINGS_CROSS_CRS` (default `KGX`)
