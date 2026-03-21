using System;
using System.Reflection;
using advent.Data.Weather;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class WeatherSceneTests
{
    [Fact]
    public void Activate_UsesPreparedSnapshotAndExpires()
    {
        var scene = new WeatherScene(new FixedWeatherSnapshotSource(CreateSnapshot()));
        scene.Activate();

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Elapsed(TimeSpan.FromMilliseconds(250));
        scene.Draw(canvas);

        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);
        Assert.True(CountLitPixels(canvas, 0, 0, 64, 32) > 0);

        scene.Elapsed(TimeSpan.FromSeconds(21));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Theory]
    [InlineData(1, "PART CLOUD")]
    [InlineData(45, "MIST")]
    [InlineData(63, "SHOWERS")]
    public void ConditionLabel_UsesCompactPanelFriendlyText(int weatherCode, string expected)
    {
        var method = typeof(WeatherScene).GetMethod("ConditionLabel", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var label = method!.Invoke(null, [weatherCode]);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void DrawForecastPanel_RendersHeaderBodyAndSummaryZones()
    {
        var scene = new WeatherScene(new FixedWeatherSnapshotSource(CreateSnapshot()));
        var drawMethod = typeof(WeatherScene).GetMethod("DrawForecastPanel", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(drawMethod);

        using var canvas = new Image<Rgba32>(64, 32);
        drawMethod!.Invoke(scene, [canvas, CreateSnapshot(), 0]);

        Assert.True(CountLitPixels(canvas, 0, 0, 64, 8) > 0);
        Assert.True(CountLitPixels(canvas, 0, 8, 64, 16) > 0);
        Assert.True(CountLitPixels(canvas, 0, 24, 64, 8) > 0);
        Assert.True(CountLitPixels(canvas, 0, 0, 64, 1) > 0);
        Assert.True(CountLitPixels(canvas, 0, 31, 64, 1) > 0);
    }

    [Fact]
    public void Draw_RendersBottomStripWithWeatherData()
    {
        var scene = new WeatherScene(new FixedWeatherSnapshotSource(CreateSnapshot()));
        SetBackingField(scene, "<IsActive>k__BackingField", true);
        SetPrivateField(scene, "snapshot", CreateSnapshot());

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        // Bottom strip (y 26-31) should have content: rain %, wind, hi/lo
        Assert.True(CountLitPixels(canvas, 0, 26, 64, 6) > 0);
    }

    private static WeatherSnapshot CreateSnapshot()
        => new(
            10f,
            8f,
            12f,
            1,
            true,
            [
                new DailyForecast("TODAY", 1, 15f, 9f, 20, 15f),
                new DailyForecast("TOM", 61, 12f, 7f, 80, 22f),
                new DailyForecast("WED", 3, 11f, 5f, 10, 8f)
            ]);

    private static void SetBackingField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private sealed class FixedWeatherSnapshotSource(WeatherSnapshot snapshot) : IWeatherSnapshotSource
    {
        public bool TryGetSnapshot(out WeatherSnapshot weatherSnapshot)
        {
            weatherSnapshot = snapshot;
            return true;
        }
    }

    private static int CountLitPixels(Image<Rgba32> image, int x, int y, int width, int height)
    {
        var litPixels = 0;
        for (var yy = y; yy < y + height; yy++)
        for (var xx = x; xx < x + width; xx++)
        {
            var pixel = image[xx, yy];
            if (pixel.R != 0 || pixel.G != 0 || pixel.B != 0)
                litPixels++;
        }

        return litPixels;
    }
}
