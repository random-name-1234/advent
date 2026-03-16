using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class BonkersParadeScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

    private readonly Random random = new();
    private readonly List<Blob> blobs = new();
    private readonly List<Spark> sparks = new();
    private TimeSpan elapsedThisScene;
    private float sparkSpawnAccumulator;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Bonkers Parade";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        sparkSpawnAccumulator = 0f;
        IsActive = true;
        HidesTime = true;

        blobs.Clear();
        sparks.Clear();
        for (var i = 0; i < 6; i++)
        {
            blobs.Add(new Blob
            {
                X = 8 + (float)random.NextDouble() * 48f,
                Y = 8 + (float)random.NextDouble() * 16f,
                Vx = (float)(random.NextDouble() * 24 - 12),
                Vy = (float)(random.NextDouble() * 20 - 10),
                HuePhase = (float)random.NextDouble() * MathF.PI * 2f
            });
        }
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

        var dt = Math.Clamp((float)timeSpan.TotalSeconds, 0f, 0.2f);
        UpdateBlobs(dt);
        UpdateSparks(dt);
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        var t = (float)elapsedThisScene.TotalSeconds;
        DrawBackground(img, t);
        DrawRibbons(img, t);
        DrawSparks(img);
        DrawBlobs(img, t);
        DrawWarpCells(img, t);
    }

    private void UpdateBlobs(float dt)
    {
        for (var i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            blob.X += blob.Vx * dt;
            blob.Y += blob.Vy * dt;
            blob.HuePhase += dt * 1.7f;

            if (blob.X < 4f)
            {
                blob.X = 4f;
                blob.Vx = MathF.Abs(blob.Vx) * (0.85f + (float)random.NextDouble() * 0.25f);
            }
            else if (blob.X > Width - 5f)
            {
                blob.X = Width - 5f;
                blob.Vx = -MathF.Abs(blob.Vx) * (0.85f + (float)random.NextDouble() * 0.25f);
            }

            if (blob.Y < 4f)
            {
                blob.Y = 4f;
                blob.Vy = MathF.Abs(blob.Vy) * (0.85f + (float)random.NextDouble() * 0.25f);
            }
            else if (blob.Y > Height - 5f)
            {
                blob.Y = Height - 5f;
                blob.Vy = -MathF.Abs(blob.Vy) * (0.85f + (float)random.NextDouble() * 0.25f);
            }

            blobs[i] = blob;
        }
    }

    private void UpdateSparks(float dt)
    {
        sparkSpawnAccumulator += dt;
        while (sparkSpawnAccumulator >= 0.035f)
        {
            sparkSpawnAccumulator -= 0.035f;
            SpawnSpark();
        }

        for (var i = sparks.Count - 1; i >= 0; i--)
        {
            var spark = sparks[i];
            spark.Age += dt;
            if (spark.Age >= spark.Lifetime)
            {
                sparks.RemoveAt(i);
                continue;
            }

            spark.X += spark.Vx * dt;
            spark.Y += spark.Vy * dt;
            spark.Vy += 18f * dt;
            sparks[i] = spark;
        }
    }

    private void SpawnSpark()
    {
        var blob = blobs[random.Next(blobs.Count)];
        var hue = blob.HuePhase + (float)(random.NextDouble() * 1.7 - 0.85);

        sparks.Add(new Spark
        {
            X = blob.X,
            Y = blob.Y,
            Vx = (float)(random.NextDouble() * 26 - 13),
            Vy = (float)(-random.NextDouble() * 20),
            Age = 0f,
            Lifetime = 0.7f + (float)random.NextDouble() * 0.8f,
            Color = Palette(hue, 1f)
        });

        if (sparks.Count > 180)
            sparks.RemoveRange(0, sparks.Count - 180);
    }

    private static void DrawBackground(Image<Rgba32> img, float time)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var a = MathF.Sin(x * 0.28f + time * 2.4f);
                var b = MathF.Cos(y * 0.37f - time * 1.9f);
                var c = MathF.Sin((x + y) * 0.18f + time * 2.9f);
                var mix = (a + b + c) / 3f;
                var phase = mix * 1.8f + time * 0.4f;
                img[x, y] = Scale(Palette(phase, 1f), 0.28f);
            }
        }
    }

    private static void DrawRibbons(Image<Rgba32> img, float time)
    {
        for (var band = 0; band < 3; band++)
        {
            var phase = time * (1.9f + band * 0.35f) + band * 1.2f;
            for (var x = 0; x < Width; x++)
            {
                var yFloat = Height / 2f +
                             MathF.Sin(x * 0.2f + phase) * (5f + band) +
                             MathF.Cos(x * 0.07f - phase * 1.3f) * 2.6f;
                var y = (int)MathF.Round(yFloat);
                BlendPixel(img, x, y, Scale(Palette(phase + x * 0.06f, 1f), 0.8f));
                BlendPixel(img, x, y + 1, Scale(Palette(phase + x * 0.06f + 0.7f, 1f), 0.36f));
            }
        }
    }

    private void DrawSparks(Image<Rgba32> img)
    {
        for (var i = 0; i < sparks.Count; i++)
        {
            var spark = sparks[i];
            var life = 1f - spark.Age / spark.Lifetime;
            var c = Scale(spark.Color, life);
            var x = (int)MathF.Round(spark.X);
            var y = (int)MathF.Round(spark.Y);
            BlendPixel(img, x, y, c);
            BlendPixel(img, x, y + 1, Scale(c, 0.3f));
        }
    }

    private void DrawBlobs(Image<Rgba32> img, float time)
    {
        for (var i = 0; i < blobs.Count; i++)
        {
            var blob = blobs[i];
            var body = Palette(blob.HuePhase + time * 0.45f, 1f);
            var x = (int)MathF.Round(blob.X);
            var y = (int)MathF.Round(blob.Y);

            BlendPixel(img, x, y, body);
            BlendPixel(img, x - 1, y, Scale(body, 0.82f));
            BlendPixel(img, x + 1, y, Scale(body, 0.82f));
            BlendPixel(img, x, y - 1, Scale(body, 0.82f));
            BlendPixel(img, x, y + 1, Scale(body, 0.82f));
            BlendPixel(img, x - 1, y - 1, Scale(body, 0.38f));
            BlendPixel(img, x + 1, y - 1, Scale(body, 0.38f));
            BlendPixel(img, x - 1, y + 1, Scale(body, 0.38f));
            BlendPixel(img, x + 1, y + 1, Scale(body, 0.38f));
            BlendPixel(img, x - 2, y, Scale(body, 0.18f));
            BlendPixel(img, x + 2, y, Scale(body, 0.18f));

            var eyeColor = new Rgba32(10, 10, 14);
            SetPixel(img, x - 1, y - 1, eyeColor);
            SetPixel(img, x + 1, y - 1, eyeColor);

            if (((int)((time + blob.HuePhase) * 7f) & 1) == 0)
            {
                SetPixel(img, x, y + 1, Scale(new Rgba32(250, 250, 250), 0.8f));
            }
            else
            {
                SetPixel(img, x - 1, y + 1, Scale(new Rgba32(250, 250, 250), 0.8f));
                SetPixel(img, x + 1, y + 1, Scale(new Rgba32(250, 250, 250), 0.8f));
            }
        }
    }

    private static void DrawWarpCells(Image<Rgba32> img, float time)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var wave = MathF.Sin(x * 0.42f + time * 2.1f) +
                       MathF.Cos(y * 0.53f - time * 2.8f) +
                       MathF.Sin((x - y) * 0.21f + time * 1.3f);
            var edge = 1f - Math.Clamp(MathF.Abs(wave) / 0.17f, 0f, 1f);
            if (edge <= 0.01f) continue;

            var phase = time * 0.85f + x * 0.05f - y * 0.04f;
            BlendPixel(img, x, y, Scale(Palette(phase, 1f), edge * 0.35f));
        }

        for (var portal = 0; portal < 3; portal++)
        {
            var centerX = 12f + portal * 20f + MathF.Sin(time * (0.8f + portal * 0.17f) + portal) * 5.5f;
            var centerY = 7f + portal * 8f + MathF.Cos(time * (1.1f + portal * 0.11f) + portal * 0.4f) * 2.8f;
            var radius = 2.8f + 1.3f * MathF.Sin(time * 2.6f + portal);
            var ringColor = Scale(Palette(time * 1.1f + portal * 1.7f, 1f), 0.45f);

            for (var deg = 0; deg < 360; deg += 30)
            {
                var theta = deg * MathF.PI / 180f;
                var px = (int)MathF.Round(centerX + MathF.Cos(theta) * radius);
                var py = (int)MathF.Round(centerY + MathF.Sin(theta) * radius);
                BlendPixel(img, px, py, ringColor);
            }
        }
    }

    private static Rgba32 Palette(float phase, float saturation)
    {
        var r = 0.5f + 0.5f * MathF.Sin(phase + 0f);
        var g = 0.5f + 0.5f * MathF.Sin(phase + 2.1f);
        var b = 0.5f + 0.5f * MathF.Sin(phase + 4.2f);
        saturation = Math.Clamp(saturation, 0f, 1f);

        return new Rgba32(
            ToByte((60f + r * 195f) * saturation),
            ToByte((50f + g * 205f) * saturation),
            ToByte((66f + b * 189f) * saturation));
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

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x >= Width || (uint)y >= Height) return;
        img[x, y] = color;
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

    private struct Blob
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float HuePhase;
    }

    private struct Spark
    {
        public float X;
        public float Y;
        public float Vx;
        public float Vy;
        public float Age;
        public float Lifetime;
        public Rgba32 Color;
    }
}
