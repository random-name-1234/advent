# 0021: Base FAST rail classification on the published timetable

## Status

Accepted, 2026-09-21. Supersedes the sixty-minute/two-intermediate-stop rule in [ADR 0020](0020-skip-rail-intros-and-identify-fast-corridor-services.md), not its layout or departure-first sequencing.

## Context

Alan correctly identified that two intermediate stops excluded fast Cambridge <-> King's Cross journeys. The first implementation chose a conservative cap without reviewing the full timetable. A short calling list is not a useful proxy for this corridor's faster journey group.

We reviewed the published [Great Northern Table A](https://www.greatnorthernrail.com/service-updates/timetables?id=4118&name=Table%20A#timetableoutput), standard timetable valid 17 May-12 December 2026. The review covered Monday, Saturday and Sunday in both directions, with Tuesday northbound as a weekday cross-check. Counts below exclude the boarding/alighting stations and count an arrival/departure pair at one intermediate station once. They refer only to the Cambridge-King's Cross leg, not the full through service.

| Day/direction | Shorter journeys | Intermediate calls in shorter group | Slower group starts at |
| --- | --- | --- | --- |
| Monday CBG -> KGX | 50-57 min | 1, 2, 3 | 68 min, 8+ calls |
| Monday KGX -> CBG | 49-58 min | 1, 3, 5 | 68 min, 8+ calls |
| Saturday CBG -> KGX | 49-56 min | 1, 3 | 67 min, 8+ calls |
| Saturday KGX -> CBG | 49-57 min | 1, 3, 5 | 68 min, 8+ calls |
| Sunday CBG -> KGX | 51-60 min | 1, 3 | 67 min, 8+ calls |
| Sunday KGX -> CBG | 50-63 min | 1, 6, 7 | 69 min, 8+ calls |

Examples that must not be missed:

- Monday Cambridge 05:42 -> King's Cross 06:37: 55 minutes, calling at Cambridge South, Royston and Letchworth Garden City.
- Monday King's Cross 23:18 -> Cambridge 00:16: 58 minutes, calling at Letchworth Garden City, Baldock, Ashwell & Morden, Royston and Cambridge South.
- Sunday King's Cross 23:24 -> Cambridge 00:25: 61 minutes with six intermediate calls.
- Sunday King's Cross 08:12 -> Cambridge 09:15: 63 minutes with seven intermediate calls, including Stevenage and Hitchin.

The operator's timetable-change notice also calls the 18:54 King's Cross service fast for Hitchin users. Its Cambridge leg is 74 minutes with thirteen calls, so that destination-specific description must not be applied to the whole route.

## Decision

- Keep FAST as an app-defined indication of the shorter journey group, not a claimed official Darwin/operator category. Use scheduled corridor duration <= 65 minutes AND intermediate calls <= 7, with positive duration required. Sixty-five lies in the observed gap between the longest shorter journey (63) and the quickest slower one (67); seven is the observed maximum calling count of that shorter group.
- Retain full structured Darwin data as the runtime source. The published timetable justifies the thresholds and regression examples; do not embed fixed departure times, weekday assumptions or an online timetable scraper in the appliance.
- Retain all existing safeguards: both CBG/KGX directions only; pass-through points excluded; assessment ends at the counterpart even on through trains; delayed estimates do not alter classification; missing/invalid metadata or cancelled/suppressed service/target calls get no badge; cancelled intermediate calls still count.
- Add regression cases for actual weekday and weekend endpoint times and complete corridor stop lists, including the newly covered three/five/six/seven-stop journeys, midnight, and slower eight/thirteen-stop counterexamples. Test both threshold boundaries and preserve delay/status rendering.

## Consequences

The badge now covers the corridor's shorter journeys even with several stops. It still excludes slow local patterns and does not imply that a delayed FAST train arrives first. The rule is an inference from the standard timetable, not an official service class. Standard tables exclude engineering/disruption alterations; the runtime always uses the service's supplied scheduled times and calls, and a diverted or changed service may legitimately fall outside the rule. Reassess these thresholds against future timetable changes rather than silently widening them again.

No scene layout, introduction behaviour, network polling, hardware settings, credentials or assets change. Deployment remains the existing backed-up DLL/PDB-only update to the Pi 4's 64x32 installation.
