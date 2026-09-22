# 0018: Page rail departures and refresh between cards

## Status

Accepted, 2026-09-21. Supersedes the activation-only snapshot capture in ADR 0004 for the rail scene only.

The introduction cards and visit durations below are amended by [ADR 0020](0020-skip-rail-intros-and-identify-fast-corridor-services.md), which also adds the FAST badge.

## Context

The deployed Pi 4 drives a landscape 64x32 panel. Three scrolling departure rows hide delay minutes and leave too little destination space. A longer scene visit also outlives the background store's 45-second freshness window.

The design takes inspiration from UK platform next-train displays: departure time and destination first, platform and explicit running status next. [Network Rail's Stockport display description](https://www.networkrailmediacentre.co.uk/news/the-futures-bright-the-futures-orange-at-stockport-station) describes this hierarchy and orange LED text. This is inspiration, not a claim of railway signage compliance.

## Decision

- Keep the logical 64x32 canvas and existing output scaling, hardware configuration, other scenes and engine transitions unchanged.
- Replace the rail scene's scrolling table with a 10-second station introduction, up to three 15-second departure cards and an optional 20-second notice section per station. Maximum visit: 150 seconds; six departures without notices take 110 seconds.
- Use the main clock's existing nine-pixel digits and the shipped five-pixel DMI text. Use static amber text, a separate platform field, readable destination abbreviations and explicit ON TIME, delay minutes or CANCELLED labels. Do not imply a service is fast from a relative stop-count heuristic.
- Each card captures a fresh background snapshot at its boundary. No scene HTTP calls. Keep the visible card stable, exclude departed/past services, and do not repeat services already shown. If the store has no fresh snapshot at a boundary, end the visit rather than retain an old platform or status.
- Notices use measured static word-wrapped pages. Show the highest-priority notice first; bound notice time and explicitly direct the reader to National Rail if content cannot be shown in full. No simultaneous marquees or page swipes.
- Preserve configured corridor filtering and the actual destination of through services. Unavailable data is not presented as an empty timetable.

## Consequences

The panel shows fewer simultaneous departures but gives each useful information room to breathe. Full calling points and operator names are intentionally omitted from the compact cards. Longer destination names use curated display labels or their CRS code, not unreadable squeezed text. Notices are summaries with explicit continuation when necessary, not replacements for full travel advice.

The carousel has deterministic time injection and tests for measured bounds, delay/cancellation text, refresh boundaries, service identity, midnight, stale data and the duration cap. Deployment must be built from the deployed revision plus this change only: unrelated experimental Pi 5 and Interstate work remains untouched.
