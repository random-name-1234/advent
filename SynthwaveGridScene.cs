using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class SynthwaveGridScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;

    private const int HorizonY = 11;
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

    private TimeSpan elapsedThisScene;

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => false;

    public string Name => "Synthwave Grid";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        HidesTime = true;
        IsActive = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive) return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        var t = (float)elapsedThisScene.TotalSeconds;
        DrawSkyGradient(img, t);
        DrawSun(img, t);
        DrawGrid(img, t);
    }

    private static void DrawSkyGradient(Image<Rgba32> img, float time)
    {
        var pulse = 0.5f + 0.5f * MathF.Sin(time * 0.9f);
        for (var y = 0; y <= HorizonY; y++)
        {
            var p = y / (float)Math.Max(1, HorizonY);
            var r = ClampToByte(34 + (1f - p) * 20f);
            var g = ClampToByte(6 + pulse * 8f);
            var b = ClampToByte(70 + p * 105f);
            for (var x = 0; x < Width; x++) img[x, y] = new Rgba32(r, g, b);
        }
    }

    private static void DrawSun(Image<Rgba32> img, float time)
    {
        var centerX = Width / 2;
        var centerY = HorizonY + 2;
        var radius = 8f + 0.3f * MathF.Sin(time);
        var radiusSquared = radius * radius;

        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var dx = x - centerX;
            var dy = y - centerY;
            if (dx * dx + dy * dy > radiusSquared) continue;

            if (y > centerY && (y - centerY) % 2 == 0) continue;

            var p = (y - (centerY - radius)) / Math.Max(1f, radius * 2f);
            var r = ClampToByte(255);
            var g = ClampToByte(120 + 60 * (1f - p));
            var b = ClampToByte(70 + 70 * (1f - p));
            img[x, y] = new Rgba32(r, g, b);
        }
    }

    private static void DrawGrid(Image<Rgba32> img, float time)
    {
        var offset = time * 18f % 6f;

        for (var y = HorizonY + 1; y < Height; y++)
        {
            var depth = (y - HorizonY) / (float)Math.Max(1, Height - HorizonY);
            var brightness = ClampToByte(120 + depth * 100f);
            img[Width / 2, y] = new Rgba32(180, 40, brightness);
        }

        for (var x = 0; x < Width; x += 7)
        {
            var rel = (x - Width / 2f) / (Width / 2f);
            for (var y = HorizonY + 1; y < Height; y++)
            {
                var depth = (y - HorizonY) / (float)Math.Max(1, Height - HorizonY);
                var projectedX = (int)Math.Round(Width / 2f + rel * depth * (Width / 2f));
                if (projectedX >= 0 && projectedX < Width)
                {
                    var lineBrightness = ClampToByte(90 + depth * 120f);
                    img[projectedX, y] = new Rgba32(170, 40, lineBrightness);
                }
            }
        }

        for (var y = HorizonY + 1; y < Height; y++)
        {
            var row = y + offset;
            if (Math.Abs(row % 6f) > 0.55f) continue;

            var depth = (y - HorizonY) / (float)Math.Max(1, Height - HorizonY);
            var widthAtDepth = (int)Math.Round(depth * (Width / 2f));
            var left = Math.Max(0, Width / 2 - widthAtDepth);
            var right = Math.Min(Width - 1, Width / 2 + widthAtDepth);
            var rowBrightness = ClampToByte(80 + depth * 135f);

            for (var x = left; x <= right; x++) img[x, y] = new Rgba32(155, 35, rowBrightness);
        }
    }

    private static byte ClampToByte(float value)
    {
        return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
    }
}