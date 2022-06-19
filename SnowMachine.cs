using System;
using System.Numerics;
using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;

namespace advent
{
    public class SnowMachine
    {
        public List<Flake> Flakes { get; set; }
        public float FlakesPerSecond { get; set; } = 5;

        private TimeSpan totalElapsed;
        private TimeSpan nextFlakeAt = TimeSpan.Zero;
        private Random random;
        private static readonly List<Rgba32> rainbow = new List<Rgba32>
        {
            new Rgba32(1, 0, 0, 0.33f),
            new Rgba32(1, 0.65f, 0, 0.33f),
            new Rgba32(1, 1, 0, 0.33f),
            new Rgba32(0, 0.5f, 0, 0.33f),
            new Rgba32(0,0, 1, 0.33f),
            new Rgba32(0.5f, 0, 0.5f, 0.33f)
        };

        public SnowMachine()
        {
            Flakes = new List<Flake>();
            random = new Random();
            RainbowSnow = false;
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            totalElapsed += timeSpan;
            if (totalElapsed >= nextFlakeAt)
            {
                nextFlakeAt += TimeSpan.FromSeconds(2.0 / FlakesPerSecond * random.NextDouble());
                if (nextFlakeAt < totalElapsed)
                {
                    nextFlakeAt = totalElapsed;
                }

                var position = new Vector2((float)(random.NextDouble() * 64.0), (float)32);
                var speed = (float)(8.0 + (random.NextDouble() * 2.0));
                if (!RainbowSnow)
                {
                    Flakes.Add(new Flake(position, speed, Color.White, 1.0f));
                }
                else
                {
                    var colour = rainbow[random.Next(0, rainbow.Count - 1)];
                    Flakes.Add(new Flake(position, speed, colour, 3.0f));
                }
            }

            foreach (var flake in Flakes)
            {
                flake.Elapsed(timeSpan);
            }

            Flakes.RemoveAll(x => x.CanBeRemoved);
        }

        public bool RainbowSnow { get; set; }
        public bool CanBeRemoved { get; }
    }
}