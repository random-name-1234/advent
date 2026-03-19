# 0006 Separate Scene Catalog From Selection

## Status

Accepted

## Context

`SceneSelector` had grown into two different components at once:

- a catalog builder that knew what scenes existed, how to load image scenes, and how to decide readiness
- a selector that knew how to pick scenes randomly, cycle through them, and resolve named requests

That made one class responsible for both scene discovery and playback policy. It also made the architecture proposal's `ISceneCatalog` direction hard to approach incrementally.

## Decision

We introduced a dedicated `ISceneCatalog` with a concrete `SceneCatalog` implementation.

`SceneCatalog` now owns:

- base scene registration
- seasonal scene inclusion
- static/animated image scene discovery
- readiness checks for data-backed scenes
- scene lookup by name
- the known / all / available scene lists

`SceneSelector` now acts as a small scheduler over catalog entries:

- random selection
- cycle traversal
- skipping unavailable cycle entries

## Consequences

- Scene discovery and selection are now separate seams with smaller responsibilities.
- The current `SceneSelector` API stays stable while the internals move toward the proposed `ISceneCatalog` / `ISceneScheduler` split.
- Future work on richer scene metadata or control-plane status can build on the catalog without reopening the scheduler logic.
- Image scene loading still lives inside the concrete catalog implementation, so there is more extraction available later if we want dedicated catalog sources.
