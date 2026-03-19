# 0004 Refresh Scene Data In Background

## Status

Accepted

## Context

Even after extracting provider/config/DTO code into `Data`, `WeatherScene` and `RailBoardScene` still owned:

- fetch timing
- retry timing
- readiness waiting
- scene-local caches

That kept data acquisition entangled with playback and transition behavior. It also meant a scene activation could still trigger work instead of simply deciding whether ready state existed.

## Decision

We now refresh weather and rail data in shared background snapshot stores:

- `WeatherSnapshotStore`
- `RailSnapshotStore`

The stores are started by the app host in `Program`, and the data-backed scenes consume snapshot sources instead of managing their own fetch tasks and caches.

In the current transitional design:

- scenes capture a snapshot if one is ready
- scenes skip immediately if no ready snapshot exists
- stores own refresh cadence and freshness windows

## Consequences

- Scene activation no longer starts HTTP work.
- Weather and rail rendering now depend on stable snapshots captured at activation time.
- The app host is more responsible for runtime wiring, which is a step toward the fuller host/engine/data split in the architecture proposal.
- Scene scheduling is still not fully data-aware, so unavailable data-backed scenes may still be selected and then skip. That is an acceptable intermediate state and a target for the next refactor slice.
