using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class OrbitalScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;
    private const float DaysPerSecond = 28f;
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);
    private static readonly DateTime J2000Utc = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static readonly Planet[] Planets =
    [
        new Planet("Mercury", 87.9691, 252.25084, 4.5f, 2.8f, new Rgba32(190, 202, 210)),
        new Planet("Venus", 224.70069, 181.97973, 6.2f, 3.8f, new Rgba32(255, 204, 142)),
        new Planet("Earth", 365.25636, 100.46435, 8.0f, 4.9f, new Rgba32(92, 172, 255)),
        new Planet("Mars", 686.97959, 355.45332, 9.9f, 6.0f, new Rgba32(255, 136, 92)),
        new Planet("Jupiter", 4332.589, 34.40438, 12.0f, 7.2f, new Rgba32(240, 186, 138)),
        new Planet("Saturn", 10759.22, 49.94432, 14.0f, 8.3f, new Rgba32(250, 214, 128)),
        new Planet("Uranus", 30688.5, 313.23218, 16.0f, 9.4f, new Rgba32(152, 235, 236)),
        new Planet("Neptune", 60182.0, 304.88003, 18.0f, 10.6f, new Rgba32(114, 150, 255))
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

        var t = (float)elapsedThisScene.TotalSeconds;
        var cx = Width / 2f;
        var cy = Height / 2f + 1f;
        var simulatedDays = daysSinceJ2000AtActivation + t * DaysPerSecond;

        DrawBackground(img, t);
        DrawOrbitRings(img, t, cx, cy);
        DrawPlanets(img, simulatedDays, cx, cy, t);
        DrawSun(img, cx, cy, t);
    }

    private static void DrawBackground(Image<Rgba32> img, float time)
    {
        for (var y = 0; y < Height; y++)
        {
            var depth = y / (float)(Height - 1);
            for (var x = 0; x < Width; x++)
            {
                var nebula = 0.5f + 0.5f * MathF.Sin(x * 0.12f + y * 0.21f + time * 0.38f);
                var haze = 0.5f + 0.5f * MathF.Cos(x * 0.08f - y * 0.17f - time * 0.26f);

                var r = 6f + depth * 8f + nebula * 8f + haze * 4f;
                var g = 8f + depth * 6f + nebula * 4f + haze * 4f;
                var b = 18f + depth * 24f + nebula * 16f + haze * 8f;

                var hash = Hash(x, y, 41);
                if ((hash & 255) == 1)
                {
                    var twinkle = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(time * 3.1f + (hash & 63)));
                    r += 110f * twinkle;
                    g += 126f * twinkle;
                    b += 172f * twinkle;
                }

                img[x, y] = new Rgba32(ToByte(r), ToByte(g), ToByte(b));
            }
        }
    }

    private static void DrawOrbitRings(Image<Rgba32> img, float time, float cx, float cy)
    {
        for (var i = 0; i < Planets.Length; i++)
        {
            var planet = Planets[i];
            var ringColor = Scale(planet.Color, 0.22f + 0.05f * MathF.Sin(time * 0.8f + i * 0.7f));

            for (var deg = 0; deg < 360; deg += 8)
            {
                var theta = deg * MathF.PI / 180f;
                var x = (int)MathF.Round(cx + MathF.Cos(theta) * planet.OrbitRadiusX);
                var y = (int)MathF.Round(cy + MathF.Sin(theta) * planet.OrbitRadiusY);
                BlendPixel(img, x, y, ringColor);
            }
        }
    }

    private static void DrawPlanets(Image<Rgba32> img, double simulatedDays, float cx, float cy, float time)
    {
        for (var i = 0; i < Planets.Length; i++)
        {
            var planet = Planets[i];
            var angleDegrees = NormalizeDegrees(planet.MeanLongitudeAtJ2000Degrees +
                                                simulatedDays * (360.0 / planet.OrbitalPeriodDays));
            var angle = DegreesToRadians((float)angleDegrees);

            var x = cx + MathF.Cos(angle) * planet.OrbitRadiusX;
            var y = cy + MathF.Sin(angle) * planet.OrbitRadiusY;

            for (var step = 1; step <= 2; step++)
            {
                var trailingDays = simulatedDays - step * 3.3;
                var trailAngleDegrees = NormalizeDegrees(planet.MeanLongitudeAtJ2000Degrees +
                                                         trailingDays * (360.0 / planet.OrbitalPeriodDays));
                var trailAngle = DegreesToRadians((float)trailAngleDegrees);
                var tx = cx + MathF.Cos(trailAngle) * planet.OrbitRadiusX;
                var ty = cy + MathF.Sin(trailAngle) * planet.OrbitRadiusY;
                BlendPixel(img, (int)MathF.Round(tx), (int)MathF.Round(ty), Scale(planet.Color, 0.25f));
            }

            var px = (int)MathF.Round(x);
            var py = (int)MathF.Round(y);
            var pulse = 0.75f + 0.25f * MathF.Sin(time * 4.2f + i * 0.6f);
            BlendPixel(img, px, py, Scale(planet.Color, pulse));
            BlendPixel(img, px - 1, py, Scale(planet.Color, 0.22f));
            BlendPixel(img, px + 1, py, Scale(planet.Color, 0.22f));
            BlendPixel(img, px, py - 1, Scale(planet.Color, 0.22f));
            BlendPixel(img, px, py + 1, Scale(planet.Color, 0.22f));

            if (planet.Name == "Saturn")
            {
                BlendPixel(img, px - 2, py, Scale(planet.Color, 0.3f));
                BlendPixel(img, px + 2, py, Scale(planet.Color, 0.3f));
            }
        }
    }

    private static void DrawSun(Image<Rgba32> img, float cx, float cy, float time)
    {
        var centerX = (int)MathF.Round(cx);
        var centerY = (int)MathF.Round(cy);
        var coronaPulse = 0.75f + 0.25f * MathF.Sin(time * 5.4f);

        DrawDisc(img, centerX, centerY, 2, Scale(new Rgba32(255, 236, 140), coronaPulse));
        DrawDisc(img, centerX, centerY, 1, new Rgba32(255, 252, 188));

        for (var ray = 0; ray < 8; ray++)
        {
            var theta = ray * MathF.PI / 4f + time * 0.8f;
            var x = (int)MathF.Round(cx + MathF.Cos(theta) * 4f);
            var y = (int)MathF.Round(cy + MathF.Sin(theta) * 2.3f);
            BlendPixel(img, x, y, new Rgba32(255, 190, 96));
        }
    }

    private static void DrawDisc(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        var r2 = radius * radius;
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            var dx = x - centerX;
            var dy = y - centerY;
            if (dx * dx + dy * dy <= r2) BlendPixel(img, x, y, color);
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

    private static float DegreesToRadians(float degrees)
    {
        return degrees * MathF.PI / 180f;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
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

    private readonly record struct Planet(
        string Name,
        double OrbitalPeriodDays,
        double MeanLongitudeAtJ2000Degrees,
        float OrbitRadiusX,
        float OrbitRadiusY,
        Rgba32 Color);
}
