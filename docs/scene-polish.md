# Existing-scene polish, September 2026

Approved follow-up to the complete scene review. The target remains Pi 4,
landscape 64x32. This is not a native-128x64 redesign: the existing exact 2x
presenter and every hardware setting are unchanged. Policy is recorded in
[ADR 0024](adr/0024-polish-existing-scenes-and-space-automatic-playback.md).

## Applied Review

| Scene/path | Outcome |
| --- | --- |
| Synthwave Grid | Moving horizontal lines are rasterised every frame; no fractional-phase dropout. |
| Starfield Parallax | Trails follow rather than lead left-moving stars; right-edge clipping retained. |
| Aquarium | One-pixel rounded fish bob and bubble sway; three fish and open water preserved. |
| Two Cats | Recent observed movement meets the resting pose continuously; indoor backdrop separates Beaker's dark coat. Barney's white bib and both tabby palettes are preserved. |
| Agile Power | Large current digits retained. Six-hour graph labelled with a current marker; both cheap-window endpoints and their days shown. Missing windows occupy only the last three seconds. |
| Donkey Kong | Brighter player clothing and alternating climbing hands within the existing 6x8 bounds. Stage, timing and other sprite shapes unchanged. |
| Orbital | Dimmer background stars, unchanged planets/rings; injectable activation time. |
| Sunrise Sunset | Translucent warm halo instead of a dark disc; injectable time. Original solar arc, hills and full-day sequence retained. |
| Moonlit Landscape | Restrained separation between hills and water; phase calculation unchanged. |
| Pixel City | Deterministically staggered window changes, unchanged bus and terraces. |
| Bonkers Parade | Slightly dimmer backdrop and ribbon edge, preserving its intentional chaos. |
| Fireworks | Slightly wider burst spacing and inset centres; original sparks and colours retained. |
| Boids | Dimmer tails and a small separation increase; thirty agents retained. |
| Tetris | Initial stack built by dropping actual pieces, capped at twelve rows with a clear well. Multi-line compaction fixed. Original 2x1 cells retained. |
| Breakout | One dim trailing pixel only on empty playfield; collision size and fixed-step physics unchanged. |
| Weather Window | Darker storm clouds with slow local illumination, more rain and sparse glass streaks. No full-screen flashes; opaque joinery retained. |
| Message | Prefer sentence-ending wrapped lines at page boundaries without extra pages or lost words. Existing duration checks retained. |
| Error | Artwork unchanged; manual-only to avoid decorative fault alarms in automatic rotation. |
| Legibility Lab | Fourteen real-renderer samples, eight seconds each, including cats/unknown location, Agile current/negative/unknown-band prices, overnight and missing windows. Manual synthetic data only. |
| Animated GIF loader | Positive minimum 10 ms frame delay and bounded catch-up, including full-cycle skipping. |
| Preview | Automatic integer-pixel fit, 1x/2x options, wrapped controls and left-accessible scrolling at explicit zoom. |

Clock, Weather's text/geometry, UK Rail Board, original Cat, Metaballs, Space
Invaders and Night Train were recaptured and intentionally preserved. Rail keeps
its reading times, FAST identification and no introduction screens. Night Train
keeps its unnamed British station. Santa's exit and the seven December GIFs were
reviewed without replacing the authored art. Matrix Rain, Warp Core, Game of Life,
Plasma SDF and Rainbow Snow remain excluded from automatic registration.

## Verification And Captures

```sh
dotnet test advent.Tests/advent.Tests.csproj -c Release
./scripts/validate-assets.sh --no-build --no-restore --verbosity quiet
node scripts/test-preview.mjs
ADVENT_SCENE_CAPTURE_DIR=/tmp/advent-polish \
ADVENT_RAIL_CAPTURE_DIR=/tmp/advent-polish/rail \
dotnet test advent.Tests/advent.Tests.csproj -c Release --no-build \
  --filter 'FullyQualifiedName~SceneLayoutCaptureTests|FullyQualifiedName~RailCardCaptureTests'
dotnet run -c Release --no-build --no-launch-profile -- \
  --capture-new-scenes=/tmp/advent-polish/new
```

Release tests pass, including 900 consecutive Synthwave frames, both cat travel
directions, star trails and edge bounds, quantised fish movement, GIF catch-up,
window/date fit, multi-line Tetris clearing, readiness-aware selection and manual
queue preservation. Frame API tests retain authenticated metadata and exact 2x
output for both supported profiles. The preview test covers 319px and desktop
widths, both frame sizes, height constraints and explicit zoom. A real-browser
319px check also confirms all four matrix edges and accessible zoom scrolling.

Fresh information, rail, seasonal/legacy and new-scene contact sheets plus
six-phase timelines were visually reviewed. Fixed time now covers Orbital and
Sunrise; instance random generators are seeded for captures. Live hardware bloom
and viewing-distance contrast cannot be judged from framebuffer captures, so
Weather's secondary text brightness was deliberately left alone.

Deployment follows ADR 0023: CI/merge first, simulator against installed Pi
dependencies, full rollback backup, application-only replacement and live health,
frame, feed readiness and native-driver hash verification.
