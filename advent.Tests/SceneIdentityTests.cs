using advent.Data.Home;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public sealed class SceneIdentityTests
{
    [Theory]
    [InlineData(CatLocation.Indoors)]
    [InlineData(CatLocation.Outdoors)]
    internal void CoatsStayWithTheCorrectCatInEitherLocation(CatLocation location)
    {
        var now = NewSceneCapture.FixtureTime;
        var data = NewSceneCapture.HomeData(now) with
        {
            Cats = [new HomeCat("barney", location, now), new HomeCat("beaker", location, now)]
        };
        var scene = new TwoCatsScene(new NewSceneCapture.HomeFixture(data), new NewSceneCapture.FixedClock(now));
        scene.Activate();
        scene.Elapsed(TimeSpan.FromSeconds(2));
        using var image = new Image<Rgba32>(64, 32);
        scene.Draw(image);

        var white = new Rgba32(230, 233, 225);
        var silver = new Rgba32(159, 168, 174);
        var brown = new Rgba32(133, 104, 69);
        var black = new Rgba32(31, 30, 28);
        Assert.True(Count(image, 0, white) >= 20);
        Assert.True(Count(image, 0, silver) >= 30);
        Assert.Equal(0, Count(image, 0, brown));
        Assert.Equal(0, Count(image, 33, white));
        Assert.True(Count(image, 33, brown) >= 25);
        Assert.True(Count(image, 33, black) >= 15);
        Assert.Equal(0, Count(image, 33, silver));
    }

    [Fact]
    public void CambridgeNameboardHasNoWireOrTrainOccludingItsGlyphsAtTheStop()
    {
        var color = new Rgba32(218, 227, 216);
        Assert.InRange(RailDmiText.MeasureWidth("CAMBRIDGE"), 1, 44);
        using var expected = new Image<Rgba32>(64, 32);
        RailDmiText.Draw(expected, "CAMBRIDGE", 9, 12, color);
        var scene = new NightTrainScene();
        scene.Activate();
        scene.Elapsed(TimeSpan.FromSeconds(10));
        using var actual = new Image<Rgba32>(64, 32);
        scene.Draw(actual);
        for (var y = 12; y < 17; y++)
        for (var x = 9; x < 53; x++)
            if (expected[x, y] == color) Assert.Equal(color, actual[x, y]);
    }

    [Fact]
    public void DoorsOnlyOpenAtRestAndCloseBeforeTheSignalClears()
    {
        for (var step = 0; step < 200; step++)
        {
            var time = step / 10.0;
            if (!NightTrainScene.DoorsOpen(time)) continue;
            Assert.Equal(15, NightTrainScene.FrontPosition(time), 8);
            Assert.Equal(NightTrainScene.FrontPosition(time), NightTrainScene.FrontPosition(time + .01), 8);
            Assert.False(NightTrainScene.DepartureSignalClear(time));
        }
        Assert.False(NightTrainScene.DoorsOpen(14));
        Assert.True(NightTrainScene.DepartureSignalClear(14));
        Assert.True(NightTrainScene.DepartureSignalClear(15.5));
        Assert.False(NightTrainScene.DepartureSignalClear(17));
    }

    [Fact]
    public void TrainBrakesStopsThenEntirelyLeavesBeforeTheStoryEnds()
    {
        Assert.True(NightTrainScene.FrontPosition(3) > 64);
        Assert.True(NightTrainScene.FrontPosition(4) - NightTrainScene.FrontPosition(5) >
                    NightTrainScene.FrontPosition(7) - NightTrainScene.FrontPosition(8));
        Assert.Equal(NightTrainScene.FrontPosition(9), NightTrainScene.FrontPosition(15));
        Assert.True(NightTrainScene.FrontPosition(19.9) + NightTrainScene.TrainLength < 0);
    }

    private static int Count(Image<Rgba32> image, int left, Rgba32 color)
    {
        var count = 0;
        for (var y = 8; y < 24; y++)
        for (var x = left; x < left + 31; x++)
            if (image[x, y] == color) count++;
        return count;
    }
}
