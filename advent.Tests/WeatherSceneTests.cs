using System;
using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class WeatherSceneTests
{
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
        var scene = new WeatherScene();
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
    public void Draw_DoesNotRenderBottomCenterIndicatorArtifacts()
    {
        var scene = new WeatherScene();
        SetBackingField(scene, "<IsActive>k__BackingField", true);
        SetPrivateField(scene, "snapshot", CreateSnapshot());

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.True(CountLitPixels(canvas, 22, 31, 20, 1) <= 3);
    }

    private static object CreateSnapshot()
    {
        var weatherSnapshotType = typeof(WeatherScene).GetNestedType("WeatherSnapshot", BindingFlags.NonPublic);
        var dailyForecastType = typeof(WeatherScene).GetNestedType("DailyForecast", BindingFlags.NonPublic);
        Assert.NotNull(weatherSnapshotType);
        Assert.NotNull(dailyForecastType);

        var forecasts = Array.CreateInstance(dailyForecastType!, 3);
        forecasts.SetValue(CreateNonPublicInstance(dailyForecastType!, "TODAY", 1, 15f, 9f), 0);
        forecasts.SetValue(CreateNonPublicInstance(dailyForecastType!, "TOM", 61, 12f, 7f), 1);
        forecasts.SetValue(CreateNonPublicInstance(dailyForecastType!, "WED", 3, 11f, 5f), 2);

        return CreateNonPublicInstance(weatherSnapshotType!, 10f, 1, true, forecasts);
    }

    private static object CreateNonPublicInstance(Type type, params object[] args)
    {
        var instance = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: args,
            culture: null);

        Assert.NotNull(instance);
        return instance!;
    }

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
