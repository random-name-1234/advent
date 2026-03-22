using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class BoidsScene : ISpecialScene
{
    private const int BoidCount = 30;
    private const float MaxSpeed = 24f;
    private const float MinSpeed = 7f;
    private const float SeparationRadius = 3.5f;
    private const float AlignmentRadius = 8f;
    private const float CohesionRadius = 10f;
    private const float SeparationWeight = 2.4f;
    private const float AlignmentWeight = 1.0f;
    private const float CohesionWeight = 0.8f;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

    private readonly Boid[] boids = new Boid[BoidCount];
    private readonly Random random = new();
    private TimeSpan elapsedThisScene;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Boids";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        HidesTime = true;
        IsActive = true;
        SeedBoids();
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
        if (dt > 0.1f) dt = 0.1f;

        StepSimulation(dt);
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        for (var i = 0; i < BoidCount; i++)
        {
            ref var b = ref boids[i];

            // Draw trail (previous position, dimmed)
            var tx = WrapCoord(b.PrevX, Width);
            var ty = WrapCoord(b.PrevY, Height);
            BlendPixel(img, tx, ty, Scale(b.Color, 0.3f));

            // Draw boid as 2x2 block (current position, bright)
            var bx = WrapCoord(b.X, Width);
            var by = WrapCoord(b.Y, Height);
            BlendPixel(img, bx, by, b.Color);
            BlendPixel(img, bx + 1, by, Scale(b.Color, 0.7f));
            BlendPixel(img, bx, by + 1, Scale(b.Color, 0.7f));
            BlendPixel(img, bx + 1, by + 1, Scale(b.Color, 0.4f));
        }
    }

    private void SeedBoids()
    {
        for (var i = 0; i < BoidCount; i++)
        {
            var x = (float)(random.NextDouble() * Width);
            var y = (float)(random.NextDouble() * Height);
            var angle = (float)(random.NextDouble() * MathF.Tau);
            var speed = MinSpeed + (float)(random.NextDouble() * (MaxSpeed - MinSpeed) * 0.5f);

            var hueShift = (float)(random.NextDouble() * 0.35f - 0.175f);
            var color = TealWithHueShift(hueShift);

            boids[i] = new Boid
            {
                X = x,
                Y = y,
                PrevX = x,
                PrevY = y,
                Prev2X = x,
                Prev2Y = y,
                Vx = MathF.Cos(angle) * speed,
                Vy = MathF.Sin(angle) * speed,
                Color = color
            };
        }
    }

    private void StepSimulation(float dt)
    {
        for (var i = 0; i < BoidCount; i++)
        {
            ref var self = ref boids[i];

            // Store previous positions for trail
            self.Prev2X = self.PrevX;
            self.Prev2Y = self.PrevY;
            self.PrevX = self.X;
            self.PrevY = self.Y;

            float sepX = 0, sepY = 0;
            float alignVx = 0, alignVy = 0;
            float cohX = 0, cohY = 0;
            int alignCount = 0, cohCount = 0;

            for (var j = 0; j < BoidCount; j++)
            {
                if (i == j) continue;

                ref var other = ref boids[j];
                var dx = ToroidalDelta(other.X, self.X, Width);
                var dy = ToroidalDelta(other.Y, self.Y, Height);
                var distSq = dx * dx + dy * dy;

                // Separation
                if (distSq < SeparationRadius * SeparationRadius && distSq > 0.001f)
                {
                    var invDist = 1f / MathF.Sqrt(distSq);
                    sepX -= dx * invDist;
                    sepY -= dy * invDist;
                }

                // Alignment
                if (distSq < AlignmentRadius * AlignmentRadius)
                {
                    alignVx += other.Vx;
                    alignVy += other.Vy;
                    alignCount++;
                }

                // Cohesion
                if (distSq < CohesionRadius * CohesionRadius)
                {
                    cohX += dx;
                    cohY += dy;
                    cohCount++;
                }
            }

            var ax = sepX * SeparationWeight;
            var ay = sepY * SeparationWeight;

            if (alignCount > 0)
            {
                alignVx /= alignCount;
                alignVy /= alignCount;
                ax += (alignVx - self.Vx) * AlignmentWeight;
                ay += (alignVy - self.Vy) * AlignmentWeight;
            }

            if (cohCount > 0)
            {
                cohX /= cohCount;
                cohY /= cohCount;
                ax += cohX * CohesionWeight;
                ay += cohY * CohesionWeight;
            }

            self.Vx += ax * dt;
            self.Vy += ay * dt;

            // Clamp speed
            var speedSq = self.Vx * self.Vx + self.Vy * self.Vy;
            if (speedSq > MaxSpeed * MaxSpeed)
            {
                var invSpeed = MaxSpeed / MathF.Sqrt(speedSq);
                self.Vx *= invSpeed;
                self.Vy *= invSpeed;
            }
            else if (speedSq < MinSpeed * MinSpeed && speedSq > 0.001f)
            {
                var invSpeed = MinSpeed / MathF.Sqrt(speedSq);
                self.Vx *= invSpeed;
                self.Vy *= invSpeed;
            }

            // Integrate position with toroidal wrap
            self.X = Wrap(self.X + self.Vx * dt, Width);
            self.Y = Wrap(self.Y + self.Vy * dt, Height);
        }
    }

    private static float ToroidalDelta(float a, float b, int size)
    {
        var delta = a - b;
        if (delta > size * 0.5f) delta -= size;
        else if (delta < -size * 0.5f) delta += size;
        return delta;
    }

    private static int WrapCoord(float value, int size)
    {
        var v = (int)MathF.Round(value) % size;
        return v < 0 ? v + size : v;
    }

    private static float Wrap(float value, int size)
    {
        var v = value % size;
        return v < 0 ? v + size : v;
    }

    private static Rgba32 TealWithHueShift(float shift)
    {
        // Base teal: roughly (0, 210, 200). Shift towards cyan or green-blue.
        var r = (byte)Math.Clamp((int)(20 + shift * 40), 0, 60);
        var g = (byte)Math.Clamp((int)(200 + shift * 55), 140, 255);
        var b = (byte)Math.Clamp((int)(210 - shift * 70), 150, 255);
        return new Rgba32(r, g, b);
    }

    private static Rgba32 Scale(Rgba32 color, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(
            ToByte(color.R * clamped),
            ToByte(color.G * clamped),
            ToByte(color.B * clamped));
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

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private struct Boid
    {
        public float X;
        public float Y;
        public float PrevX;
        public float PrevY;
        public float Prev2X;
        public float Prev2Y;
        public float Vx;
        public float Vy;
        public Rgba32 Color;
    }
}
