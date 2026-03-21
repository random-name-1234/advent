# ADR 0012: Shared matrix constants and new procedural scenes

## Status

Accepted

## Context

Every scene independently declared `const int Width = 64` and `const int Height = 32`, duplicated across 16+ files. This created maintenance risk if the matrix resolution ever changed and added noise to each scene file.

Separately, the scene catalog had strong procedural and data-driven coverage but lacked flocking/swarm simulations, classic game simulations beyond the existing arcade pair, and ambient sky/nature scenes.

## Decision

1. **Introduce `MatrixConstants`** — a single static class exposing `Width` and `Height`. Scenes reference these via `using static advent.MatrixConstants;`, keeping call-site code unchanged while eliminating per-file duplication.

2. **Add three new scenes:**
   - **BoidsScene** — classic flocking simulation (separation, alignment, cohesion) with 35 boids, teal/cyan palette, toroidal wrapping, trail pixels.
   - **TetrisScene** — auto-playing Tetris with 7-bag randomiser, AI placement using weighted heuristics (complete lines, height, holes, bumpiness), line-clear flash animation, progressive speed increase.
   - **SunriseSunsetScene** — animated day/night cycle through dawn, sunrise, day, sunset, dusk. Parabolic sun arc, fading stars, gradient sky with 16 palette keyframes, rolling hill silhouette.

All three follow the established `ISpecialScene` lifecycle pattern and are registered in `BuiltinSceneModule`.

## Consequences

- Removing per-scene Width/Height reduces boilerplate by ~32 lines across the codebase and makes resolution changes a single-point edit.
- Three new scenes broaden the visual variety without introducing new dependencies or architectural patterns.
- Existing tests continue to pass; new scenes are covered by the parameterised `ProceduralScenesTests`.
