# 0001 Record Architecture Decisions

## Status

Accepted

## Context

`advent` has grown from a small matrix scene runner into a system that now includes:

- scene orchestration
- transitions and overlays
- background-backed scenes
- web control
- appliance deployment
- Pi 4 and Pi 5 output support

That growth makes it easier to lose track of why a structural choice was made or what tradeoffs were accepted at the time.

## Decision

We will keep Architecture Decision Records in [`docs/adr/`](README.md) for significant architectural and cross-cutting decisions.

ADRs in this repo should:

- use short sequential numbering
- stay concise
- be written alongside the change, not long afterwards
- prefer superseding old decisions with new ADRs rather than rewriting history

## Consequences

- Future refactors should have a clearer paper trail.
- Humans and LLMs working in the repo get a shared source of architectural context.
- There is a small ongoing cost to write ADRs, but it should pay for itself by reducing repeated rediscovery.
