using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent
{
    public class PlasmaSdfScene : ISpecialScene
    {
        private const int Width = 64;
        private const int Height = 32;

        private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

        private TimeSpan elapsedThisScene;

        public bool IsActive { get; private set; }

        public bool HidesTime { get; private set; }

        public bool RainbowSnow => false;

        public string Name => "Plasma SDF";

        public void Activate()
        {
            elapsedThisScene = TimeSpan.Zero;
            HidesTime = true;
            IsActive = true;
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
            }
        }

        public void Draw(Image<Rgba32> img)
        {
            if (!IsActive)
            {
                return;
            }

            var t = (float)elapsedThisScene.TotalSeconds;

            var c1x = MathF.Sin(t * 0.8f) * 0.45f;
            var c1y = MathF.Cos(t * 1.1f) * 0.35f;
            var c2x = MathF.Cos(t * 1.3f) * 0.4f;
            var c2y = MathF.Sin(t * 0.7f) * 0.3f;

            for (var y = 0; y < Height; y++)
            {
                var ny = (y / (Height - 1f)) * 2f - 1f;
                for (var x = 0; x < Width; x++)
                {
                    var nx = (x / (Width - 1f)) * 2f - 1f;

                    var d1 = Distance(nx, ny, c1x, c1y);
                    var d2 = Distance(nx, ny, c2x, c2y);

                    var sdfBand = MathF.Sin(17f * d1 - t * 2.4f) + MathF.Sin(14f * d2 + t * 2.0f);
                    var plasma = MathF.Sin((nx + ny) * 5f + t) + MathF.Cos((nx - ny) * 4f - t * 0.7f);
                    var combined = (sdfBand + plasma) * 0.25f + 0.5f;

                    img[x, y] = Palette(combined, t);
                }
            }
        }

        private static float Distance(float x1, float y1, float x2, float y2)
        {
            var dx = x1 - x2;
            var dy = y1 - y2;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        private static Rgba32 Palette(float value, float time)
        {
            var phase = value * MathF.PI * 2f;
            var r = 0.5f + 0.5f * MathF.Sin(phase + time * 0.3f);
            var g = 0.5f + 0.5f * MathF.Sin(phase + 2.1f + time * 0.25f);
            var b = 0.5f + 0.5f * MathF.Sin(phase + 4.2f + time * 0.2f);

            return new Rgba32(ToByte(r * 255f), ToByte(g * 255f), ToByte(b * 255f));
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }
    }
}
