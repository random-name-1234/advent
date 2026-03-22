using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class StarfieldParallaxScene : ISpecialScene
{
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

    private readonly Random random = new();
    private readonly List<Star> stars = new();

    private TimeSpan elapsedThisScene;

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => false;

    public string Name => "Starfield Parallax";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        HidesTime = true;
        IsActive = true;
        stars.Clear();

        AddLayer(18, 1);
        AddLayer(14, 2);
        AddLayer(10, 3);
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive) return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
            return;
        }

        var deltaSeconds = (float)timeSpan.TotalSeconds;
        for (var i = 0; i < stars.Count; i++)
        {
            var star = stars[i];
            var x = star.X - star.Speed * deltaSeconds;
            if (x < -1f)
            {
                stars[i] = CreateStar(star.Layer, Width + (float)random.NextDouble() * 6f);
                continue;
            }

            stars[i] = star with { X = x };
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        var t = (float)elapsedThisScene.TotalSeconds;
        foreach (var star in stars)
        {
            var x = (int)star.X;
            var y = (int)star.Y;
            if (x < 0 || x >= Width || y < 0 || y >= Height) continue;

            var twinkle = 0.7f + 0.3f * MathF.Sin(t * 7f + star.Phase);
            var brightness = ClampToByte(star.BaseBrightness * twinkle);

            // Occasional warm/cool star tints based on phase
            var phaseIdx = (int)(star.Phase * 3f) % 5;
            var color = phaseIdx switch
            {
                0 => new Rgba32(brightness, ClampToByte(brightness * 0.85f), ClampToByte(brightness * 0.7f)),
                1 => new Rgba32(ClampToByte(brightness * 0.8f), ClampToByte(brightness * 0.85f), brightness),
                _ => new Rgba32(brightness, brightness, ClampToByte(brightness * 0.95f))
            };
            img[x, y] = color;

            // Fast stars (layer 3) get a 2px trail
            if (star.Layer == 3 && x > 0)
            {
                var trail = ClampToByte(brightness * 0.4f);
                img[x - 1, y] = new Rgba32(trail, trail, trail);
                if (x > 1)
                {
                    var trail2 = ClampToByte(brightness * 0.15f);
                    img[x - 2, y] = new Rgba32(trail2, trail2, trail2);
                }
            }
            else if (star.Layer == 2 && x > 0)
            {
                var trail = ClampToByte(brightness * 0.25f);
                img[x - 1, y] = new Rgba32(trail, trail, trail);
            }
        }
    }

    private void AddLayer(int count, byte layer)
    {
        for (var i = 0; i < count; i++) stars.Add(CreateStar(layer, (float)random.NextDouble() * Width));
    }

    private Star CreateStar(byte layer, float x)
    {
        var speed = layer switch
        {
            1 => 7f + (float)random.NextDouble() * 2f,
            2 => 11f + (float)random.NextDouble() * 3f,
            _ => 15f + (float)random.NextDouble() * 4f
        };

        var brightness = layer switch
        {
            1 => 120f,
            2 => 185f,
            _ => 250f
        };

        return new Star(
            x,
            (float)random.NextDouble() * (Height - 1),
            speed,
            layer,
            brightness,
            (float)random.NextDouble() * MathF.PI * 2f);
    }

    private static byte ClampToByte(float value)
    {
        return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
    }

    private readonly record struct Star(
        float X,
        float Y,
        float Speed,
        byte Layer,
        float BaseBrightness,
        float Phase);
}