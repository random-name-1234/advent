using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class OrbitalScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

    private static readonly Orbiter[] Orbiters =
    [
        new Orbiter(8.5f, 4.8f, 1.45f, 0.4f, 17, new Rgba32(106, 178, 255)),
        new Orbiter(11.5f, 6.1f, -0.88f, 1.8f, 20, new Rgba32(255, 158, 92)),
        new Orbiter(14.2f, 7.4f, 0.52f, -0.6f, 22, new Rgba32(138, 255, 196)),
        new Orbiter(6.2f, 3.6f, -1.82f, 2.2f, 13, new Rgba32(238, 128, 255))
    ];

    private TimeSpan elapsedThisScene;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Orbital";

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
        var centerX = Width / 2f;
        var centerY = Height / 2f + 1f;

        DrawNebulaBackground(img, t);
        DrawEventHorizon(img, centerX, centerY, t);

        for (var i = 0; i < Orbiters.Length; i++)
            DrawOrbiter(img, Orbiters[i], centerX, centerY, t);

        DrawCore(img, centerX, centerY, t);
    }

    private static void DrawNebulaBackground(Image<Rgba32> img, float time)
    {
        for (var y = 0; y < Height; y++)
        {
            var depth = y / (float)(Height - 1);
            for (var x = 0; x < Width; x++)
            {
                var nebula = 0.5f + 0.5f * MathF.Sin(x * 0.14f + y * 0.21f + time * 0.48f);
                var aurora = 0.5f + 0.5f * MathF.Cos(x * 0.08f - y * 0.16f - time * 0.32f);

                var r = 6f + depth * 8f + nebula * 8f + aurora * 6f;
                var g = 5f + depth * 4f + nebula * 5f + aurora * 4f;
                var b = 14f + depth * 19f + nebula * 24f + aurora * 10f;

                var starHash = Hash(x, y, 19);
                if ((starHash & 255) == 0)
                {
                    var twinkle = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(time * 2.8f + (starHash & 31)));
                    r += 120f * twinkle;
                    g += 140f * twinkle;
                    b += 180f * twinkle;
                }

                img[x, y] = new Rgba32(ToByte(r), ToByte(g), ToByte(b));
            }
        }
    }

    private static void DrawEventHorizon(Image<Rgba32> img, float centerX, float centerY, float time)
    {
        for (var deg = 0; deg < 360; deg += 10)
        {
            var theta = deg * MathF.PI / 180f;
            var radius = 10.4f + 0.9f * MathF.Sin(theta * 3f + time * 1.35f);
            var x = (int)MathF.Round(centerX + MathF.Cos(theta) * radius);
            var y = (int)MathF.Round(centerY + MathF.Sin(theta) * radius * 0.63f);
            BlendPixel(img, x, y, new Rgba32(88, 96, 184));
        }
    }

    private static void DrawOrbiter(Image<Rgba32> img, Orbiter orbiter, float centerX, float centerY, float time)
    {
        for (var step = orbiter.TrailSteps; step >= 0; step--)
        {
            var sampleTime = time - step * 0.075f;
            var (x, y) = GetOrbiterPosition(orbiter, centerX, centerY, sampleTime);
            var trail = 1f - step / (float)(orbiter.TrailSteps + 1);
            var intensity = step == 0 ? 1f : trail * 0.72f;
            BlendPixel(img, (int)MathF.Round(x), (int)MathF.Round(y), Scale(orbiter.Color, intensity));
        }

        var (orbiterX, orbiterY) = GetOrbiterPosition(orbiter, centerX, centerY, time);
        var px = (int)MathF.Round(orbiterX);
        var py = (int)MathF.Round(orbiterY);
        BlendPixel(img, px, py, Scale(orbiter.Color, 1f));
        BlendPixel(img, px - 1, py, Scale(orbiter.Color, 0.35f));
        BlendPixel(img, px + 1, py, Scale(orbiter.Color, 0.35f));
        BlendPixel(img, px, py - 1, Scale(orbiter.Color, 0.35f));
        BlendPixel(img, px, py + 1, Scale(orbiter.Color, 0.35f));
    }

    private static (float X, float Y) GetOrbiterPosition(Orbiter orbiter, float centerX, float centerY, float time)
    {
        var theta = orbiter.Phase + time * orbiter.AngularSpeed;
        var wobbleX = MathF.Sin(time * 1.72f + orbiter.Phase * 1.3f) * 0.75f;
        var wobbleY = MathF.Cos(time * 1.31f + orbiter.Phase * 0.9f) * 0.55f;

        var x = centerX + MathF.Cos(theta) * orbiter.RadiusX + wobbleX;
        var y = centerY + MathF.Sin(theta) * orbiter.RadiusY + wobbleY;
        return (x, y);
    }

    private static void DrawCore(Image<Rgba32> img, float centerX, float centerY, float time)
    {
        var corePulse = 0.7f + 0.3f * MathF.Sin(time * 4.4f);

        for (var y = (int)centerY - 10; y <= (int)centerY + 10; y++)
        {
            if ((uint)y >= Height) continue;

            for (var x = (int)centerX - 14; x <= (int)centerX + 14; x++)
            {
                if ((uint)x >= Width) continue;

                var dx = x - centerX;
                var dy = (y - centerY) * 1.24f;
                var dist = MathF.Sqrt(dx * dx + dy * dy);
                if (dist > 10.8f) continue;

                var inner = Math.Clamp(1f - dist / 8.4f, 0f, 1f);
                var rim = SmoothStep(7.1f, 9.8f, dist);
                var swirl = 0.5f + 0.5f * MathF.Sin(dist * 2.25f - time * 3.9f + dx * 0.24f);

                var r = 20f + 128f * inner * corePulse + 78f * rim;
                var g = 22f + 74f * inner + 42f * rim;
                var b = 76f + 136f * swirl * inner + 80f * rim;
                BlendPixel(img, x, y, new Rgba32(ToByte(r), ToByte(g), ToByte(b)));
            }
        }

        DrawDisc(img, (int)MathF.Round(centerX), (int)MathF.Round(centerY), 2, new Rgba32(254, 248, 170));
    }

    private static void DrawDisc(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        var r2 = radius * radius;
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            if ((uint)y >= Height) continue;
            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if ((uint)x >= Width) continue;
                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= r2) BlendPixel(img, x, y, color);
            }
        }
    }

    private static void BlendPixel(Image<Rgba32> img, int x, int y, Rgba32 source)
    {
        if ((uint)x >= Width || (uint)y >= Height) return;

        var current = img[x, y];
        var r = Math.Min(255, current.R + source.R);
        var g = Math.Min(255, current.G + source.G);
        var b = Math.Min(255, current.B + source.B);
        img[x, y] = new Rgba32((byte)r, (byte)g, (byte)b);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static int Hash(int x, int y, int z)
    {
        unchecked
        {
            var h = x * 374761393 + y * 668265263 + z * 982451653;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    private static Rgba32 Scale(Rgba32 color, float factor)
    {
        var f = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(ToByte(color.R * f), ToByte(color.G * f), ToByte(color.B * f));
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private readonly record struct Orbiter(
        float RadiusX,
        float RadiusY,
        float AngularSpeed,
        float Phase,
        int TrailSteps,
        Rgba32 Color);
}
