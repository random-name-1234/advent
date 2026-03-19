# 0009 Register Scenes Through Modules

## Status

Accepted

## Context

Even after separating the catalog from selection, the catalog still had to know every scene family directly:

- built-in scenes
- seasonal scenes
- rail and weather scenes
- image-driven scenes
- manual-only scenes

That meant adding or changing a scene family still required editing the central catalog implementation.

## Decision

Scenes are now contributed through `ISceneModule`.

The catalog is built from module registrations such as:

- `BuiltinSceneModule`
- `WeatherSceneModule`
- `RailSceneModule`
- `SeasonalSceneModule`
- `ImageSceneModule`
- `ManualSceneModule`

Each module contributes scene registrations from its own bounded context, and the catalog aggregates them into known, selectable, and cycle entries.

## Consequences

- New scene families can be added by registering a module instead of editing a monolithic catalog.
- Scene discovery is more plugin-like and easier to extend.
- Image scene loading now lives in the image module rather than the catalog core.
- The app is closer to a modular architecture even while staying in a single project.
