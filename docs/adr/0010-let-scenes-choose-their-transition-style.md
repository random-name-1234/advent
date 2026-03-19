# 0010 Let Scenes Choose Their Transition Style

## Status

Accepted

## Context

The refactor made crossfade the default outer transition for catalog scenes, which improved consistency for most of the app.

That also exposed a mismatch for scenes that already contain their own entrance and exit choreography. The cat scene is the clearest example: wrapping it in a generic fade made the clock and scene movement interact in surprising ways, because the outer fade and the inner animation were both trying to define the viewer's sense of arrival and departure.

## Decision

Scene registrations now declare an explicit `SceneTransitionStyle`.

The catalog defaults scenes to `Crossfade`, but a scene can opt into `Cut` when it already owns its own transition behavior.

We use that immediately for the cat scene, which now:

- bypasses the generic outer fade wrapper
- keeps clock-hiding behavior stable for the duration of the scene
- relies on its own movement as the transition

## Consequences

- Transition behavior is now an explicit part of scene registration rather than an implicit global wrapper.
- Scenes with bespoke choreography can avoid fighting the compositor.
- The catalog has a clean extension point for future transition types without reopening every scene factory again.
