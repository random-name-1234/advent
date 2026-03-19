# Architecture Decision Records

This folder holds ADRs for `advent`.

The goal is to keep a lightweight record of the decisions that shape the app as it grows beyond a simple scene runner.

## When To Write One

Write an ADR for decisions that materially affect:

- scene lifecycle or scheduling
- transitions or overlays
- data providers, caching, or background refresh
- web/API control flow
- output backend abstractions
- deployment or appliance behavior
- repo-wide coding conventions

Small bug fixes and isolated implementation details usually do not need an ADR.

## Format

Use short sequential filenames:

- `0001-some-decision.md`
- `0002-another-decision.md`

Keep ADRs concise and practical with these sections:

1. `Status`
2. `Context`
3. `Decision`
4. `Consequences`

## Index

- [`0001-record-architecture-decisions.md`](0001-record-architecture-decisions.md)
- [`0002-prefer-modern-csharp-style.md`](0002-prefer-modern-csharp-style.md)
- [`0003-extract-data-provider-boundaries.md`](0003-extract-data-provider-boundaries.md)
- [`0004-refresh-scene-data-in-background.md`](0004-refresh-scene-data-in-background.md)
- [`0005-only-schedule-ready-data-scenes.md`](0005-only-schedule-ready-data-scenes.md)
- [`0006-separate-scene-catalog-from-selection.md`](0006-separate-scene-catalog-from-selection.md)
- [`0007-separate-playback-engine-from-rendering.md`](0007-separate-playback-engine-from-rendering.md)
- [`0008-use-hosted-services-for-runtime-composition.md`](0008-use-hosted-services-for-runtime-composition.md)
- [`0009-register-scenes-through-modules.md`](0009-register-scenes-through-modules.md)
