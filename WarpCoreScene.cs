using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent
{
    public enum WarpCorePalettePreset
    {
        Blue,
        RedAlert
    }

    public class WarpCoreScene : ISpecialScene
    {
        private const int Width = 64;
        private const int Height = 32;
        private const int CoreCenterX = Width / 2;
        private const int CoreHalfWidth = 12;
        private const int CoreStartX = CoreCenterX - CoreHalfWidth;
        private const int CoreColumnCount = CoreHalfWidth * 2 + 1;

        private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);
        private static readonly TimeSpan SurgePeriod = TimeSpan.FromMilliseconds(1400);
        private static readonly TimeSpan SurgePhaseOffset = TimeSpan.FromMilliseconds(700);

        // Precomputed horizontal falloff profiles to keep per-frame work low on Pi.
        private static readonly float[] CoreProfile = BuildHorizontalProfile(1.9f);
        private static readonly float[] GlowProfile = BuildHorizontalProfile(7.0f);

        private readonly WarpCorePalette palette;
        private TimeSpan elapsedThisScene;

        public WarpCoreScene()
            : this(WarpCorePalettePreset.Blue)
        {
        }

        public WarpCoreScene(WarpCorePalettePreset preset)
        {
            palette = preset switch
            {
                WarpCorePalettePreset.Blue => new WarpCorePalette(
                    new Rgba32(175, 230, 255),
                    new Rgba32(20, 70, 180),
                    new Rgba32(240, 255, 255)),
                WarpCorePalettePreset.RedAlert => new WarpCorePalette(
                    new Rgba32(255, 145, 130),
                    new Rgba32(145, 22, 18),
                    new Rgba32(255, 230, 140)),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unknown warp core palette preset.")
            };
        }

        public bool IsActive { get; private set; }

        public bool HidesTime { get; private set; }

        public bool RainbowSnow => false;

        public string Name => "Warp Core";

        public void Activate()
        {
            elapsedThisScene = TimeSpan.Zero;
            HidesTime = false;
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
            var surgeY1 = GetTravellingSurgePosition(elapsedThisScene);
            var surgeY2 = GetTravellingSurgePosition(elapsedThisScene + SurgePhaseOffset);

            for (var y = 0; y < Height; y++)
            {
                var verticalPulse = 0.62f + 0.38f * MathF.Sin(t * 6.4f + y * 0.42f);
                var surge = ComputeSurgeContribution(y, surgeY1) + ComputeSurgeContribution(y, surgeY2);
                var energy = Clamp01(verticalPulse + surge);

                for (var i = 0; i < CoreColumnCount; i++)
                {
                    var x = CoreStartX + i;
                    var core = CoreProfile[i];
                    var glow = GlowProfile[i];
                    var intensity = Clamp01(glow * 0.26f + core * (0.55f + 0.85f * energy));
                    if (intensity < 0.03f)
                    {
                        continue;
                    }

                    var surgeMix = Clamp01(surge * (0.45f + 0.55f * core));
                    img[x, y] = MixColor(palette, intensity, core, surgeMix);
                }
            }
        }

        private static float[] BuildHorizontalProfile(float spread)
        {
            var values = new float[CoreColumnCount];
            var denom = spread * spread;

            for (var i = 0; i < CoreColumnCount; i++)
            {
                var distance = i - CoreHalfWidth;
                values[i] = MathF.Exp(-(distance * distance) / denom);
            }

            return values;
        }

        private static float GetTravellingSurgePosition(TimeSpan elapsed)
        {
            var phase = (float)((elapsed.TotalMilliseconds % SurgePeriod.TotalMilliseconds) / SurgePeriod.TotalMilliseconds);
            return (Height + 3f) - (phase * (Height + 7f));
        }

        private static float ComputeSurgeContribution(int y, float surgeY)
        {
            var dy = y - surgeY;
            const float sigma = 2.4f;
            var gaussian = MathF.Exp(-(dy * dy) / (2f * sigma * sigma));
            return gaussian * 0.95f;
        }

        private static Rgba32 MixColor(WarpCorePalette palette, float intensity, float coreWeight, float surgeWeight)
        {
            var glowWeight = Clamp01(intensity * (1f - (coreWeight * 0.7f)));
            var coreMix = Clamp01(intensity * (0.35f + coreWeight * 0.65f));

            var r = palette.Glow.R * glowWeight + palette.Core.R * coreMix + palette.Surge.R * surgeWeight;
            var g = palette.Glow.G * glowWeight + palette.Core.G * coreMix + palette.Surge.G * surgeWeight;
            var b = palette.Glow.B * glowWeight + palette.Core.B * coreMix + palette.Surge.B * surgeWeight;

            return new Rgba32(ToByte(r), ToByte(g), ToByte(b));
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value)));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            if (value > 1f)
            {
                return 1f;
            }

            return value;
        }

        private readonly record struct WarpCorePalette(
            Rgba32 Core,
            Rgba32 Glow,
            Rgba32 Surge);
    }
}
