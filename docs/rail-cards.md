# Rail cards on the 64x32 panel

The rail scene uses the clock's 36x9 time digits, shipped DMI text and amber platform-board styling. It still shows the configured two-way corridor, including through services to their actual destinations.

Each station starts directly with up to three 15-second departure cards, followed by an optional 20-second notice section. Six departures take 90 seconds without notices, at most 130 seconds with notices at both stations. Fewer departures mean a shorter visit. Empty and unavailable boards retain a 10-second status card, not an introduction before trains.

Departure fields remain still: FROM station code and sequence number; scheduled time; platform; destination; ON TIME, delay minutes, CANCELLED or CHECK STATUS. Long familiar names use display labels such as KINGS CROSS and LIVERPOOL ST. Other long names use a CRS code if available. Unknown or oversized platforms show -- rather than a misleading clipped number. Operator and calling-point tickers are deliberately omitted from these compact cards.

`FAST` replaces the sequence number in the upper-right corner for qualifying Cambridge <-> King's Cross trains. This is an app-defined indication of the shorter, limited-stop journey group, not an operator-supplied classification: the scheduled journey to the corridor counterpart must be positive and at most 65 minutes, with at most seven intermediate station calls. These limits are based on Great Northern Table A, including Sunday services with six or seven stops, rather than the original two-stop assumption. Pass-through timing points do not count, and calls after Cambridge on a through service do not count. The actual final destination remains visible. Delays do not change the classification or replace the delay field. Missing timings, a missing/non-stopping/cancelled counterpart call, suppressed/cancelled services and other corridors get no badge. A cancelled intermediate call still counts, so disruption does not promote a stopping service to FAST.

The scene reads the background store at card boundaries, never from the network. It excludes departed/past services, avoids repeats, and exits if fresh data is no longer available. A displayed card is stable for its 15-second reading interval. Notices use up to four static pages; MORE ONLINE / NATIONAL RAIL explicitly marks omitted content.

## Verify and capture

```sh
dotnet test advent.Tests/advent.Tests.csproj
ADVENT_RAIL_CAPTURE_DIR=/tmp/advent-rail-captures dotnet test advent.Tests/advent.Tests.csproj --filter FullyQualifiedName~RailCardCaptureTests
```

Two existing non-rail test classes change the process-wide current directory. If the full suite races on local-image discovery, run it serially with `dotnet test advent.Tests/advent.Tests.csproj -- xUnit.ParallelizeTestCollections=false`.

The capture test renders fixed synthetic inputs: FAST in both directions, on-time/delayed/cancelled services, platforms 5/10/12A/unknown, a fast through service, three-digit delays, unavailable/empty boards and notice pages. It writes native PNGs, nearest-neighbour enlargements and a contact sheet. These are fixture data, not live train information. The production preview remains `/preview`, with native frames at `/api/frame`.

See [ADR 0018](adr/0018-page-rail-departures-and-refresh-between-cards.md) for the original rationale and deployment boundary, amended by [ADR 0020](adr/0020-skip-rail-intros-and-identify-fast-corridor-services.md). [ADR 0021](adr/0021-base-fast-rail-classification-on-the-published-timetable.md) supersedes the original FAST thresholds with timetable evidence.
