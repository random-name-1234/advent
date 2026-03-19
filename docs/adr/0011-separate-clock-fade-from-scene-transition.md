# 0011 Separate Clock Fade From Scene Transition

## Status

Accepted

## Context

Allowing scenes to choose their transition style solved the worst mismatch between the compositor and scenes with bespoke choreography.

That still left a second concern coupled to the wrong thing: the clock overlay. Some scenes, such as the cat, should keep their own scene motion, but still want the clock to fade out before the scene begins and fade back in after it ends.

Treating that as a full scene crossfade made the scene animation and the overlay transition fight each other.

## Decision

We now treat clock fading as a separate concern from scene-to-scene visual transition.

The catalog supports a `CutWithClockFade` transition style, which:

- keeps the scene itself as a cut-style scene
- fades the clock out before the scene activates
- fades the clock back in after the scene finishes

The cat scene now uses this mode.

## Consequences

- Scenes can keep bespoke motion without losing graceful clock transitions.
- The compositor no longer has to pretend every transition is a scene crossfade.
- Future scene registrations have a clearer path for combining scene motion and overlay behavior without custom hacks in individual scenes.
