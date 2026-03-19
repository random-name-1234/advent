# 0005 Only Schedule Ready Data Scenes

## Status

Accepted

## Context

After moving weather and rail fetching into background snapshot stores, the app still had two competing ideas of scene availability:

- the scheduler treated configured data-backed scenes as selectable even when no fresh snapshot existed
- manual scene requests could still target scenes that would immediately skip during activation

That left playback and control flow inconsistent. The web UI could list a scene, the queue could accept it, and the renderer could still drop it because data was not ready.

## Decision

We now treat runtime readiness as part of scene availability:

- `SceneSelector.AvailableSceneNames` is a live view of scenes that are ready now
- normal random selection only chooses from ready cycle scenes
- test-mode cycling skips unavailable data-backed scenes when creating the next scene
- named scene requests fail fast when a known scene is not ready yet

`AllSceneNames` and `KnownSceneNames` still describe the broader catalog, so the UI can distinguish between:

- scenes that exist
- scenes that are currently playable

## Consequences

- Weather and rail no longer get queued and silently dropped when their snapshots are missing.
- The web/API control plane now matches the scheduler more closely.
- Availability is now dynamic, so tests and callers must not assume the available scene list is a fixed startup snapshot.
- This is still an intermediate design: readiness is evaluated in the selector rather than a dedicated scene catalog or scheduler model.
