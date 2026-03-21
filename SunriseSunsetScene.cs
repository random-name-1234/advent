using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class SunriseSunsetScene : ISpecialScene
{
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(20);

    private const int GroundHeight = 4;
    private const int StarCount = 28;
    private const float SunRadius = 3f;

    // Precomputed star positions (deterministic via hash)
    private static readonly (int X, int Y, float Phase)[] Stars = BuildStars();

    // Sky palette keyframes: (normalizedTime, topColor, horizonColor)
    private static readonly SkyKey[] SkyKeys =
    [
        new(0.00f, Rgb(10, 10, 46),   Rgb(20, 20, 50)),    // night
        new(0.15f, Rgb(10, 10, 46),   Rgb(20, 20, 50)),    // night hold
        new(0.20f, Rgb(18, 14, 60),   Rgb(80, 40, 90)),    // dawn – purple
        new(0.25f, Rgb(30, 30, 90),   Rgb(200, 100, 50)),  // dawn – orange horizon
        new(0.30f, Rgb(60, 100, 180), Rgb(255, 160, 80)),  // sunrise – orange/pink
        new(0.35f, Rgb(80, 140, 220), Rgb(255, 200, 120)), // sunrise – golden
        new(0.40f, Rgb(100, 170, 240),Rgb(180, 210, 240)), // transition to day
        new(0.50f, Rgb(110, 180, 250),Rgb(190, 220, 250)), // day
        new(0.60f, Rgb(110, 180, 250),Rgb(190, 220, 250)), // day hold
        new(0.65f, Rgb(90, 150, 230), Rgb(200, 190, 180)), // transition from day
        new(0.70f, Rgb(60, 80, 160),  Rgb(255, 140, 60)),  // sunset – orange
        new(0.75f, Rgb(40, 40, 110),  Rgb(220, 80, 50)),   // sunset – red
        new(0.80f, Rgb(25, 20, 70),   Rgb(120, 50, 80)),   // dusk – purple
        new(0.85f, Rgb(14, 14, 50),   Rgb(40, 30, 60)),    // dusk – fading
        new(0.90f, Rgb(10, 10, 46),   Rgb(20, 20, 50)),    // night
        new(1.00f, Rgb(10, 10, 46),   Rgb(20, 20, 50)),    // night hold
    ];

    // Ground hill profile: height offset per column (precomputed)
    private static readonly int[] HillProfile = BuildHillProfile();

    private TimeSpan elapsedThisScene;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Sunrise Sunset";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        HidesTime = true;
        IsActive = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive) return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        var t = Math.Clamp((float)(elapsedThisScene.TotalSeconds / SceneDuration.TotalSeconds), 0f, 1f);
        var time = (float)elapsedThisScene.TotalSeconds;

        DrawSkyGradient(img, t);
        DrawStars(img, t, time);
        DrawSun(img, t);
        DrawGround(img, t);
    }

    private static void DrawSkyGradient(Image<Rgba32> img, float t)
    {
        var (topColor, horizonColor) = InterpolateSky(t);
        var skyBottom = Height - GroundHeight;

        for (var y = 0; y < skyBottom; y++)
        {
            var verticalT = y / (float)(skyBottom - 1);
            var r = ToByte(Lerp(topColor.R, horizonColor.R, verticalT));
            var g = ToByte(Lerp(topColor.G, horizonColor.G, verticalT));
            var b = ToByte(Lerp(topColor.B, horizonColor.B, verticalT));
            var rowColor = new Rgba32(r, g, b);

            for (var x = 0; x < Width; x++)
                img[x, y] = rowColor;
        }
    }

    private static void DrawStars(Image<Rgba32> img, float t, float time)
    {
        // Stars visible when t < 0.20 or t > 0.82 (night phases)
        float starAlpha;
        if (t < 0.15f)
            starAlpha = 1f;
        else if (t < 0.28f)
            starAlpha = 1f - (t - 0.15f) / 0.13f;
        else if (t < 0.82f)
            starAlpha = 0f;
        else if (t < 0.90f)
            starAlpha = (t - 0.82f) / 0.08f;
        else
            starAlpha = 1f;

        if (starAlpha <= 0.01f) return;

        for (var i = 0; i < StarCount; i++)
        {
            var (sx, sy, phase) = Stars[i];
            if (sy >= Height - GroundHeight) continue;

            var twinkle = 0.6f + 0.4f * MathF.Sin(time * 3.5f + phase);
            var brightness = ToByte(200f * starAlpha * twinkle);
            var starColor = new Rgba32(brightness, brightness, ToByte(brightness * 0.95f));
            BlendPixel(img, sx, sy, starColor);
        }
    }

    private static void DrawSun(Image<Rgba32> img, float t)
    {
        // Sun arc: rises from bottom-left at t=0.22, peaks at t=0.50, sets bottom-right at t=0.78
        const float sunAppear = 0.22f;
        const float sunDisappear = 0.78f;

        if (t < sunAppear || t > sunDisappear) return;

        var sunT = (t - sunAppear) / (sunDisappear - sunAppear); // 0..1 across sun's journey

        // Horizontal: left edge to right edge
        var sunX = 4f + sunT * (Width - 8f);

        // Vertical: parabolic arc, high point in top third
        var peakY = 5f;
        var horizonY = Height - GroundHeight - 1f;
        var arcT = sunT * 2f - 1f; // -1..1
        var sunY = peakY + (horizonY - peakY) * arcT * arcT;

        var cx = (int)MathF.Round(sunX);
        var cy = (int)MathF.Round(sunY);

        // Sun visibility: fade in/out near horizon
        var sunAlpha = 1f;
        if (sunT < 0.1f) sunAlpha = sunT / 0.1f;
        else if (sunT > 0.9f) sunAlpha = (1f - sunT) / 0.1f;
        sunAlpha = Math.Clamp(sunAlpha, 0f, 1f);

        // Draw sun as layered discs using direct writes (not additive)
        // so glow/body/core each have distinct colors.
        var glowColor = new Rgba32(
            ToByte(255f * sunAlpha * 0.5f),
            ToByte(200f * sunAlpha * 0.4f),
            ToByte(80f * sunAlpha * 0.3f));
        DrawDiscDirect(img, cx, cy, (int)SunRadius + 2, glowColor);

        var sunColor = new Rgba32(
            ToByte(255f * sunAlpha),
            ToByte(220f * sunAlpha),
            ToByte(100f * sunAlpha));
        DrawDiscDirect(img, cx, cy, (int)SunRadius, sunColor);

        var coreColor = new Rgba32(
            ToByte(255f * sunAlpha),
            ToByte(250f * sunAlpha),
            ToByte(180f * sunAlpha));
        DrawDiscDirect(img, cx, cy, 1, coreColor);
    }

    private static void DrawGround(Image<Rgba32> img, float t)
    {
        // Ground color shifts with time of day
        Rgba32 groundDark, groundLight;
        if (t < 0.20f || t > 0.85f)
        {
            // Night ground
            groundDark = Rgb(8, 12, 8);
            groundLight = Rgb(14, 20, 14);
        }
        else if (t is > 0.35f and < 0.65f)
        {
            // Day ground
            groundDark = Rgb(20, 50, 15);
            groundLight = Rgb(30, 70, 22);
        }
        else
        {
            // Transitional ground – blend between night and day
            var blend = t < 0.35f
                ? (t - 0.20f) / 0.15f
                : 1f - (t - 0.65f) / 0.20f;
            blend = Math.Clamp(blend, 0f, 1f);

            groundDark = LerpColor(Rgb(8, 12, 8), Rgb(20, 50, 15), blend);
            groundLight = LerpColor(Rgb(14, 20, 14), Rgb(30, 70, 22), blend);
        }

        for (var x = 0; x < Width; x++)
        {
            var hillTop = Height - GroundHeight + HillProfile[x];
            for (var y = hillTop; y < Height; y++)
            {
                var depth = (float)(y - hillTop) / GroundHeight;
                var color = LerpColor(groundLight, groundDark, depth);
                img[x, y] = color;
            }
        }
    }

    private static (Rgba32 Top, Rgba32 Horizon) InterpolateSky(float t)
    {
        // Find surrounding keyframes
        var i = 0;
        for (; i < SkyKeys.Length - 1; i++)
        {
            if (SkyKeys[i + 1].T >= t) break;
        }

        var a = SkyKeys[i];
        var b = SkyKeys[Math.Min(i + 1, SkyKeys.Length - 1)];
        var range = b.T - a.T;
        var local = range > 0.001f ? (t - a.T) / range : 0f;
        local = Math.Clamp(local, 0f, 1f);

        // Smooth interpolation
        local = local * local * (3f - 2f * local);

        return (LerpColor(a.Top, b.Top, local), LerpColor(a.Horizon, b.Horizon, local));
    }

    private static void DrawDiscDirect(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        var r2 = radius * radius;
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > r2) continue;
            var px = centerX + dx;
            var py = centerY + dy;
            if ((uint)px < Width && (uint)py < Height)
                img[px, py] = color;
        }
    }

    private static void DrawDisc(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        var r2 = radius * radius;
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy > r2) continue;
            BlendPixel(img, centerX + dx, centerY + dy, color);
        }
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

    private static Rgba32 LerpColor(Rgba32 a, Rgba32 b, float t)
    {
        return new Rgba32(
            ToByte(Lerp(a.R, b.R, t)),
            ToByte(Lerp(a.G, b.G, t)),
            ToByte(Lerp(a.B, b.B, t)));
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static Rgba32 Rgb(byte r, byte g, byte b) => new(r, g, b);

    private static byte ToByte(float value) =>
        (byte)Math.Clamp((int)MathF.Round(value), 0, 255);

    private static (int X, int Y, float Phase)[] BuildStars()
    {
        var result = new (int, int, float)[StarCount];
        for (var i = 0; i < StarCount; i++)
        {
            var x = ((Hash(i, 17) & 0x7FFFFFFF) % Width);
            var y = ((Hash(i, 31) & 0x7FFFFFFF) % (Height - GroundHeight - 2));
            var phase = (Hash(i, 53) & 0x7FFFFFFF) % 628 / 100f;
            result[i] = (x, y, phase);
        }

        return result;
    }

    private static int[] BuildHillProfile()
    {
        var profile = new int[Width];
        for (var x = 0; x < Width; x++)
        {
            // Gentle rolling hills using layered sine waves
            var nx = x / (float)Width;
            var h = MathF.Sin(nx * MathF.PI * 2f) * 1.2f
                  + MathF.Sin(nx * MathF.PI * 5f + 1.2f) * 0.6f
                  + MathF.Sin(nx * MathF.PI * 9f + 0.7f) * 0.3f;
            profile[x] = -(int)MathF.Round(Math.Clamp(h, -2f, 2f));
        }

        return profile;
    }

    private static int Hash(int seed, int salt)
    {
        unchecked
        {
            var h = seed * 374761393 + salt * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    private readonly record struct SkyKey(float T, Rgba32 Top, Rgba32 Horizon);
}
