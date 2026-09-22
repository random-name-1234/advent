# Eight independent 64x32 scenes

These additions leave the existing scene artwork, timing and hardware settings
alone. They are selectable from the normal scene picker, but excluded from
automatic rotation by default while their previews are reviewed.

| Scene | Composition | Data |
| --- | --- | --- |
| Night Train | British electric commuter train, yellow cab, Cambridge canopy / doors / departure signal | None |
| Aquarium | Three fish, independent swimming/tail cycles, plants, bubbles | None |
| Pixel City | Terraced street, double-decker bus, pedestrians, night windows | Local clock and configured solar location |
| Breakout | Collision-driven ball, destructible bricks, fallible paddle, re-serve | None |
| Weather Window | Rain, snow, fog, clouds or sunshine outside opaque window joinery | Existing fresh weather snapshot |
| Moonlit Landscape | Phase-lit lunar disc, hills, slow clouds and water reflection | UTC clock; approximate mean lunar cycle |
| Two Cats | Named indoor/outdoor vignettes; unknown is `NO FIX` | Versioned home-dashboard API |
| Agile Power | Ten seconds of current price/graph, ten seconds of cheap window | Versioned home-dashboard API |

All art is drawn at integer pixel positions. The two data scenes use the clock's
nine-pixel headline digits and existing DMI text. Cat sprites use the supplied
photo references: Barney is a fluffy silver-grey tabby with a broad white bib,
pale muzzle and white paws; Beaker has a brown face, black/brown striped coat,
dark chest and smaller pale chin. Cat-flap movement is only
shown after an observed known-to-known transition; no fake arrivals on startup.

### Artwork review: cats and British railway

The user's photo/name mapping is authoritative; do not substitute orange Barney
or blue-grey Beaker. Both need visible tabby markings, not merely different
solid fills. The pictures are visual references, not runtime dependencies, and
are not copied into the repo or gallery.

Night Train uses a stylised Great Northern-like electric unit: continuous
silver sides, blue doors/lower body, sloping yellow cab, lit window band and
pantograph. Station cues are brickwork, a green canopy with valance, a readable
Cambridge nameboard and a platform starting signal ahead of the train. This is
an evocation, not an exact scale drawing of a particular platform or train class.
Doors open only while stopped and close before the departure signal clears;
the entire train leaves before the scene ends. No lifecycle or data contract
changes accompany this art revision, so ADR 0022 remains the architecture.

Moon rendering uses a mean synodic cycle, sufficient for a tiny ambient disc,
not precise observing predictions. The reference epoch and period come from
[NASA's phase tables](https://eclipse.gsfc.nasa.gov/phase/phases1901.html).

## Offline animation gallery

From the repository directory:

```sh
dotnet run --no-launch-profile -- --capture-new-scenes=/tmp/advent-scene-gallery
python3 -m http.server 8091 --bind 127.0.0.1 --directory /tmp/advent-scene-gallery
```

Open `http://127.0.0.1:8091`. This command exits after writing eight looping GIFs,
native screenshots, exact 2x screenshots, a contact sheet, variant captures and
a self-contained HTML index. The page clearly labels synthetic home/weather
data and its fixed timestamp. It starts no matrix hardware, fetches no services
and never registers fixtures as live state.

The gallery includes sunny/snowy/foggy weather, daytime city, the four principal
moon phases, unknown pet presence, arrival animation, negative price and no
published cheap window. It uses the actual C# scene renderers, not mockups.

## Live simulator and integration

```sh
ADVENT_WEB_BIND=127.0.0.1 ADVENT_WEB_PORT=8090 dotnet run --no-launch-profile -- --simulator --matrix-size=64x32
```

The normal selector is at `http://127.0.0.1:8090/`, with the live framebuffer at
`http://127.0.0.1:8090/preview`. Home-data scenes require the URL below to be set.
The service described in [the API spec](home-dashboard-advent-api.md) was verified
live on 2026-09-22: both pet and Agile domains passed Advent's readiness checks.
No live household values are included in the checked-in fixtures or gallery.

```sh
ADVENT_HOME_DASHBOARD_URL=http://192.168.1.4:8787/api/output/advent/v1
# Optional, only for a dedicated read-only endpoint credential:
ADVENT_HOME_DASHBOARD_TOKEN=<read-only-token>
# Enable only after reviewing the new scenes:
ADVENT_NEW_SCENES_IN_ROTATION=true
```

The full configured URL is polled every 30 seconds with a five-second timeout.
Successful unavailable responses invalidate old state immediately; transport
errors retain it only until the two-minute snapshot expiry. Pet observations
expire after five minutes; Agile observations after thirty minutes, with the
current half-hour interval additionally checked against the local clock.
No cloud credentials are copied and no control calls are made.

Scene readiness is independent per domain. If data expires while a scene is
visible it shows a short `NO LIVE DATA` message, not zero prices or fake absence.
Unknown pets are not automatically labelled outdoors. Future-price gaps remain
gaps in the chart. Colours use the dashboard's price bands, not new thresholds.

## Verification and rollout

```sh
dotnet test advent.Tests/advent.Tests.csproj
```

Tests cover repeatable rendering/reset/lifetime, fixed-step physics and genuine
misses, moon orientation, local day/night, weather codes, measured text fit,
module readiness, explicit rotation opt-in, JSON versions/units, unknown/stale
data, sample rejection, rate boundaries/DST and provider failures.

No Pi or dashboard deployment is implicit. The published scene branch preserves
main's existing output/scaling architecture and excludes the original development
checkout's unrelated hardware experiments. A later deployment must follow the
existing backed-up Pi 4 deployment procedure. See ADR 0022.
