# 0007 Separate Playback Engine From Rendering

## Status

Accepted

## Context

Before this refactor, `Scene` still mixed several responsibilities:

- queue ownership
- current-scene lifecycle
- random and test-mode scheduling triggers
- transition-aware drawing
- overlays such as clock and snow

That meant the render surface, playback state, and control flow were still tightly coupled even after the catalog and data refactors.

## Decision

We split the runtime into explicit playback and rendering pieces:

- `ScenePlaybackEngine` owns queued scenes and the active scene lifecycle
- `SceneControlService` now acts as the playback controller, owning mode and scheduling cadence
- `SceneRenderer` owns framebuffer composition, transitions, clock overlay, and snow

`Program` now drives the app through those seams directly instead of routing everything through the old `Scene` orchestration object.

## Consequences

- Playback state is now easier to inspect and expose through the web/API layer.
- Rendering concerns are isolated from queueing and scheduling rules.
- The runtime is closer to the architecture proposal's `scene engine` plus `renderer/compositor` split.
- The `Scene` type remains as a small compatibility wrapper, but it is no longer the primary runtime orchestrator.
