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
    {
        new(6.5f, 0.9f, 1.2f, new Rgba32(80, 140, 255)),
        new(10f, 0.55f, -0.4f, new Rgba32(255, 150, 70)),
        new(13.5f, 0.35f, 2.1f, new Rgba32(120, 255, 190))
    };

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
        var cx = Width / 2f;
        var cy = Height / 2f + 1f;

        DrawDisc(img, (int)cx, (int)cy, 3, new Rgba32(255, 235, 110));
        DrawDisc(img, (int)cx, (int)cy, 1, new Rgba32(255, 255, 180));

        for (var i = 0; i < Orbiters.Length; i++)
        {
            var orbiter = Orbiters[i];
            DrawOrbitPath(img, cx, cy, orbiter.Radius, new Rgba32(40, 40, 65));

            var theta = orbiter.Phase + t * orbiter.AngularSpeed;
            var x = cx + MathF.Cos(theta) * orbiter.Radius;
            var y = cy + MathF.Sin(theta) * orbiter.Radius * 0.62f;
            DrawDisc(img, (int)Math.Round(x), (int)Math.Round(y), 1, orbiter.Color);
        }
    }

    private static void DrawOrbitPath(Image<Rgba32> img, float cx, float cy, float radius, Rgba32 color)
    {
        for (var degrees = 0; degrees < 360; degrees += 18)
        {
            var theta = degrees * MathF.PI / 180f;
            var x = (int)Math.Round(cx + MathF.Cos(theta) * radius);
            var y = (int)Math.Round(cy + MathF.Sin(theta) * radius * 0.62f);
            if (x < 0 || x >= Width || y < 0 || y >= Height) continue;

            img[x, y] = color;
        }
    }

    private static void DrawDisc(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        var r2 = radius * radius;
        for (var y = centerY - radius; y <= centerY + radius; y++)
        {
            if (y < 0 || y >= Height) continue;

            for (var x = centerX - radius; x <= centerX + radius; x++)
            {
                if (x < 0 || x >= Width) continue;

                var dx = x - centerX;
                var dy = y - centerY;
                if (dx * dx + dy * dy <= r2) img[x, y] = color;
            }
        }
    }

    private readonly record struct Orbiter(
        float Radius,
        float AngularSpeed,
        float Phase,
        Rgba32 Color);
}