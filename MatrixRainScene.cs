using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class MatrixRainScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);
    private readonly List<RainColumn> columns = new();

    private readonly Random random = new();

    private TimeSpan elapsedThisScene;

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => false;

    public string Name => "Matrix Rain";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        HidesTime = true;
        IsActive = true;
        columns.Clear();

        for (var x = 0; x < Width; x += 2) columns.Add(CreateColumn(x));
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

        var dt = (float)timeSpan.TotalSeconds;
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            column.HeadY += column.Speed * dt;
            if (column.HeadY - column.TrailLength > Height + 1) columns[i] = CreateColumn(column.X);
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            var headY = (int)Math.Round(column.HeadY);

            for (var t = 0; t < column.TrailLength; t++)
            {
                var y = headY - t;
                if (y < 0 || y >= Height) continue;

                var fade = 1f - t / (float)column.TrailLength;
                var green = ClampToByte(30f + 190f * fade);
                var red = ClampToByte(8f + 16f * fade);
                var blue = ClampToByte(8f + 28f * fade);
                img[column.X, y] = new Rgba32(red, green, blue);
            }

            if (headY >= 0 && headY < Height) img[column.X, headY] = new Rgba32(190, 255, 200);
        }
    }

    private RainColumn CreateColumn(int x)
    {
        return new RainColumn
        {
            X = x,
            HeadY = -random.Next(0, Height),
            Speed = 12f + (float)random.NextDouble() * 18f,
            TrailLength = random.Next(6, 14)
        };
    }

    private static byte ClampToByte(float value)
    {
        return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
    }

    private sealed class RainColumn
    {
        public int X { get; set; }
        public float HeadY { get; set; }
        public float Speed { get; set; }
        public int TrailLength { get; set; }
    }
}