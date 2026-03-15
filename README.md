# advent

`advent` is a .NET LED matrix scene runner for Raspberry Pi (64x32 panel setup in the current defaults).

It renders:
- a clock overlay
- snow/rainbow effects by season
- special scenes (static + animated + procedural), including a full-cycle `--test-mode`
- live weather scene powered by Open-Meteo (current conditions + 3-day outlook)
- optional terminal simulator mode for development on macOS/Linux (`--simulator`)

## Matrix Library / Bindings Source

This project uses local C# bindings in [`MatrixApi/`](MatrixApi/) to call the native RGB matrix library (`librgbmatrix.so`).

Upstream source for the underlying matrix library:
- https://github.com/hzeller/rpi-rgb-led-matrix

## Requirements

- Raspberry Pi with supported RGB matrix hardware
- .NET runtime compatible with this project target (`net6.0`)
- Native `librgbmatrix.so` available next to the app (included in this repo)

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
- `--test-mode` ignores random seasonal selection and continuously cycles the full scene catalog.
- `--simulator` renders a live ANSI/terminal preview of the 64x32 output, useful on macOS while developing.
- Hardware/driver flag details (`--led-*`) are defined by `rpi-rgb-led-matrix`; refer to upstream docs for full options.
- Weather scene configuration is optional via env vars:
  - `ADVENT_WEATHER_LATITUDE` (default `52.2053`, Cambridge UK)
  - `ADVENT_WEATHER_LONGITUDE` (default `0.1218`, Cambridge UK)
