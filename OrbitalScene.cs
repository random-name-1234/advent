using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class OrbitalScene : ISpecialScene
{
    private const float DaysPerSecond = 10f;
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);
    private static readonly DateTime J2000Utc = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Planet[] Planets =
    [
        new Planet("Mercury", 87.9691, 252.25084, 5.0f, 3.1f, 1, new Rgba32(196, 204, 214)),
        new Planet("Venus", 224.70069, 181.97973, 8.0f, 5.0f, 1, new Rgba32(255, 208, 142)),
        new Planet("Earth", 365.25636, 100.46435, 11.0f, 6.9f, 2, new Rgba32(96, 178, 255)),
        new Planet("Mars", 686.97959, 355.45332, 14.0f, 8.7f, 1, new Rgba32(255, 138, 92)),
        new Planet("Jupiter", 4332.589, 34.40438, 18.0f, 11.0f, 2, new Rgba32(244, 196, 144)),
        new Planet("Saturn", 10759.22, 49.94432, 21.0f, 13.0f, 2, new Rgba32(250, 222, 132))
    ];

    private TimeSpan elapsedThisScene;
    private double daysSinceJ2000AtActivation;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Orbital";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        daysSinceJ2000AtActivation = (DateTime.UtcNow - J2000Utc).TotalDays;
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

        var time = (float)elapsedThisScene.TotalSeconds;
        var cx = Width / 2f;
        var cy = Height / 2f;
        var simulatedDays = daysSinceJ2000AtActivation + time * DaysPerSecond;

        DrawBackground(img, time);
        DrawOrbitRings(img, cx, cy, time);
        DrawSun(img, cx, cy, time);
        DrawPlanets(img, cx, cy, simulatedDays, time);
    }

    private static void DrawBackground(Image<Rgba32> img, float time)
    {
        for (var y = 0; y < Height; y++)
        {
            var depth = y / (float)(Height - 1);
            var row = new Rgba32(
                ToByte(4f + depth * 6f),
                ToByte(7f + depth * 7f),
                ToByte(16f + depth * 18f));

            for (var x = 0; x < Width; x++)
                img[x, y] = row;
        }

        for (var i = 0; i < 18; i++)
        {
            var x = (Hash(i, 11) % Width + Width) % Width;
            var y = (Hash(i, 29) % Height + Height) % Height;
            var pulse = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(time * 2.2f + i * 0.8f));
            img[x, y] = Scale(new Rgba32(210, 224, 255), pulse);
        }
    }

    private static void DrawOrbitRings(Image<Rgba32> img, float cx, float cy, float time)
    {
        for (var i = 0; i < Planets.Length; i++)
        {
            var planet = Planets[i];
            var brightness = 0.14f + 0.03f * MathF.Sin(time * 0.9f + i * 0.7f);
            var ringColor = Scale(new Rgba32(146, 168, 214), brightness);

            for (var deg = 0; deg < 360; deg += 18)
            {
                var theta = DegreesToRadians(deg);
                var x = (int)MathF.Round(cx + MathF.Cos(theta) * planet.OrbitRadiusX);
                var y = (int)MathF.Round(cy + MathF.Sin(theta) * planet.OrbitRadiusY);
                BlendPixel(img, x, y, ringColor);
            }
        }
    }

    private static void DrawSun(Image<Rgba32> img, float cx, float cy, float time)
    {
        var centerX = (int)MathF.Round(cx);
        var centerY = (int)MathF.Round(cy);
        var glow = 0.82f + 0.18f * MathF.Sin(time * 4f);

        DrawDisc(img, centerX, centerY, 2, Scale(new Rgba32(255, 222, 118), glow));
        DrawDisc(img, centerX, centerY, 1, new Rgba32(255, 248, 200));

        BlendPixel(img, centerX - 3, centerY, new Rgba32(255, 184, 92));
        BlendPixel(img, centerX + 3, centerY, new Rgba32(255, 184, 92));
        BlendPixel(img, centerX, centerY - 2, new Rgba32(255, 184, 92));
        BlendPixel(img, centerX, centerY + 2, new Rgba32(255, 184, 92));
    }

    private static void DrawPlanets(Image<Rgba32> img, float cx, float cy, double simulatedDays, float time)
    {
        for (var i = 0; i < Planets.Length; i++)
        {
            var planet = Planets[i];
            var angleDegrees = NormalizeDegrees(planet.MeanLongitudeAtJ2000Degrees +
                                                simulatedDays * (360.0 / planet.OrbitalPeriodDays));
            var angle = DegreesToRadians((float)angleDegrees);

            var x = cx + MathF.Cos(angle) * planet.OrbitRadiusX;
            var y = cy + MathF.Sin(angle) * planet.OrbitRadiusY;

            var trailingDays = simulatedDays - 2.2;
            var tailDegrees = NormalizeDegrees(planet.MeanLongitudeAtJ2000Degrees +
                                               trailingDays * (360.0 / planet.OrbitalPeriodDays));
            var tailAngle = DegreesToRadians((float)tailDegrees);
            var tx = cx + MathF.Cos(tailAngle) * planet.OrbitRadiusX;
            var ty = cy + MathF.Sin(tailAngle) * planet.OrbitRadiusY;

            BlendPixel(img, (int)MathF.Round(tx), (int)MathF.Round(ty), Scale(planet.Color, 0.3f));

            var pulse = 0.82f + 0.18f * MathF.Sin(time * 3.4f + i * 0.8f);
            DrawPlanetDot(img, (int)MathF.Round(x), (int)MathF.Round(y), planet.SizePixels, Scale(planet.Color, pulse));

            if (planet.Name == "Saturn")
            {
                BlendPixel(img, (int)MathF.Round(x) - 2, (int)MathF.Round(y), Scale(planet.Color, 0.42f));
                BlendPixel(img, (int)MathF.Round(x) + 2, (int)MathF.Round(y), Scale(planet.Color, 0.42f));
            }
        }
    }

    private static void DrawPlanetDot(Image<Rgba32> img, int x, int y, int sizePixels, Rgba32 color)
    {
        BlendPixel(img, x, y, color);
        if (sizePixels < 2)
            return;

        BlendPixel(img, x - 1, y, Scale(color, 0.45f));
        BlendPixel(img, x + 1, y, Scale(color, 0.45f));
        BlendPixel(img, x, y - 1, Scale(color, 0.45f));
        BlendPixel(img, x, y + 1, Scale(color, 0.45f));
    }

    private static void DrawDisc(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        var radiusSquared = radius * radius;
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            var dx = x - centerX;
            var dy = y - centerY;
            if (dx * dx + dy * dy <= radiusSquared)
                BlendPixel(img, x, y, color);
        }
    }

    private static void BlendPixel(Image<Rgba32> img, int x, int y, Rgba32 source)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return;

        var current = img[x, y];
        var r = Math.Min(255, current.R + source.R);
        var g = Math.Min(255, current.G + source.G);
        var b = Math.Min(255, current.B + source.B);
        img[x, y] = new Rgba32((byte)r, (byte)g, (byte)b);
    }

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static int Hash(int seed, int salt)
    {
        unchecked
        {
            var h = seed * 374761393 + salt * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    private static Rgba32 Scale(Rgba32 color, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(
            ToByte(color.R * clamped),
            ToByte(color.G * clamped),
            ToByte(color.B * clamped));
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private readonly record struct Planet(
        string Name,
        double OrbitalPeriodDays,
        double MeanLongitudeAtJ2000Degrees,
        float OrbitRadiusX,
        float OrbitRadiusY,
        int SizePixels,
        Rgba32 Color);
}
