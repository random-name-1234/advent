# 0002 Prefer Modern C# Style

## Status

Accepted

## Context

`advent` now contains a mix of rendering code, orchestration code, API code, and hardware integration. As that surface area grows, readability depends more on keeping data flow obvious and reducing incidental ceremony.

The repo already targets modern `.NET`, and upcoming refactors will touch cross-cutting logic where concise, explicit code will help.

## Decision

For new and substantially edited code, we will prefer:

- `.NET 10`
- `C# 14`
- a functional style where appropriate

In practice, that means favoring:

- pure helper functions where possible
- immutable records and value-like models
- pattern matching and switch expressions
- collection expressions
- small composable methods

This preference is not a license for cleverness. Readability wins over brevity, and imperative code is still the right choice when it is clearer for render loops, state machines, or performance-sensitive paths.

## Consequences

- New code should usually become shorter and easier to scan.
- Contributors should feel comfortable using modern language features when they genuinely improve clarity.
- Reviews should push back on dense or overly abstract "functional" code that makes the runtime flow harder to understand.
