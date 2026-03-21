using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class MetaballsScene : ISpecialScene
{
    private const float Aspect = Width / (float)Height;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(19);

    private TimeSpan elapsedThisScene;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Metaballs";

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

        var x1 = 0.5f + 0.22f * MathF.Sin(t * 0.75f + 0.2f);
        var y1 = 0.5f + 0.18f * MathF.Cos(t * 0.92f + 0.8f);
        var x2 = 0.5f + 0.24f * MathF.Cos(t * 0.63f + 1.4f);
        var y2 = 0.5f + 0.19f * MathF.Sin(t * 0.84f + 0.3f);
        var x3 = 0.5f + 0.20f * MathF.Sin(t * 1.06f + 2.6f);
        var y3 = 0.5f + 0.17f * MathF.Cos(t * 0.70f + 2.1f);
        var x4 = 0.5f + 0.26f * MathF.Cos(t * 0.54f + 4.1f);
        var y4 = 0.5f + 0.16f * MathF.Sin(t * 0.96f + 1.9f);

        var background = BuildColor(t, 0.00f, 4f, 16f, 8f, 30f, 20f, 62f);
        var glow = BuildColor(t, 0.85f, 28f, 110f, 90f, 220f, 125f, 255f);
        var interior = BuildColor(t, 1.40f, 176f, 255f, 208f, 255f, 220f, 255f);

        for (var y = 0; y < Height; y++)
        {
            var ny = y / (Height - 1f);
            for (var x = 0; x < Width; x++)
            {
                var nx = x / (Width - 1f);

                var field = Metaball(nx, ny, x1, y1, 0.17f);
                field += Metaball(nx, ny, x2, y2, 0.16f);
                field += Metaball(nx, ny, x3, y3, 0.15f);
                field += Metaball(nx, ny, x4, y4, 0.18f);

                var shell = SmoothStep(0.58f, 1.10f, field);
                var contour = 1f - Clamp01(MathF.Abs(field - 1.02f) / 0.23f);
                var core = SmoothStep(1.42f, 2.45f, field);
                contour *= 1f - core * 0.65f;

                var color = Lerp(background, glow, shell * 0.45f + contour * 0.75f);
                color = Lerp(color, interior, core);
                img[x, y] = color;
            }
        }
    }

    private static float Metaball(float x, float y, float centerX, float centerY, float radius)
    {
        var dx = (x - centerX) * Aspect;
        var dy = y - centerY;
        var distSquared = dx * dx + dy * dy + 0.0028f;
        return (radius * radius) / distSquared;
    }

    private static Rgba32 BuildColor(float time, float phaseOffset, float rMin, float rMax, float gMin, float gMax,
        float bMin, float bMax)
    {
        var phase = time * 0.18f + phaseOffset;
        return new Rgba32(
            OscByte(phase + 0.0f, rMin, rMax),
            OscByte(phase + 2.1f, gMin, gMax),
            OscByte(phase + 4.2f, bMin, bMax));
    }

    private static Rgba32 Lerp(Rgba32 from, Rgba32 to, float t)
    {
        t = Clamp01(t);
        return new Rgba32(
            ToByte(from.R + (to.R - from.R) * t),
            ToByte(from.G + (to.G - from.G) * t),
            ToByte(from.B + (to.B - from.B) * t));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float Clamp01(float value)
    {
        return MathF.Max(0f, MathF.Min(1f, value));
    }

    private static byte OscByte(float phase, float min, float max)
    {
        var wave = 0.5f + 0.5f * MathF.Sin(phase);
        return ToByte(min + (max - min) * wave);
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }
}
