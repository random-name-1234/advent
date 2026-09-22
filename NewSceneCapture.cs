using System.Net;
using advent.Data.Home;
using advent.Data.Weather;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

// Used only by the explicit offline capture command. Never registered as live data.
internal static class NewSceneCapture
{
    internal static readonly DateTimeOffset FixtureTime = new(2026, 9, 21, 19, 10, 0, TimeSpan.Zero);

    internal sealed class FixedClock(DateTimeOffset utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
    }

    internal sealed class HomeFixture(HomeSnapshot snapshot) : IHomeSnapshotSource
    {
        public bool TryGetSnapshot(out HomeSnapshot value) { value = snapshot; return true; }
    }

    internal sealed class WeatherFixture(int code = 63, bool day = true) : IWeatherSnapshotSource
    {
        public bool TryGetSnapshot(out WeatherSnapshot snapshot)
        {
            snapshot = new WeatherSnapshot(12, 10, 8, code, day, []);
            return true;
        }
    }

    internal static HomeSnapshot HomeData(DateTimeOffset now)
    {
        var start = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute < 30 ? 0 : 30, 0, now.Offset);
        double[] prices = [24.6, 22, 19, 12, 8, -1.2, 3, 9, 14, 22, 29, 34];
        var slots = prices.Select((p, i) => new AgileSlot(start.AddMinutes(i * 30), start.AddMinutes((i + 1) * 30), p,
            p < 15 ? "cheap" : p >= 30 ? "expensive" : "normal")).ToArray();
        return new HomeSnapshot(now, now,
            [new HomeCat("barney", CatLocation.Indoors, now.AddMinutes(-18)), new HomeCat("beaker", CatLocation.Outdoors, now.AddMinutes(-6))],
            now, slots[0], slots, new CheapWindow(slots[3].StartsAt, slots[8].EndsAt));
    }

    internal static IReadOnlyList<(string Slug, Func<ISpecialScene> Create, string Caption)> Scenes()
    {
        var clock = new FixedClock(FixtureTime);
        var home = new HomeFixture(HomeData(FixtureTime));
        return
        [
            ("night-train", () => new NightTrainScene(), "A silver-and-blue British electric train, yellow cab and lit windows at an unnamed brick-built station with a green canopy. Doors close before the departure signal clears."),
            ("aquarium", () => new AquariumScene(), "Three fish with independent paths, gently swaying plants and rising bubbles."),
            ("pixel-city", () => new PixelCityScene(clock), "A double-decker passes the night-time streetscape. Day/night follows local solar times."),
            ("breakout", () => new BreakoutScene(), "Destructible bricks, fixed-step ball collisions and a fallible paddle. No scoreboard."),
            ("weather-window", () => new WeatherWindowScene(new WeatherFixture()), "Fixture: rainy daylight. Production uses the existing cached weather feed."),
            ("moonlit-landscape", () => new MoonlitLandscapeScene(clock), "Calculated approximate phase, slow clouds, layered hills and rippling water."),
            ("two-cats", () => new TwoCatsScene(home, clock), "Photo-inspired tabbies: fluffy grey-and-white Barney and dark brown-and-black Beaker. DEMO DATA: Barney in, Beaker out."),
            ("agile-power", () => new AgilePowerScene(home, clock), "DEMO DATA: current VAT-inclusive p/kWh and six-hour chart; then the next cheap window.")
        ];
    }

    internal static void WriteGallery(string directory)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        using var sheet = new Image<Rgba32>(64 * 4, 42 * 2, PixelArt.Color(3, 7, 11));
        var cards = new List<string>();
        var index = 0;
        foreach (var (slug, create, caption) in Scenes())
        {
            var scene = create();
            scene.Activate();
            using var animation = new Image<Rgba32>(64, 32);
            animation.Metadata.GetGifMetadata().RepeatCount = 0;
            for (var frame = 0; frame < 200; frame++)
            {
                using var image = new Image<Rgba32>(64, 32);
                scene.Draw(image);
                image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 10;
                animation.Frames.AddFrame(image.Frames.RootFrame);
                if (frame == 80)
                {
                    image.SaveAsPng(Path.Combine(directory, slug + "-64x32.png"));
                    using var large = image.Clone(ctx => ctx.Resize(128, 64, KnownResamplers.NearestNeighbor));
                    large.SaveAsPng(Path.Combine(directory, slug + "-128x64.png"));
                    var x = index % 4 * 64;
                    var y = index / 4 * 42;
                    sheet.Mutate(ctx => ctx.DrawImage(image, new Point(x, y + 10), 1));
                    RailDmiText.Draw(sheet, RailDmiText.TrimToWidth(scene.Name, 62), x + 1, y + 2, PixelArt.Color(187, 205, 194));
                }
                scene.Elapsed(TimeSpan.FromMilliseconds(100));
            }
            animation.Frames.RemoveFrame(0);
            animation.SaveAsGif(Path.Combine(directory, slug + ".gif"));
            cards.Add($"""
                <article><div class="number">{++index:00} / NEW SCENE</div><h2>{WebUtility.HtmlEncode(scene.Name)}</h2>
                <div class="screen"><img src="{slug}.gif" width="64" height="32" alt="Animated {WebUtility.HtmlEncode(scene.Name)} scene"></div>
                <p>{WebUtility.HtmlEncode(caption)}</p><nav><a href="{slug}-64x32.png">Native 64 x 32</a><a href="{slug}-128x64.png">Exact 2x capture</a></nav></article>
                """);
        }
        sheet.Mutate(ctx => ctx.Resize(sheet.Width * 4, sheet.Height * 4, KnownResamplers.NearestNeighbor));
        sheet.SaveAsPng(Path.Combine(directory, "contact-sheet.png"));
        WriteVariants(directory);
        File.WriteAllText(Path.Combine(directory, "index.html"), """
            <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Advent / Eight small worlds</title><style>
            :root{color-scheme:dark;--ink:#ece4cd;--muted:#acae9f;--accent:#e4b75a}*{box-sizing:border-box}
            body{margin:0;background:radial-gradient(ellipse at top right,#20383a,transparent 60%),#101b1d;color:var(--ink);font:16px Georgia,serif}
            header,main,footer{max-width:1200px;margin:auto;padding:32px}header{padding-top:64px;border-bottom:1px solid #46514c}
            .eyebrow,.number,nav,button{font:11px 'Courier New',monospace;letter-spacing:.1em}.eyebrow,.number{color:var(--accent)}
            h1{font-size:clamp(42px,7vw,80px);font-weight:normal;letter-spacing:-.06em;line-height:1;margin:22px 0}
            header p{max-width:670px;font-size:18px;line-height:1.6;color:var(--muted)}.notice{border-left:3px solid var(--accent);padding:8px 16px}
            main{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:44px 32px}h2{font-weight:normal;font-size:29px;margin:12px 0 22px}
            .screen{position:relative;background:#000;border:8px solid #071013;box-shadow:0 8px 30px #0005;aspect-ratio:2}
            .screen img{width:100%;height:100%;image-rendering:pixelated;object-fit:contain;display:block}article p{min-height:48px;line-height:1.6;color:var(--muted)}
            nav{display:flex;gap:20px}a{color:var(--accent);text-underline-offset:4px}footer{line-height:1.8;border-top:1px solid #46514c;color:var(--muted)}
            @media(max-width:650px){main{grid-template-columns:1fr}header,main,footer{padding:24px}h1{margin-top:28px}article p{min-height:0}}
            </style><header><div class="eyebrow">ADVENT / PI 4 / 64 x 32 LANDSCAPE</div><h1>Eight small worlds.</h1>
            <p>Original pixel scenes for the existing panel. Each animation loops a full twenty-second sequence. Existing scenes are untouched.</p>
            <p class="notice">Offline review. All home and weather values are fixtures, not live readings. Time-dependent scenes use 21 September 2026, 20:10 Europe/London.</p>
            </header><main>
            """ + string.Join('\n', cards) + """
            </main><footer>Every drawing is native 64 x 32. The 128 x 64 files show only the existing exact 2x presenter, not a redesign.<br>
            <a href="contact-sheet.png">Contact sheet</a> / <a href="variants.png">Weather, moon-phase and data-state variants</a><br>
            New scenes remain manual-only unless ADVENT_NEW_SCENES_IN_ROTATION=true is explicitly enabled.</footer></html>
            """);
        Console.WriteLine($"Wrote eight-scene fixture gallery to {directory}");
    }

    private static void WriteVariants(string directory)
    {
        var epoch = new DateTimeOffset(2000, 1, 6, 18, 14, 0, TimeSpan.Zero);
        var home = HomeData(FixtureTime);
        var clock = new FixedClock(FixtureTime);
        (string Name, ISpecialScene Scene)[] cases =
        [
            ("SUN", new WeatherWindowScene(new WeatherFixture(0))),
            ("SNOW", new WeatherWindowScene(new WeatherFixture(73))),
            ("FOG", new WeatherWindowScene(new WeatherFixture(45))),
            ("CITY DAY", new PixelCityScene(new FixedClock(FixtureTime.AddHours(-8)))),
            ("NEW MOON", new MoonlitLandscapeScene(new FixedClock(epoch))),
            ("WAXING", new MoonlitLandscapeScene(new FixedClock(epoch.AddDays(29.530588 / 4)))),
            ("FULL MOON", new MoonlitLandscapeScene(new FixedClock(epoch.AddDays(29.530588 / 2)))),
            ("WANING", new MoonlitLandscapeScene(new FixedClock(epoch.AddDays(29.530588 * .75)))),
            ("UNKNOWN CAT", new TwoCatsScene(new HomeFixture(home with { Cats = [home.Cats[0], home.Cats[1] with { Location = CatLocation.Unknown }] }), clock)),
            ("CAT ARRIVES", new TwoCatsScene(new HomeFixture(home with { Cats = [home.Cats[0] with { ChangedAt = FixtureTime, PreviousLocation = CatLocation.Outdoors }, home.Cats[1]] }), clock)),
            ("NEGATIVE", new AgilePowerScene(new HomeFixture(home with { Current = home.Current! with { Price = -3.4, Band = "cheap" } }), clock)),
            ("NO WINDOW", new AgilePowerScene(new HomeFixture(home with { NextCheapWindow = null }), clock))
        ];
        using var sheet = new Image<Rgba32>(256, 126, PixelArt.Color(3, 7, 11));
        for (var i = 0; i < cases.Length; i++)
        {
            var (name, scene) = cases[i];
            scene.Activate();
            scene.Elapsed(TimeSpan.FromSeconds(name == "NO WINDOW" ? 12 : 2));
            using var frame = new Image<Rgba32>(64, 32);
            scene.Draw(frame);
            var x = i % 4 * 64;
            var y = i / 4 * 42;
            sheet.Mutate(ctx => ctx.DrawImage(frame, new Point(x, y + 10), 1));
            RailDmiText.Draw(sheet, name, x + 1, y + 2, PixelArt.Color(187, 205, 194));
        }
        sheet.Mutate(ctx => ctx.Resize(1024, 504, KnownResamplers.NearestNeighbor));
        sheet.SaveAsPng(Path.Combine(directory, "variants.png"));
    }
}
