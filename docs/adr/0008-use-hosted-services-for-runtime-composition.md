# 0008 Use Hosted Services For Runtime Composition

## Status

Accepted

## Context

The application startup flow still manually created long-lived runtime objects in `Program`:

- data refresh loops
- web control host
- render loop
- output backend

That worked, but it left lifecycle management and dependency wiring spread across imperative startup code rather than the host.

## Decision

We now compose the runtime through a host-based registration model:

- `AdventServiceCollectionExtensions` registers the application graph
- background snapshot refresh runs through hosted services
- rendering runs through `RenderLoopHostedService`
- web control runs through `WebControlHostedService`

`Program` is now a thin composition root that builds the host and runs it.

## Consequences

- Long-lived services now have a consistent lifecycle model.
- Startup wiring is easier to extend without reopening `Program`.
- Data refresh, rendering, and web control are explicit runtime capabilities instead of ad hoc loops.
- The app is closer to the architecture proposal's “host plus services” design.
