# 0024: Polish existing scenes and space automatic playback

## Status

Accepted, 2026-09-22, following approval to apply the scene review in full.
Builds on ADRs 0010, 0019 and 0022-0023.

## Context

The reviewed 64x32 scenes need local motion, contrast and information fixes, not
another redesign. Independent random picks can repeat immediately, and automatic
requests can accumulate during the intentionally long rail visit. The decorative
Error scene can be mistaken for a real appliance fault.

## Decision

- Preserve existing scene compositions, pixel dimensions, transitions and assets.
  Fix demonstrated defects and apply restrained review polish. Leave code-only
  experiments disabled and make Error manual-only alongside Legibility Lab.
- Select normal automatic scenes without replacement from a readiness-aware bag.
  Avoid consecutive repeats whenever another ready scene exists. Keep ordered
  test-mode cycling and explicit named requests independent of the bag.
- Do not enqueue automatic scenes while playback or its queue is occupied. Require
  ten idle seconds before the next automatic visit; retain the random cadence and
  rate limit. Never discard, delay or reorder manual requests to create that gap.
- Extend deterministic motion/data tests and the manual lab using real renderers.
  Preview defaults to the largest integer scale fitting its viewport, while
  explicit zoom remains scrollable. Physical frame output is unchanged.
- Merge and deploy using ADR 0023's simulator-first, backed-up application-only
  procedure. Preserve working Pi 4 drivers, secrets and hardware configuration.

## Consequences

Normal rotation covers the ready catalogue more evenly and leaves the clock
visible between visits, including after rail. Error remains explicitly selectable.
No dashboard decisions or data-fetching responsibilities move into renderers.
Capture review checks framebuffer quality; physical LED bloom still needs the
owner's real-panel judgement.
