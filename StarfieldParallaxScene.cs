using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent
{
    public class StarfieldParallaxScene : ISpecialScene
    {
        private const int Width = 64;
        private const int Height = 32;

        private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

        private readonly Random random = new Random();
        private readonly List<Star> stars = new List<Star>();

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

            AddLayer(14, 1);
            AddLayer(10, 2);
            AddLayer(8, 3);
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            if (!IsActive)
            {
                return;
            }

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
            if (!IsActive)
            {
                return;
            }

            var t = (float)elapsedThisScene.TotalSeconds;
            foreach (var star in stars)
            {
                var x = (int)star.X;
                var y = (int)star.Y;
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                {
                    continue;
                }

                var twinkle = 0.7f + 0.3f * MathF.Sin(t * 7f + star.Phase);
                var brightness = ClampToByte(star.BaseBrightness * twinkle);
                img[x, y] = new Rgba32(brightness, brightness, ClampToByte(brightness * 0.9f));

                if (star.Layer == 3 && x > 0)
                {
                    var trail = ClampToByte(brightness * 0.4f);
                    img[x - 1, y] = new Rgba32(trail, trail, trail);
                }
            }
        }

        private void AddLayer(int count, byte layer)
        {
            for (var i = 0; i < count; i++)
            {
                stars.Add(CreateStar(layer, (float)random.NextDouble() * Width));
            }
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
                1 => 90f,
                2 => 155f,
                _ => 235f
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
}
