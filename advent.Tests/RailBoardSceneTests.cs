using System;
using System.Reflection;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class RailBoardSceneTests
{
    [Fact]
    public void RailBoardScene_RendersCompactPages_AndRunsLongerThanTwentySeconds()
    {
        ResetCache();

        var snapshot = new RailBoardScene.RailSceneSnapshot(
            new RailBoardScene.RailStationSnapshot(
                "CAM",
                "Cambridge",
                [
                    CreateService("18:12", "LONDON KX", "KGX", "P5", "ON TIME", new Rgba32(210, 188, 144),
                        "Great Northern", "CALLS ROYSTON, STEVENAGE, FINSBURY PK"),
                    CreateService("18:19", "LIV ST", "LST", "P1", "+5", new Rgba32(255, 184, 64),
                        "Greater Anglia", "CALLS AUD, BSH, BXB")
                ],
                [],
                [
                    new RailBoardScene.RailAlertSnapshot("Signal failure between Foxton and Royston. Delays expected.", 3)
                ],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                false),
            new RailBoardScene.RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [],
                [
                    CreateService("18:28", "CAMBRIDGE", "CBG", "P8", "+4", new Rgba32(255, 184, 64),
                        "Great Northern", "CALLS FINSBURY PK, STEVENAGE, ROYSTON"),
                    CreateService("18:34", "PETERBORO", "PBO", "--", "ON TIME", new Rgba32(210, 188, 144),
                        "LNER", "CALLS STEVENAGE")
                ],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                false),
            new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero));

        var scene = new RailBoardScene((_, _) => Task.FromResult(snapshot));

        scene.Activate();
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromMilliseconds(50));
        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.True(CountLitPixels(canvas) > 0);

        scene.Elapsed(TimeSpan.FromSeconds(25));
        Assert.True(scene.IsActive);

        scene.Elapsed(TimeSpan.FromSeconds(40));
        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Fact]
    public void RailBoardScene_ShowsGracefulNoDataBoard_WhenStationDataIsUnavailable()
    {
        ResetCache();

        var snapshot = new RailBoardScene.RailSceneSnapshot(
            new RailBoardScene.RailStationSnapshot(
                "CAM",
                "Cambridge",
                [],
                [],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                true),
            new RailBoardScene.RailStationSnapshot(
                "KGX",
                "London Kings Cross",
                [],
                [],
                [],
                new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero),
                true),
            new DateTimeOffset(2026, 3, 16, 18, 10, 0, TimeSpan.Zero));

        var scene = new RailBoardScene((_, _) => Task.FromResult(snapshot));

        scene.Activate();
        scene.Elapsed(TimeSpan.FromMilliseconds(50));

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.True(CountLitPixels(canvas) > 0);
    }

    private static RailBoardScene.RailServiceSnapshot CreateService(
        string scheduledText,
        string locationText,
        string locationCode,
        string platformText,
        string statusText,
        Rgba32 statusColor,
        string operatorText,
        string callingText)
    {
        return new RailBoardScene.RailServiceSnapshot(
            scheduledText,
            locationText,
            locationCode,
            platformText,
            statusText,
            statusColor,
            operatorText,
            callingText,
            $"{locationText}  •  {operatorText}  •  {callingText}",
            DateTimeOffset.Parse($"2026-03-16T{scheduledText}:00+00:00"));
    }

    private static int CountLitPixels(Image<Rgba32> image)
    {
        var litPixels = 0;
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];
            if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)
                litPixels++;
        }

        return litPixels;
    }

    private static void ResetCache()
    {
        var snapshotField = typeof(RailBoardScene).GetField("cachedSnapshot", BindingFlags.Static | BindingFlags.NonPublic);
        var updatedAtField = typeof(RailBoardScene).GetField("cacheUpdatedAtUtc", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(snapshotField);
        Assert.NotNull(updatedAtField);
        snapshotField!.SetValue(null, null);
        updatedAtField!.SetValue(null, DateTimeOffset.MinValue);
    }
}
