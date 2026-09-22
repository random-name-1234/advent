# 0019: Use measured pixel text for information scenes

## Status

Accepted, 2026-09-21. Builds on ADR 0018; preserves the 64x32 logical canvas and ADRs 0004, 0010 and 0011.

## Context

The clock is the legibility reference on the deployed Pi 4. Weather still uses a host font for its main temperature, clips condition labels and lacks a percent glyph. Messages scroll even when short and accelerate to fit long text into 20 seconds. The manual Legibility Lab overlays labels on its examples and no longer represents production layouts.

## Decision

- Extract the clock's existing digit shapes into a shared nine-pixel headline renderer without changing the clock or rail geometry. Use it for weather temperatures too.
- Use shipped five-pixel DMI glyphs for information text, with integer positions, explicit regions and hand-drawn missing symbols. Preserve curated glyphs even if the font asset cannot be loaded.
- Retain weather's animated icons, palette, three-day outlook and slide transitions. Separate the icon, temperature, supporting temperature and rain/wind fields. Use complete intentional condition labels rather than arbitrary clipping. Invalid values get unknown markers rather than clipped or invented numbers.
- Messages hold still. Very short messages may use exact 2x pixel text; other messages use measured word-wrapped pages with at least four seconds per page. Keep the existing 120-character and 20-second API limits. Reject content or explicit durations that cannot meet the reading budget instead of silently truncating or accelerating it. Single-page messages retain explicit 1-20 second overrides.
- The manual-only Legibility Lab renders real clock, weather, rail and message components with fixture data and no overlapping lab decorations.
- Review and recapture all other scenes, but preserve their original compositions, assets and animation identities unless a demonstrated defect warrants a local correction. Tetris proportions and game restaging are not part of this typography pass. No new scene enters automatic rotation.

## Consequences

Informational text no longer depends on an installed system font. Messages become paged rather than scrolling; API callers receive useful validation errors for unreadably short durations. Unsupported message characters are visibly represented with a question mark, with common accented letters and punctuation normalized first.

Tests cover glyph coverage, bounds, non-overlap, stationary messages, complete paging, duration validation, scene lifecycles and clock/rail rendering equivalence. Deterministic capture fixtures document the changed layouts; seeded captures retain the original game and ambient designs. Deployment remains an isolated, backed-up application update with drivers, assets and hardware configuration untouched.
