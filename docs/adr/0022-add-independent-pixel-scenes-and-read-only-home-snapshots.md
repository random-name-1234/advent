# 0022: Add independent pixel scenes and read-only home snapshots

## Status

Accepted, 2026-09-21. Builds on ADRs 0004, 0005, 0009, 0013 and 0019.

## Context

The active appliance remains a Pi 4 with a landscape 64x32 panel. Eight new
scenes are requested without another redesign of the existing collection.
The home dashboard already owns pet presence and Agile tariff interpretation;
its matrix projection does not yet contain individual pets or tariff slots.

## Decision

- Add a separate scene module: Night Train, Aquarium, Pixel City, Breakout,
  Weather Window, Moonlit Landscape, Two Cats and Agile Power.
- Render original pixel art on the existing 64x32 logical canvas. Keep existing
  scenes, hardware configuration and the nearest-neighbour presenter unchanged.
- New scenes are manually selectable first. Automatic rotation is an explicit
  `ADVENT_NEW_SCENES_IN_ROTATION=true` opt-in after preview review.
- Read weather from its existing snapshot source. Poll the versioned dashboard
  projection specified in `docs/home-dashboard-advent-api.md` in the background,
  retaining only typed pet and tariff fields. Do not couple Advent to `/api/state`,
  duplicate credentials, device control or household decision logic, or
  alter/deploy the dashboard here. The user will arrange that service's API.
- Reject sample dashboard payloads and expired snapshots. Treat unknown pet
  location as unknown, not outdoors. Data scenes recheck readiness on activation.
- Use injectable time and deterministic animation. Breakout uses bounded fixed
  physics steps rather than moving the ball once per rendered frame.
- Provide an offline, explicitly synthetic capture gallery of all eight scenes,
  including native 64x32 and exact 2x presentation captures. Fixtures never
  become live fallback data. No production deployment is part of this change.

## Consequences

The new work can be reviewed without changing the installed appliance or its
automatic rotation. The host needs one optional dashboard URL and no new cloud
keys. A dashboard outage cannot block rendering. The adapter is tested against
the dashboard's current payload shape, including stale and unknown states.
Moon phase is a mean-synodic-month visual approximation, not an ephemeris.
