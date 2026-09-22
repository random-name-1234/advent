# 0020: Skip rail intros and identify fast corridor services

- Status: Accepted
- Date: 2026-09-21
- Amends: [0018](0018-page-rail-departures-and-refresh-between-cards.md)

The FAST thresholds below are superseded by [ADR 0021](0021-base-fast-rail-classification-on-the-published-timetable.md). The original two-stop limit excluded published short-journey services. The no-intro layout and other behaviour remain unchanged.

## Context

Alan wants departures immediately, without a Cambridge or King's Cross introduction, and a readable fast-service indication in both directions. Each departure already identifies its origin with FROM CBG or FROM KGX. The deployed target remains the Pi 4 and landscape 64x32 panel.

Darwin's detailed board supplies full subsequent locations, scheduled timestamps and isPass flags, not an explicit fast-service category. The display ticker truncates calls and cannot classify services reliably. Through trains must be assessed only as far as the configured counterpart, not their final destination. The live feed checked on 2026-09-21 includes 50-minute Cambridge -> King's Cross trains calling at Cambridge South and many pass-through timing points; it also includes much slower stopping trains.

[Great Northern's Cambridge South information](https://www.greatnorthernrail.com/travel-information/cambridge-south) describes direct King's Cross services calling just before Cambridge. A strict non-stop-only rule would miss these. This source does not define our threshold below: the badge is deliberately an application rule, not a claim of an official operator designation.

## Decision

- Skip station introductions when upcoming departures exist. Keep ten-second empty/unavailable status cards and existing notice pages. Departures still get fifteen seconds each: six cards take ninety seconds, with a hard 130-second limit including two twenty-second notices.
- Show FAST in the upper-right header instead of the ordinal on qualifying services, in the same shipped DMI pixel font and amber palette. Keep origin, scheduled departure, platform, actual final destination and live status unchanged. Do not add scrolling or extra pages.
- Classify in the background snapshot provider, before any calling-point truncation. Only CBG -> KGX and KGX -> CBG qualify. Require a scheduled departure and a non-passing, non-cancelled counterpart call with a scheduled arrival. Require a positive scheduled journey of at most sixty minutes and no more than two intermediate station calls.
- Ignore pass-through locations and stop assessment at the counterpart, including for trains continuing to Ely or King's Lynn. Count cancelled intermediate calls conservatively. Missing/invalid metadata, unknown intermediate station codes, cancelled/suppressed trains and other corridors get no FAST badge. Estimated times do not influence the badge, so a delayed fast train remains a fast train with its delay visible.
- Remove the old unused ticker heuristic that inferred Fast via from a short, truncated list of names. No network calls are introduced into rendering or card selection.

## Consequences and verification

The badge is intentionally conservative and may omit unusually slow or heavily diverted limited-stop services. It is not a promise that a delayed FAST train arrives before the next train. Revisit the documented threshold if timetable patterns change; do not choose FAST relative to whatever trains happen to be in the board window.

Tests cover both directions, boundaries at sixty minutes and two stops, through destinations, more than eight raw timing points, midnight, delays, missing data, cancellations, other corridors, stable card boundaries, immediate station switching and scene duration. Layout tests measure both origins with and without FAST for every status and confirm non-overlap on 64x32. Deterministic capture fixtures cover the badge in both directions and on a delayed through service.

Deployment remains a narrow DLL/PDB replacement on pi@192.168.1.163 with a rollback copy. Existing configuration, credentials, drivers, assets, other scenes and unrelated experimental hardware work are left untouched.
