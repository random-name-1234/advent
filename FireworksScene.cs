using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class FireworksScene : ISpecialScene
{
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(12);

    private readonly List<Particle> particles = new();
    private readonly Random random = new();

    private TimeSpan elapsedThisScene;
    private TimeSpan timeToNextBurst;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Fireworks";

    public void Activate()
    {
        particles.Clear();
        elapsedThisScene = TimeSpan.Zero;
        timeToNextBurst = TimeSpan.Zero;
        IsActive = true;
        HidesTime = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        elapsedThisScene += timeSpan;

        if (elapsedThisScene <= SceneDuration)
        {
            timeToNextBurst -= timeSpan;
            while (timeToNextBurst <= TimeSpan.Zero)
            {
                SpawnBurst();
                timeToNextBurst += TimeSpan.FromMilliseconds(350 + random.Next(400));
            }
        }

        var dt = Math.Max(0.0, timeSpan.TotalSeconds);
        const float gravity = 20f;

        for (var i = particles.Count - 1; i >= 0; i--)
        {
            var particle = particles[i];
            particle.Age += dt;
            if (particle.Age >= particle.LifeSeconds)
            {
                particles.RemoveAt(i);
                continue;
            }

            particle.VelocityY += gravity * (float)dt;
            particle.X += particle.VelocityX * (float)dt;
            particle.Y += particle.VelocityY * (float)dt;
            particles[i] = particle;
        }

        if (elapsedThisScene > SceneDuration && particles.Count == 0)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive)
            return;

        for (var i = 0; i < particles.Count; i++)
        {
            var particle = particles[i];
            var x = (int)Math.Round(particle.X);
            var y = (int)Math.Round(particle.Y);

            if ((uint)x >= Width || (uint)y >= Height)
                continue;

            var lifeRatio = 1f - (float)(particle.Age / particle.LifeSeconds);
            lifeRatio = Math.Clamp(lifeRatio, 0f, 1f);

            var spark = ScaleColor(particle.Color, lifeRatio);
            BlendPixel(img, x, y, spark);

            // Larger sparks get a 2x2 core
            if (particle.Size >= 2)
            {
                BlendPixel(img, x + 1, y, spark);
                BlendPixel(img, x, y + 1, ScaleColor(spark, 0.7f));
                BlendPixel(img, x + 1, y + 1, ScaleColor(spark, 0.5f));
            }

            // Trail pixel behind the spark
            var trailY = y + 1;
            if (particle.Size < 2 && (uint)trailY < Height)
            {
                var trail = ScaleColor(particle.Color, lifeRatio * 0.35f);
                BlendPixel(img, x, trailY, trail);
            }
        }
    }

    private void SpawnBurst()
    {
        var cx = 8 + random.Next(48);
        var cy = 4 + random.Next(12);
        var color = RandomBurstColor();
        var sparkCount = 35 + random.Next(25);

        // Bright flash at burst centre
        particles.Add(new Particle
        {
            X = cx,
            Y = cy,
            VelocityX = 0,
            VelocityY = 0,
            LifeSeconds = 0.15,
            Age = 0,
            Color = new Rgba32(255, 255, 255),
            Size = 2
        });

        for (var i = 0; i < sparkCount; i++)
        {
            var angle = random.NextDouble() * Math.PI * 2.0;
            var speed = 8.0 + random.NextDouble() * 22.0;
            var life = 1.0 + random.NextDouble() * 1.0;

            particles.Add(new Particle
            {
                X = cx,
                Y = cy,
                VelocityX = (float)(Math.Cos(angle) * speed),
                VelocityY = (float)(Math.Sin(angle) * speed),
                LifeSeconds = life,
                Age = 0,
                Color = color,
                Size = speed > 16 ? 2 : 1
            });
        }
    }

    private Rgba32 RandomBurstColor()
    {
        return random.Next(0, 6) switch
        {
            0 => new Rgba32(255, 70, 60),
            1 => new Rgba32(255, 210, 80),
            2 => new Rgba32(80, 200, 255),
            3 => new Rgba32(165, 120, 255),
            4 => new Rgba32(120, 255, 170),
            _ => new Rgba32(255, 255, 255)
        };
    }

    private static Rgba32 ScaleColor(Rgba32 color, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(
            (byte)(color.R * clamped),
            (byte)(color.G * clamped),
            (byte)(color.B * clamped));
    }

    private static void BlendPixel(Image<Rgba32> img, int x, int y, Rgba32 source)
    {
        var current = img[x, y];
        var r = Math.Min(255, current.R + source.R);
        var g = Math.Min(255, current.G + source.G);
        var b = Math.Min(255, current.B + source.B);
        img[x, y] = new Rgba32((byte)r, (byte)g, (byte)b);
    }

    private struct Particle
    {
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public double LifeSeconds;
        public double Age;
        public Rgba32 Color;
        public int Size;
    }
}
