# 0023: Roll out reviewed scenes on the existing Pi 4

## Status

Accepted, 2026-09-22, following the user's preview approval and explicit request
to merge and deploy. Extends the rollout decision in ADR 0022.

## Context

The active appliance is pi@192.168.1.163, a Pi 4 with a landscape 64x32 panel.
Its installed driver and local configuration are known to work. There is no
registered GitHub Actions deployment runner. The live appliance also exposes
frame metadata used by its existing preview, absent from the current main branch.

## Decision

- Merge through a tested PR, then deploy the merged source over SSH using the
  backed-up application-only procedure from ADR 0020. Do not change the systemd
  unit, native driver, panel wiring/timing, credentials or dashboard service.
- Stage the new application against the installed runtime dependencies and
  verify a simulator process on the Pi before replacing the running assembly.
  Keep rollback copies of the application and any added local configuration.
- Enable the eight approved scenes with ADVENT_NEW_SCENES_IN_ROTATION=true and
  configure the existing read-only dashboard projection URL. Keep the default
  opt-in behaviour unchanged for other installations.
- Preserve authenticated /api/frame/meta and physical-size /api/frame output
  using host dimensions and exact nearest-neighbour scaling. The browser uses
  the returned image dimensions. This does not change the hardware backend.

## Consequences

Approved scenes join normal rotation; data scenes still require fresh live data.
Deployment must verify service health, 64x32 frames, scene readiness, normal mode
and unchanged native-driver hashes. There is no Pi 5 or Interstate deployment.
