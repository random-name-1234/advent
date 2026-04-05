# ADR 0013: Multi-resolution support via scaling output decorator

## Status

Accepted

## Context

The project targets a 64x32 HUB75 LED matrix. A new 128x64 panel is being introduced alongside the existing hardware. All 20+ scenes render at 64x32 using `MatrixConstants.Width` and `MatrixConstants.Height` (see ADR 0012). Rewriting every scene to support multiple resolutions simultaneously would be high-effort and error-prone.

The hardware driver layer (`IMatrixOutput`) already receives the frame as an `Image<Rgba32>` and writes it to the panel. The Pi 5 driver validates that frame dimensions match the configured geometry.

## Decision

1. **Distinguish virtual canvas from hardware resolution.** `MatrixConstants` (64x32) continues to define the scene rendering resolution -- the "virtual canvas." Hardware resolution is configured separately via `ADVENT_MATRIX_WIDTH` / `ADVENT_MATRIX_HEIGHT` environment variables (or `--matrix-width=` / `--matrix-height=` CLI args), defaulting to 64x32.

2. **Introduce `ScalingMatrixOutput`** -- a decorator implementing `IMatrixOutput` that wraps the real hardware output. When hardware dimensions differ from the virtual canvas, it performs a nearest-neighbour upscale per frame before passing to the inner driver. When they match, it passes through with no overhead.

3. **Fix stale hardcoded literals.** Several files (`SceneRenderer`, `FadeInOutScene`, `SnowMachine`) used literal `64`/`32` instead of `MatrixConstants`. These are corrected to use the constants or image dimensions.

4. **Phase 2 path.** Individual scenes can later opt into native high-res rendering. The scaling decorator already checks frame dimensions and passes through when they match hardware size, so a scene that renders directly at 128x64 would bypass scaling automatically.

## Consequences

- All existing scenes work on 128x64 hardware via 2x upscale with zero scene code changes.
- The 64x32 panel continues to work unchanged (scaling is a no-op when resolutions match).
- Per-frame upscale cost for 64x32 to 128x64 (8,192 source pixels) is negligible on Pi 5.
- For 64-row panels, users must also set `ADVENT_PI5_ADDR_LINES=5` (default is 4 for 32-row panels).
- Supersedes nothing; extends ADR 0012's single-point resolution intent to support heterogeneous hardware.
