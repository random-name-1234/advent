# 64x32 scene readability pass

Reviewed and implemented 2026-09-21, for the existing Pi 4 / landscape 64x32 appliance. No driver, matrix geometry, scene registration or asset changes.

## Changed

| Scene | Outcome |
| --- | --- |
| Clock | Identical digit geometry and colours. Digit shapes extracted into shared `HeadlineText`; golden-pixel tests guard the original shapes. |
| Weather | Same icon-led composition, animated icons, palette and three-panel forecast. Nine-pixel headline temperature, complete compact condition labels, separate supporting temperature and rain/wind regions, proper percent glyph, clear unknown markers and edge margins. All temperatures are Celsius. |
| Message | Shipped pixel text instead of Arial. Very short messages use crisp 2x glyphs; other messages use balanced static pages of up to three lines. No scrolling, acceleration or silent truncation. |
| Error | Original alarm/glitch/recovery design and tiny font preserved. Glitch noise starts below the title so it cannot erase letters. |
| Legibility Lab | Manual-only, seven eight-second examples rendered by the actual clock, weather, rail and message components. No extra badges or progress strip overlapping their content. Fixture data, not live travel information. |
| UK Rail Board | Earlier ADR 0018 redesign retained. Its time digits still render identically after the shared-font extraction. |

## Reviewed And Preserved

| Scenes | Decision |
| --- | --- |
| Cat; Santa | Keep assets, intentional crops and entrance/traversal choreography. |
| Donkey Kong; Space Invaders; Tetris | Keep original sprites, stage, playfield and framing. Changing Tetris to square cells would change visible board depth, not merely fix text. |
| Bonkers Parade | Retain its intentionally busy full-canvas composition. |
| Starfield Parallax; Fireworks; Boids; Orbital | Keep motion, density and focal points. No demonstrated layout defect warrants restaging them. |
| Sunrise Sunset; Metaballs; Synthwave Grid | Keep the original landscape compositions and soft procedural effects. |
| Seven seasonal GIFs | Keep authored artwork and existing playback; individually recaptured. |
| Matrix Rain; Warp Core; Game of Life; Plasma SDF; Rainbow Snow | Code-only scenes remain outside registration/rotation. Warp Core is still unsuitable as a clock background without a separate design change; no new scene is enabled here. Isolated Rainbow Snow is the clock; snow belongs to the seasonal compositor. |
| Static image; scrolling image; slideshow | Asset contracts and loaders unchanged, covered by existing asset/image tests. No new user image assets were installed. |

## Message Contract

The existing API accepts at most 120 source characters and a maximum duration of 20 seconds. A single-page message defaults to four seconds; an explicit 1-20 second duration remains available. Multiple pages require at least four seconds each. A message that cannot fit this budget is rejected with a readable error asking for a longer duration or separate shorter messages. The existing control page displays those errors; leaving duration blank uses the calculated reading time.

Messages use uppercase pixel glyphs, normalize common punctuation and accented letters, and show unsupported characters as `?`. Explicit text colours remain supported. Long words wrap without losing characters; ordinary words are kept intact. Page indicators occupy a separate bottom region.

## Verification And Captures

```sh
dotnet test advent.Tests/advent.Tests.csproj -c Release -- xUnit.ParallelizeTestCollections=false
ADVENT_SCENE_CAPTURE_DIR=/tmp/advent-scene-captures dotnet test advent.Tests/advent.Tests.csproj -c Release --filter FullyQualifiedName~SceneLayoutCaptureTests -- xUnit.ParallelizeTestCollections=false
```

The first capture test creates the information contact sheet and numbered PNGs with fixed date and weather/message fixtures. The second captures the remaining automatic, seasonal and code-only scenes as PNGs and eight-second GIF samples, with an ordered contact sheet and JSON index. Random instance fields are seeded where supported; sunrise still depends on the capture date and any ambient shared randomness is not frozen. The GIFs sample at 10 fps; they are not simulations of electrical refresh, brightness or physical LED bloom.

The suite runs serially because existing tests change the process-wide working directory. Bounds tests cover all weather-code groups and extreme/missing values, message layout and duration limits, curated symbols, exact 2x glyph scaling, the Error title's clear region, and production-renderer lab samples.

See [ADR 0019](adr/0019-use-measured-pixel-text-for-information-scenes.md). Deploy from the isolated known-good source plus the accepted rail and typography changes, matching the already-installed dependency versions. Do not include the unrelated experimental Pi 5 / Interstate worktree changes.
