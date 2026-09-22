# Space Invaders balance review

Reviewed 2026-09-22 for the existing Pi 4 / 64x32 landscape panel.
Lifecycle policy: [ADR 0025](adr/0025-let-space-invaders-finish-a-fallible-round.md).

## Findings and changes

- Dead rows caused premature invasions; dead columns caused unnecessary descents.
  Only living aliens now determine bounds, and invasion occurs at ship height.
- The player shot at current rather than future positions and could refuse all
  shielded lanes. It now leads targets and can shoot holes through its shields.
- Dodging considered the destination rather than the path, and could steer back
  into a bolt. The player now evaluates the path and retains fallible reactions.
- Losses increased difficulty and restarted inside a short visit. Each visit now
  plays one round, with up to 42 seconds of gameplay and a 2.5-second result hold.
  The catalog allows 45 seconds; engine-owned transitions and clock gaps remain.
- Original sprites, palette, formation size, shields and framing are retained.
  No outcome is scripted, and the player has no extra lives or invulnerability.

## Measured results

The original first round lost in all 500 seeds tested (0-499), averaging 8.10
kills out of 21. The final implementation across seeds 0-999 produced **656 wins
and 344 losses**, with no timeouts. Mean gameplay duration was 15.45 seconds;
the longest was 27.63 seconds, excluding the result hold and engine fades.

The regression suite replays those 1,000 seeds with broad 50-80% win guardrails,
not an exact required sequence. Seed 6 clears all aliens after 20.17 seconds;
seed 2 loses after 19.87 seconds with one alien remaining. Both have reviewed
six-frame capture sheets and complete 20 fps animations. These demonstrate the
existing composition and readable, unclipped WAVE CLEAR / GAME OVER endings.

Reproduce tests and optional captures:

```sh
ADVENT_INVADERS_CAPTURE_DIR=/tmp/advent-invaders \
  dotnet test advent.Tests/advent.Tests.csproj -c Release \
  --filter 'FullyQualifiedName~SpaceInvaders'
```

The output contains `win.gif`, `close-loss.gif` and matching `*-sheet.png` files.
Tests also cover cadence independence, collision-earned results, result holds,
reactivation, the catalog's extended timer, shield drilling, predictive aiming,
swept-path dodging, living formation bounds, negative elapsed and timeout cleanup.
