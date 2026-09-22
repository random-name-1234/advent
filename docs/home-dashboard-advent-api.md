# Home Dashboard -> Advent API v1

## Implementation request for the Home Dashboard service

Add a small, read-only, versioned projection for the Advent LED matrix app.
Advent is a separate C#/.NET 10 process on `pi@192.168.1.163`, driving a 64x32
landscape panel. The dashboard is on `http://192.168.1.4:8787` (Nakiska).
This work belongs in the Home Dashboard repository, not Advent.

The first consumers are **Two Cats** (Barney/Beaker presence) and **Agile Power**
(current tariff, upcoming prices and the next cheap period). Do not return HTML,
pixel coordinates, display text, calendar contents, people locations, keys or
other household state. The dashboard owns interpretation; Advent owns layout.

## Endpoint

`GET /api/output/advent/v1`

- JSON, `Content-Type: application/json`, `Cache-Control: no-store`.
- Project the already-cached canonical state; never fetch HA, Sure Petcare or
  Octopus synchronously on this request. Target a LAN response under 250 ms.
- Advent polls every 30 seconds with a 5-second timeout. No stream/websocket or
  commands are required in v1. This endpoint must not change device state.
- Prefer the existing LAN-only access policy. No upstream/cloud credentials
  should be given to Advent. If endpoint authentication is required, accept an
  optional dedicated read-only `Authorization: Bearer <token>` credential.
- Return 200 with individual unavailable domains during source failures. Return
  503 only if the projection service itself cannot answer. Never substitute
  sample data under a `live` source tag.

## Response

This is a synthetic example, not live household information:

```json
{
  "schema_version": 1,
  "source": "live",
  "generated_at": "2026-09-21T18:10:00Z",
  "pets": {
    "status": "ok",
    "observed_at": "2026-09-21T18:09:45Z",
    "items": [
      {
        "id": "barney",
        "name": "Barney",
        "location": "indoors",
        "changed_at": "2026-09-21T17:52:00Z"
      },
      {
        "id": "beaker",
        "name": "Beaker",
        "location": "outdoors",
        "changed_at": "2026-09-21T17:58:00Z"
      }
    ]
  },
  "agile": {
    "status": "ok",
    "observed_at": "2026-09-21T18:08:00Z",
    "currency": "GBP",
    "unit": "p/kWh",
    "current": {
      "starts_at": "2026-09-21T18:00:00Z",
      "ends_at": "2026-09-21T18:30:00Z",
      "price_p_per_kwh": 24.6,
      "band": "normal"
    },
    "slots": [
      {
        "starts_at": "2026-09-21T18:00:00Z",
        "ends_at": "2026-09-21T18:30:00Z",
        "price_p_per_kwh": 24.6,
        "band": "normal"
      },
      {
        "starts_at": "2026-09-21T18:30:00Z",
        "ends_at": "2026-09-21T19:00:00Z",
        "price_p_per_kwh": 11.2,
        "band": "cheap"
      },
      {
        "starts_at": "2026-09-21T19:00:00Z",
        "ends_at": "2026-09-21T19:30:00Z",
        "price_p_per_kwh": 9.8,
        "band": "cheap"
      }
    ],
    "next_cheap_window": {
      "starts_at": "2026-09-21T18:30:00Z",
      "ends_at": "2026-09-21T19:30:00Z"
    }
  }
}
```

## Contract rules

- `schema_version` is integer 1. Additive fields are allowed; breaking changes
  require a new endpoint/version. All dates are ISO 8601 with explicit UTC offset,
  preferably `Z`. No naive timestamps or preformatted clock strings.
- `source` is `live` or `demo`. Advent rejects `demo` from its live provider.
  The existing dashboard's hybrid mode may project `live` only for domains
  actually backed by live sources. Simulated domains must be `unavailable`.
- `generated_at` is the snapshot assembly time. Refreshing it must not make
  stale underlying data appear fresh.
- Domain `status` is `ok`, `stale` or `unavailable`. `observed_at` is the last
  successful upstream observation/validation, not the last state change and
  not the time this endpoint was requested. It may be null when unavailable.
- The two domains are independent: a failed pet feed must not hide Agile data.
- No null-to-zero conversion, invented prices, or default `outdoors` locations.

### Pets

- Return stable IDs `barney` and `beaker`, with names for human identification.
  Advent ignores unrecognised IDs. One missing cat does not invalidate the other.
- `location` is `indoors`, `outdoors` or `unknown`. Convert HA/Sure Petcare
  unknown/unavailable/low-confidence readings to `unknown`.
- `changed_at` is the last known actual location change, or null. Do not stamp it
  on every poll. A stable cat location can remain unchanged for days while the
  feed itself stays fresh.
- `status: ok` requires successful source observation within 5 minutes;
  otherwise use `stale` or `unavailable`. Keep cached items if useful for
  diagnostics, but Advent will not render stale items as current facts.
- Initial appearance of a cat in Advent is not an arrival event. A short
  arrival/departure vignette may follow a known-to-known location transition
  observed while Advent runs. Tracker recovery and startup must not create one.

### Agile

- All prices are **VAT-inclusive pence per kWh**, numeric, finite, preserving
  negative prices. Do not send pounds, rounded integer pence, or standing charge.
- `currency` must be `GBP`, `unit` must be exactly `p/kWh`.
- `current` is the published half-hour interval containing `generated_at`,
  using inclusive start / exclusive end. Null if there is no valid current rate.
- `slots` contains the current and next published half-hour intervals, ascending,
  with no overlaps or duplicates, limited to 48 entries (24 hours). Unpublished
  future intervals must be absent, not zero-filled. Gaps are allowed and must
  remain gaps. Each interval has an explicit `starts_at` and `ends_at` so DST
  never creates ambiguity.
- `band` is `cheap`, `normal`, `expensive` or `unknown`, using the dashboard's
  existing configured thresholds. Advent must not invent its own thresholds.
- `next_cheap_window` is the dashboard's next continuous qualifying cheap period,
  including the current one if it is underway; both boundaries are explicit.
  Null means no known cheap window, not midnight or unavailable whole-domain.
- `status: ok` requires a current published rate and a successful tariff-cache
  validation within 30 minutes. Previously published rates may be reused if
  still valid; no fabricated extrapolation. Otherwise use `stale`/`unavailable`.
- Advent checks interval validity against its own clock as well as source
  freshness, and formats window times in `Europe/London` with `TODAY`/`TOMORROW`
  labels as appropriate.

## Failure example

```json
{
  "schema_version": 1,
  "source": "live",
  "generated_at": "2026-09-21T18:10:00Z",
  "pets": { "status": "unavailable", "observed_at": null, "items": [] },
  "agile": {
    "status": "unavailable", "observed_at": null,
    "currency": "GBP", "unit": "p/kWh",
    "current": null, "slots": [], "next_cheap_window": null
  }
}
```

## Backend acceptance tests

1. Endpoint projects cached data without triggering upstream I/O or mutations.
2. Healthy live fixtures produce the above field types and enums.
3. Missing/unavailable pet trackers become unknown, never away by default.
4. Unchanged location retains `changed_at`; successful polling advances only
   `observed_at`. A real transition changes `changed_at` once.
5. Pet failure and Octopus failure independently degrade their domain.
6. Sample mode is explicitly demo; hybrid mode never leaks simulated data.
7. Half-hour rollover, negative and zero prices, unpublished tomorrow rates,
   gaps, and both UK DST transitions preserve timestamps and units.
8. Cheap windows match existing dashboard logic, including already-active
   windows and null when no published window qualifies.
9. Freshness is based on source observations, not endpoint request time.
10. No tokens, entity IDs, location coordinates or unrelated personal data leak.

## Advent integration

Configure the **full endpoint URL**:

```sh
ADVENT_HOME_DASHBOARD_URL=http://192.168.1.4:8787/api/output/advent/v1
# Only if that endpoint requires a dedicated read-only credential:
ADVENT_HOME_DASHBOARD_TOKEN=<read-only-token>
```

Advent will use background polling, immutable snapshots, explicit expiry and
deterministic fixture previews. No HA/Sure Petcare/Octopus credentials or changes
to Pi hardware configuration are required. Until this API ships, those two
scenes remain unavailable in live mode but can be reviewed in the fixture gallery.

Please implement in the dashboard repository, add an ADR and tests, deploy via
its normal backed-up workflow, then report the endpoint URL and a redacted health
check. Do not deploy or reconfigure Advent as part of the dashboard change.
