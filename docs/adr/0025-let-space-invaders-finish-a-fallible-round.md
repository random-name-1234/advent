# 0025: Let Space Invaders finish a fallible round

## Status

Accepted.

## Context

Space Invaders repeatedly lost and restarted, increasing difficulty after losses.
Dead rows and columns still constrained the formation, aiming ignored projectile
travel, and dodging could steer back into incoming fire. The 18-second scene and
20-second catalog limit also cut short games that could otherwise finish.

## Decision

- Play one complete round per visit, with a 45-second catalog limit. End gameplay
  at 42 seconds at the latest, then hold a clear result for 2.5 seconds before the
  existing engine-owned fade. Keep ADR 0024's clock gaps unchanged.
- Use only living aliens for movement and invasion checks, predict shot travel,
  and evaluate the ship's movement path when dodging. Shots still collide with
  aliens and destructible shields; a win requires clearing all 21 aliens.
- Retain finite movement speed, one player shot at a time, imperfect aiming and
  variable reaction/fire intervals. Never preselect wins, grant invulnerability,
  or deliberately kill the player. Preserve the existing sprites and layout.
- Use a bounded 30 Hz simulation and seeded test constructor so outcomes are
  reproducible independently of rendering cadence. Check a broad sample of seeds
  for both real wins and losses, alongside collision and lifecycle regressions.

## Consequences

Visits can last longer than other arcade scenes but finish promptly after a
result. There is no invisible escalating difficulty or mid-visit retry. A brief
WAVE CLEAR, GAME OVER or TIME UP makes the ending visible without adding a HUD.
Balance is statistical, not a promise about the outcome of any particular visit.
