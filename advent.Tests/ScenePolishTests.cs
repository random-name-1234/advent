using System.Collections;
using System.Reflection;
using advent.Data.Home;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public sealed class ScenePolishTests
{
    [Fact]
    public void SynthwaveKeepsHorizontalLinesOnEveryNeighbouringFrame()
    {
        var scene = new SynthwaveGridScene();
        scene.Activate();
        for (var tick = 0; tick < 900; tick++)
        {
            scene.Elapsed(TimeSpan.FromMilliseconds(20));
            using var frame = Render(scene);
            Assert.Contains(Pixels(frame), p => p.R == 155 && p.G == 35);
        }
    }

    [Theory]
    [InlineData(true, 18)]
    [InlineData(false, 10)]
    public void CatTransitionMeetsItsRestingPosition(bool indoors, int rest)
    {
        Assert.Equal(rest, TwoCatsScene.CatCenter(indoors, true, 3.99));
        Assert.Equal(rest, TwoCatsScene.CatCenter(indoors, false, 4.01));
        var previous = TwoCatsScene.CatCenter(indoors, true, 0);
        for (var i = 1; i <= 200; i++)
        {
            var current = TwoCatsScene.CatCenter(indoors, i < 200, i * .02);
            Assert.InRange(Math.Abs(current - previous), 0, 1);
            previous = current;
        }
    }

    [Fact]
    public void CatMotionRequiresARealRecentChangeNotSameLocationOrFutureData()
    {
        var now = NewSceneCapture.FixtureTime;
        var home = NewSceneCapture.HomeData(now);
        byte[] Frame(HomeCat cat)
        {
            var scene = new TwoCatsScene(new NewSceneCapture.HomeFixture(home with { Cats = [cat] }), new NewSceneCapture.FixedClock(now));
            scene.Activate();
            scene.Elapsed(TimeSpan.FromSeconds(1));
            using var frame = Render(scene);
            return InformationLayoutTests.Pixels(frame);
        }
        var stationary = new HomeCat("barney", CatLocation.Indoors, now);
        Assert.Equal(Frame(stationary), Frame(stationary with { PreviousLocation = CatLocation.Indoors }));
        Assert.Equal(Frame(stationary), Frame(stationary with { PreviousLocation = CatLocation.Outdoors, ChangedAt = now.AddMinutes(1) }));
        Assert.NotEqual(Frame(stationary), Frame(stationary with { PreviousLocation = CatLocation.Outdoors }));
    }

    [Theory]
    [InlineData(20f)]
    [InlineData(62f)]
    [InlineData(63f)]
    public void StarTailsAreBehindLeftMovingHeadsAndClipAtTheRightEdge(float x)
    {
        var scene = new StarfieldParallaxScene();
        scene.Activate();
        var stars = (IList)Field(scene, "stars");
        stars.Clear();
        var type = typeof(StarfieldParallaxScene).GetNestedType("Star", BindingFlags.NonPublic)!;
        stars.Add(Activator.CreateInstance(type, x, 10f, 16f, (byte)3, 250f, 0f));
        using var first = Render(scene);
        Assert.True(first[(int)x, 10].R > 100);
        Assert.Equal(0, first[(int)x - 1, 10].R);
        if (x < 63) Assert.InRange(first[(int)x + 1, 10].R, 1, 100);
        scene.Elapsed(TimeSpan.FromMilliseconds(100));
        using var later = Render(scene);
        Assert.True(later[(int)(x - 1.6f), 10].R > 100);
    }

    [Fact]
    public void AquariumWavesActuallyUseAllThreePixelOffsets()
    {
        foreach (var frequency in new[] { .6, .9, 1 })
            Assert.Equal(new[] { -1, 0, 1 }, Enumerable.Range(0, 1000)
                .Select(i => AquariumScene.Bob(i * .02 * frequency)).Distinct().Order());
        var scene = new AquariumScene();
        scene.Activate();
        scene.Elapsed(TimeSpan.FromSeconds(2));
        using var image = Render(scene);
        // The first fish has bobbed down to y=11; its eye is at y=10.
        Assert.Equal(new Rgba32(6, 11, 17), image[6, 10]);
    }

    [Fact]
    public void SolarAndOrbitalActivationCanBeReproduced()
    {
        var clock = new NewSceneCapture.FixedClock(NewSceneCapture.FixtureTime);
        ISpecialScene[] scenes = [new OrbitalScene(clock), new SunriseSunsetScene(timeProvider: clock)];
        foreach (var scene in scenes)
        {
            scene.Activate();
            scene.Elapsed(TimeSpan.FromSeconds(10));
            using var first = Render(scene);
            scene.Activate();
            scene.Elapsed(TimeSpan.FromSeconds(10));
            using var second = Render(scene);
            Assert.Equal(Pixels(first), Pixels(second));
        }
    }

    [Fact]
    public void SunHaloNeverDarkensTheExistingSky()
    {
        var sky = new Rgba32(74, 155, 230);
        using var image = new Image<Rgba32>(64, 32, sky);
        SunriseSunsetScene.DrawHalo(image, 32, 10, 5, 1);
        foreach (var pixel in Pixels(image))
        {
            Assert.True(pixel.R >= sky.R && pixel.G >= sky.G && pixel.B >= sky.B);
        }
        Assert.True(image[37, 10].R > sky.R);
        Assert.Equal(sky, image[38, 10]);
    }

    [Fact]
    public void CityWindowsDoNotAllSwitchOnTheSameBeat()
    {
        var changes = new List<int>();
        for (var tick = 1; tick <= 200; tick++)
        {
            var count = 0;
            for (var i = 0; i < 6; i++)
            for (var row = 0; row < 2; row++)
            for (var col = 0; col < 2; col++)
                if (PixelCityScene.WindowLit(i, row, col, tick / 10d) !=
                    PixelCityScene.WindowLit(i, row, col, (tick - 1) / 10d)) count++;
            if (count > 0) changes.Add(count);
        }
        Assert.True(changes.Count > 8);
        Assert.All(changes, count => Assert.InRange(count, 1, 2));
    }

    [Fact]
    public void StormDiffersFromRainButLeavesJoineryOpaque()
    {
        var rain = new WeatherWindowScene(new NewSceneCapture.WeatherFixture(63));
        var storm = new WeatherWindowScene(new NewSceneCapture.WeatherFixture(95));
        rain.Activate(); storm.Activate();
        rain.Elapsed(TimeSpan.FromSeconds(3)); storm.Elapsed(TimeSpan.FromSeconds(3));
        using var a = Render(rain);
        using var b = Render(storm);
        Assert.NotEqual(Pixels(a), Pixels(b));
        for (var x = 0; x < 64; x++) Assert.Equal(a[x, 30], b[x, 30]);
        for (var y = 2; y < 28; y++) Assert.Equal(a[31, y], b[31, y]);
    }

    [Fact]
    public void AgileWindowLabelsFitAndMakeOvernightEndExplicit()
    {
        var now = NewSceneCapture.FixtureTime;
        foreach (var hours in new[] { 0, 6, 24, 48, 72 })
        foreach (var prefix in new[] { "FROM", "TO" })
        {
            var text = AgilePowerScene.WindowEndpoint(prefix, now.AddHours(hours));
            Assert.True(RailDmiText.MeasureWidth(text) <= 60, text);
            Assert.All(text, c => Assert.True(RailDmiText.HasGlyph(c)));
            var dates = AgilePowerScene.WindowDays(now.AddHours(hours), now.AddHours(hours + 6), now);
            Assert.InRange(RailDmiText.MeasureWidth(dates), 1, 60);
        }
        Assert.Equal("TOD-TOM", AgilePowerScene.WindowDays(now, now.AddHours(6), now));
        Assert.False(AgilePowerScene.ShowWindow(16.99, false));
        Assert.True(AgilePowerScene.ShowWindow(17, false));
        Assert.True(AgilePowerScene.ShowWindow(10, true));
    }

    [Fact]
    public void AgileGraphLabelsAndCurrentMarkerDoNotOverlapPrice()
    {
        var scene = LegibilityLabScene.CreateHomeSample(10);
        scene.Activate();
        using var image = Render(scene);
        Assert.Equal(new Rgba32(234, 239, 221), image[4, 18]);
        Assert.Contains(Enumerable.Range(27 * 64, 5 * 64), i => image[i % 64, i / 64].R > 100);
        Assert.Contains(Enumerable.Range(24 * 64, 3 * 64), i => image[i % 64, i / 64] == AgilePowerScene.BandColor("cheap"));
    }

    [Fact]
    public void LabHomeExamplesUseTheActualSceneRenderers()
    {
        for (var page = 7; page < LegibilityLabScene.SampleCount; page++)
        {
            var scene = LegibilityLabScene.CreateHomeSample(page);
            scene.Activate();
            scene.Elapsed(TimeSpan.FromSeconds(page == 11 ? 12 : page == 12 ? 18 : 2));
            using var expected = Render(scene);
            using var actual = new Image<Rgba32>(64, 32);
            LegibilityLabScene.DrawSample(actual, page, TimeSpan.FromSeconds(2));
            Assert.Equal(Pixels(expected), Pixels(actual));
        }
    }

    [Fact]
    public void TetrisInitialStackConsistsOfWholeDroppedPieces()
    {
        var scene = new TetrisScene();
        typeof(TetrisScene).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, new Random(1234));
        scene.Activate();
        var grid = (int[,])Field(scene, "grid");
        Assert.InRange(grid.Cast<int>().Count(value => value != 0), 48, 80);
        foreach (var group in grid.Cast<int>().Where(value => value != 0).GroupBy(value => value))
            Assert.Equal(0, group.Count() % 4);
        for (var y = 0; y < 28; y++) Assert.Equal(0, grid[9, y]);
        Assert.True(Enumerable.Range(0, 16).All(y => Enumerable.Range(0, 10).All(x => grid[x, y] == 0)));
    }

    [Fact]
    public void TetrisClearsMultipleRowsWithoutLeavingOneBehind()
    {
        var scene = new TetrisScene();
        var grid = (int[,])Field(scene, "grid");
        for (var x = 0; x < 10; x++) { grid[x, 26] = 2; grid[x, 27] = 3; }
        grid[4, 25] = 7;
        var rows = (HashSet<int>)Field(scene, "flashingRows");
        rows.UnionWith([26, 27]);
        typeof(TetrisScene).GetMethod("CollapseFlashingRows", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, null);
        Assert.Equal(7, grid[4, 27]);
        Assert.Equal(1, grid.Cast<int>().Count(value => value != 0));
    }

    private static object Field(object scene, string name) => scene.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(scene)!;
    private static Image<Rgba32> Render(ISpecialScene scene)
    {
        var frame = new Image<Rgba32>(64, 32, new Rgba32(0, 0, 0));
        scene.Draw(frame);
        return frame;
    }
    private static Rgba32[] Pixels(Image<Rgba32> image)
    {
        var pixels = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);
        return pixels;
    }
}
