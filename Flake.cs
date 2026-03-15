using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class Flake
{
    private readonly Random random;
    private TimeSpan offsetElapsed;
    private Vector2 offsetFrom;
    private Vector2 offsetTo;
    private TimeSpan offsetTransitionTime;

    public Flake(Vector2 startPosition, float speed, Rgba32 color, float width)
    {
        random = new Random();
        offsetTo = new Vector2(0, 0);
        ResetOffset();
        Position = startPosition;
        Speed = speed;
        Color = color;
        Width = width;
    }

    public Vector2 Position { get; set; }
    public float Speed { get; }
    public Rgba32 Color { get; set; }
    public float Width { get; set; }

    public bool CanBeRemoved => Position.Y < -1.0;

    public void Elapsed(TimeSpan timeSpan)
    {
        var delta = new Vector2(0, -Speed * (float)timeSpan.TotalSeconds);
        offsetElapsed += timeSpan;
        var offsetProgress =
            (float)Math.Min(1.0, offsetElapsed.TotalMilliseconds / offsetTransitionTime.TotalMilliseconds);
        var currentOffset =
            new Vector2(offsetFrom.X * (1.0f - offsetProgress), offsetFrom.Y * (1.0f - offsetProgress)) +
            new Vector2(offsetTo.X * offsetProgress, offsetTo.Y * offsetProgress);
        if (offsetProgress >= 1.0f) ResetOffset();

        Position += delta + currentOffset;
        var v = new Vector2(1, 1);
    }

    private void ResetOffset()
    {
        offsetFrom = offsetTo;
        offsetTo = new Vector2((float)random.NextDouble() * 0.25f - 0.125f,
            (float)random.NextDouble() * 0.25f - 0.125f);
        offsetTransitionTime = TimeSpan.FromSeconds(0.5 + 0.5 * random.NextDouble());
        offsetElapsed = TimeSpan.Zero;
    }
}