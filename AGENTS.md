# AGENTS

This file applies to the whole `advent` repo.

## Intent

`advent` is a C#-first LED matrix appliance. It now spans scene playback, transitions, overlays, background data, web control, and multiple hardware backends.

Treat [`docs/architecture-proposal.md`](docs/architecture-proposal.md) as the current design direction when making non-trivial changes.

## C# Style

- Target the current repo toolchain: `.NET 10` and `C# 14`.
- Prefer modern C# 14 features when they improve readability and are supported by the project toolchain.
- Prefer a functional style where it makes code clearer and smaller:
  - pure functions
  - immutable records and value-like data
  - pattern matching
  - switch expressions
  - collection expressions
  - small composable helpers
- Keep that preference in service of readability. Do not compress code into dense LINQ or clever one-liners if it becomes harder to follow.
- Keep side effects at the edges where possible. Data fetching, file access, time, and hardware I/O should be easy to spot.

## Architectural Guardrails

- Prefer scenes to be renderers over already-available state, not owners of HTTP calls or background polling.
- Prefer transitions and overlays to be engine-level behavior rather than ad hoc scene-specific logic.
- Keep hardware concerns behind output abstractions such as [`IMatrixOutput.cs`](IMatrixOutput.cs).
- Prefer explicit models for playback state, scheduling, and data freshness over implicit behavior spread across multiple classes.

## ADRs

- Record significant architectural or cross-cutting decisions in [`docs/adr/`](docs/adr/).
- Write ADRs as you go, not as a cleanup task later.
- LLM agents working in this repo should do the same.
- Use short sequential filenames: `0001-some-decision.md`, `0002-another-decision.md`, and so on.
- Keep ADRs concise and practical. Use this shape:
  - `Status`
  - `Context`
  - `Decision`
  - `Consequences`
- Add a new ADR when a decision materially affects one or more of:
  - scene lifecycle or scheduling
  - transitions or overlays
  - data fetching / caching / provider boundaries
  - web API or control flow
  - output backends or deployment model
  - repo-wide coding conventions
- If a later decision replaces an earlier one, add a new ADR that supersedes it instead of rewriting history.

## Working Agreement For Contributors And LLMs

- Read this file and the relevant ADRs before substantial refactors.
- If a change is architectural, leave a short paper trail in `docs/adr`.
- When summarizing completed work, mention any ADRs added or updated as part of the change.
