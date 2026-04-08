# ADR 0013: Separate logical scene canvas from physical matrix presentation

## Status

Accepted

## Context

ADR 0012 deliberately centralized the app around a shared `64x32` matrix size, and the current scene catalog is authored around that canvas.

The hardware target now needs to support two landscape panels:

- `64x32`
- `128x64`

For the initial migration, we want the new panel working on the Pi without redesigning scenes yet. That means preserving the existing scene composition while allowing the output stack and preview surfaces to target the larger physical display.

## Decision

1. **Keep `MatrixConstants` as the logical scene canvas.**
   Scenes, overlays, transitions, and tests continue to render against a fixed `64x32` framebuffer.

2. **Introduce explicit physical matrix profiles.**
   Runtime configuration supports exactly two approved landscape sizes:
   - `64x32`
   - `128x64`

   The profile is selected with `ADVENT_MATRIX_SIZE` or `--matrix-size=...`.

3. **Add a presentation layer between rendering and output.**
   `SceneRenderer` produces the logical frame. `MatrixFramePresenter` converts that frame into the physical output frame:
   - `64x32` profile: clone the logical frame directly
   - `128x64` profile: apply exact `2x2` nearest-neighbour scaling

4. **Make preview surfaces show the physical output.**
   `/api/frame` and the browser preview reflect the presented frame, not the raw logical framebuffer, so simulator, web preview, and hardware all show the same output shape.

## Consequences

- Existing scenes remain unchanged and continue to behave as before on `64x32`.
- The new `128x64` panel works immediately, but it initially shows a scaled version of the `64x32` scene catalog rather than native high-resolution compositions.
- A later scene-retune pass can build on this architecture by teaching selected scenes to render natively for `128x64` without reworking the deployment and output plumbing again.
