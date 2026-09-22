using advent.Data.Home;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public sealed class NewScenesTests
{
    public static IEnumerable<object[]> SceneIndices() => Enumerable.Range(0, 8).Select(i => new object[] { i });

    [Theory]
    [MemberData(nameof(SceneIndices))]
    public void StoriesAreDeterministicAnimatedOpaqueAndResettable(int index)
    {
        var create = NewSceneCapture.Scenes()[index].Create;
        var a = create();
        var b = create();
        Assert.False(a.IsActive);
        a.Activate(); b.Activate();
        var initial = Render(a);
        var time = index == 7 ? 12 : 8;
        a.Elapsed(TimeSpan.FromSeconds(time)); b.Elapsed(TimeSpan.FromSeconds(time));
        var later = Render(a);
        Assert.Equal(later, Render(b));
        Assert.False(initial.SequenceEqual(later));
        Assert.True(initial.Distinct().Count() >= 5);
        Assert.True(later.Distinct().Count() >= 3);
        Assert.All(later, pixel => Assert.Equal(255, pixel.A));
        a.Elapsed(TimeSpan.FromSeconds(20 - time));
        Assert.False(a.IsActive);
        Assert.False(a.HidesTime);
        a.Activate();
        Assert.Equal(initial, Render(a));
    }

    [Fact]
    public void BreakoutUsesFrameIndependentPhysicsAndActuallyDestroysBricksAndMisses()
    {
        var one = new BreakoutScene();
        var many = new BreakoutScene();
        one.Activate(); many.Activate();
        one.Elapsed(TimeSpan.FromSeconds(19.5));
        for (var i = 0; i < 195; i++) many.Elapsed(TimeSpan.FromMilliseconds(100));
        Assert.Equal(one.Ball.X, many.Ball.X, 8);
        Assert.Equal(one.Ball.Y, many.Ball.Y, 8);
        Assert.Equal(one.DestroyedBricks, many.DestroyedBricks);
        Assert.Equal(one.Misses, many.Misses);
        Assert.True(one.DestroyedBricks > 5);
        Assert.True(one.Misses > 0);
        Assert.InRange(one.Ball.X, 2, 61);
        Assert.InRange(one.Ball.Y, 2, 32);
    }

    [Theory]
    [MemberData(nameof(SceneIndices))]
    public void AllEightScenesUseExactTwoByTwoPresentation(int index)
    {
        var scene = NewSceneCapture.Scenes()[index].Create();
        scene.Activate();
        scene.Elapsed(TimeSpan.FromSeconds(8));
        using var logical = new Image<Rgba32>(64, 32);
        scene.Draw(logical);
        using var sink = new CaptureOutput();
        using var output = new ScalingMatrixOutput(sink, 64, 32, 128, 64);
        output.Present(logical);
        var physical = sink.Frame!;
        Assert.Equal(128, physical.Width);
        Assert.Equal(64, physical.Height);
        for (var y = 0; y < 64; y++)
        for (var x = 0; x < 128; x++) Assert.Equal(logical[x / 2, y / 2], physical[x, y]);
    }

    [Fact]
    public void VisibleDataScenesRespondToInvalidatedSnapshotsWithoutDoingIo()
    {
        var source = new MutableHomeSource { Snapshot = NewSceneCapture.HomeData(NewSceneCapture.FixtureTime) };
        var clock = new NewSceneCapture.FixedClock(NewSceneCapture.FixtureTime);
        ISpecialScene[] scenes = [new TwoCatsScene(source, clock), new AgilePowerScene(source, clock)];
        foreach (var scene in scenes) scene.Activate();
        var catsBefore = Render(scenes[0]);
        var agileBefore = Render(scenes[1]);
        source.Snapshot = null;
        Assert.False(catsBefore.SequenceEqual(Render(scenes[0])));
        Assert.False(agileBefore.SequenceEqual(Render(scenes[1])));
    }

    [Theory]
    [InlineData(0, "clear")]
    [InlineData(3, "cloud")]
    [InlineData(45, "fog")]
    [InlineData(67, "rain")]
    [InlineData(77, "snow")]
    [InlineData(86, "snow")]
    [InlineData(96, "storm")]
    [InlineData(-1, "unknown")]
    public void WeatherCodesHaveExplicitTreatments(int code, string expected) =>
        Assert.Equal(expected, WeatherWindowScene.Condition(code));

    [Fact]
    public void CityUsesLocalSunriseAndSunset()
    {
        var noon = new PixelCityScene(new NewSceneCapture.FixedClock(NewSceneCapture.FixtureTime.AddHours(-8)));
        var night = new PixelCityScene(new NewSceneCapture.FixedClock(NewSceneCapture.FixtureTime));
        noon.Activate(); night.Activate();
        Assert.False(noon.IsNight);
        Assert.True(night.IsNight);
        Assert.False(Render(noon).SequenceEqual(Render(night)));
    }

    [Fact]
    public void MoonPhaseHasCorrectEpochDirectionAndNegativeDateWrapping()
    {
        var epoch = new DateTimeOffset(2000, 1, 6, 18, 14, 0, TimeSpan.Zero);
        Assert.Equal(0, MoonlitLandscapeScene.CalculatePhase(epoch));
        Assert.Equal(.25, MoonlitLandscapeScene.CalculatePhase(epoch.AddDays(29.530588 * .25)), 7);
        Assert.Equal(.75, MoonlitLandscapeScene.CalculatePhase(epoch.AddDays(-29.530588 * .25)), 7);
        Assert.False(MoonlitLandscapeScene.IsLit(0, 0, 0));
        Assert.True(MoonlitLandscapeScene.IsLit(0, 0, .5));
        Assert.True(MoonlitLandscapeScene.IsLit(.5, 0, .25));
        Assert.False(MoonlitLandscapeScene.IsLit(-.5, 0, .25));
        Assert.True(MoonlitLandscapeScene.IsLit(-.5, 0, .75));
        Assert.False(MoonlitLandscapeScene.IsLit(.5, 0, .75));
    }

    [Fact]
    public void NewModuleIsManualByDefaultAndDataReadinessIsIndependent()
    {
        var context = new SceneModuleContext(9, "missing-images", null, null, null, false);
        var registrations = new NewSceneModule().RegisterScenes(context).ToArray();
        Assert.Equal(8, registrations.Length);
        Assert.All(registrations, r => Assert.False(r.IncludedInCycle));
        var catalog = SceneCatalog.Create([new NewSceneModule()], context);
        Assert.Equal(5, catalog.AvailableSceneNames.Count);
        Assert.Empty(catalog.CycleEntries);
        Assert.Equal(SceneSelectionStatus.Unavailable, catalog.SelectSceneByName("Two Cats").Status);
        Assert.Equal(SceneSelectionStatus.Unavailable, catalog.SelectSceneByName("Agile Power").Status);
        Assert.Equal(SceneSelectionStatus.Unavailable, catalog.SelectSceneByName("Weather Window").Status);
        var now = DateTimeOffset.UtcNow;
        var live = context with
        {
            WeatherSnapshotSource = new NewSceneCapture.WeatherFixture(),
            HomeSnapshotSource = new NewSceneCapture.HomeFixture(NewSceneCapture.HomeData(now)),
            NewScenesInRotation = true
        };
        catalog = SceneCatalog.Create([new NewSceneModule()], live);
        Assert.Equal(8, catalog.AvailableSceneNames.Count);
        Assert.Equal(8, catalog.CycleEntries.Count);
    }

    [Fact]
    public void ActivationRechecksSnapshotsAndDoesNotInventMissingHomeData()
    {
        var now = NewSceneCapture.FixtureTime;
        var expired = NewSceneCapture.HomeData(now.AddHours(-1));
        var source = new NewSceneCapture.HomeFixture(expired);
        var cats = new TwoCatsScene(source, new NewSceneCapture.FixedClock(now));
        var agile = new AgilePowerScene(source, new NewSceneCapture.FixedClock(now));
        cats.Activate(); agile.Activate();
        Assert.False(cats.IsActive);
        Assert.False(agile.IsActive);
    }

    [Theory]
    [InlineData("BARNEY", 31)]
    [InlineData("BEAKER", 31)]
    [InlineData("NO FIX", 31)]
    [InlineData("NO LIVE DATA", 60)]
    [InlineData("CHEAP POWER", 60)]
    [InlineData("CHEAP FROM", 60)]
    [InlineData("CHEAP UNTIL", 60)]
    [InlineData("TOMORROW", 60)]
    [InlineData("NO WINDOW", 60)]
    [InlineData("PUBLISHED", 60)]
    [InlineData("OUT OF RANGE", 60)]
    public void EveryNewInformationLabelFitsItsRegion(string text, int width) =>
        Assert.InRange(RailDmiText.MeasureWidth(text), 1, width);

    [Fact]
    public void NegativeZeroAndLargePricesAreCultureIndependent()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
            Assert.Equal("-3.4", AgilePowerScene.PriceText(-3.4));
            Assert.Equal("0.0", AgilePowerScene.PriceText(0));
            Assert.Equal("1234.5", AgilePowerScene.PriceText(1234.5));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    private static Rgba32[] Render(ISpecialScene scene)
    {
        using var frame = new Image<Rgba32>(64, 32);
        scene.Draw(frame);
        var pixels = new Rgba32[64 * 32];
        frame.CopyPixelDataTo(pixels);
        return pixels;
    }

    private sealed class CaptureOutput : IMatrixOutput
    {
        internal Image<Rgba32>? Frame;
        public string Name => "Test capture";
        public void Present(Image<Rgba32> image)
        {
            Frame?.Dispose();
            Frame = image.Clone();
        }
        public void Dispose() { Frame?.Dispose(); Frame = null; }
    }

    private sealed class MutableHomeSource : IHomeSnapshotSource
    {
        internal HomeSnapshot? Snapshot;
        public bool TryGetSnapshot(out HomeSnapshot snapshot)
        {
            snapshot = Snapshot!;
            return snapshot is not null;
        }
    }
}
