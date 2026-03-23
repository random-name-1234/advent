using System;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class SunriseSunsetSceneTests
{
    [Fact]
    public void CalculateSunTimes_UsesLongitude_WhenLatitudeIsConstant()
    {
        var localNow = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.Zero);

        var cambridge = SunriseSunsetScene.CalculateSunTimes(localNow, 52.2053, 0.1218);
        var farWest = SunriseSunsetScene.CalculateSunTimes(localNow, 52.2053, -5.0);

        Assert.True(farWest.SunriseHour > cambridge.SunriseHour);
        Assert.True(farWest.SunsetHour > cambridge.SunsetHour);
        Assert.True(farWest.SunriseHour - cambridge.SunriseHour > 0.2);
    }

    [Fact]
    public void CalculateSunTimes_RespectsLocalUtcOffset()
    {
        var utc = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.Zero);
        var britishSummerTime = new DateTimeOffset(2026, 6, 21, 12, 0, 0, TimeSpan.FromHours(1));

        var utcResult = SunriseSunsetScene.CalculateSunTimes(utc, 52.2053, 0.1218);
        var bstResult = SunriseSunsetScene.CalculateSunTimes(britishSummerTime, 52.2053, 0.1218);

        Assert.True(bstResult.SunriseHour > utcResult.SunriseHour + 0.9);
        Assert.True(bstResult.SunsetHour > utcResult.SunsetHour + 0.9);
    }

    [Fact]
    public void CaptureFrame_ReturnsDetachedSnapshot()
    {
        using var renderer = new SceneRenderer();
        renderer.Img[0, 0] = new Rgba32(255, 0, 0);

        using var snapshot = renderer.CaptureFrame();
        renderer.Img[0, 0] = new Rgba32(0, 0, 255);

        Assert.Equal(new Rgba32(255, 0, 0), snapshot[0, 0]);
    }
}
