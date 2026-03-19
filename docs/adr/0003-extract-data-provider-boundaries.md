# 0003 Extract Data Provider Boundaries

## Status

Accepted

## Context

`WeatherScene` and `RailBoardScene` had each grown to include:

- environment/config parsing
- HTTP client setup
- request construction
- authentication details
- wire-format DTOs
- response parsing
- rendering

That made the scenes harder to understand, harder to test as renderers, and harder to evolve toward the architecture described in [`docs/architecture-proposal.md`](../architecture-proposal.md).

## Decision

As the first refactor slice, we extract provider-facing concerns into a `Data` area:

- weather options, snapshots, and the Open-Meteo client now live under `Data/Weather`
- rail options, station-name helpers, Darwin DTOs, and the Darwin departure-board client now live under `Data/Rail`

For now, scene-specific presentation models and rendering logic remain in the scene classes. This is an intentional midpoint rather than a final architecture.

## Consequences

- Scenes are now less coupled to HTTP and environment details.
- Provider/auth/DTO changes can be made with less risk to rendering code.
- Tests can start targeting the new boundary directly instead of relying on private nested data types.
- The next refactor slice can move from "scene fetches data" to background refresh services and snapshot stores without first untangling wire-format concerns again.
