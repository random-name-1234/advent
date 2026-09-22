using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

// Integer-only primitives for the 64x32 logical canvas. The presenter owns scaling.
internal static class PixelArt
{
    internal static Rgba32 Color(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);
    internal static void Dot(Image<Rgba32> image, int x, int y, Rgba32 color)
    {
        if ((uint)x < image.Width && (uint)y < image.Height) image[x, y] = color;
    }

    internal static void Box(Image<Rgba32> image, int x, int y, int w, int h, Rgba32 color)
    {
        for (var py = Math.Max(0, y); py < Math.Min(image.Height, y + h); py++)
        for (var px = Math.Max(0, x); px < Math.Min(image.Width, x + w); px++)
            image[px, py] = color;
    }

    internal static void Disc(Image<Rgba32> image, int x, int y, int radius, Rgba32 color)
    {
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
            if (dx * dx + dy * dy <= radius * radius) Dot(image, x + dx, y + dy, color);
    }

    internal static Rgba32 Mix(Rgba32 a, Rgba32 b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    internal static void Sky(Image<Rgba32> image, Rgba32 top, Rgba32 bottom)
    {
        for (var y = 0; y < image.Height; y++) Box(image, 0, y, image.Width, 1, Mix(top, bottom, y / 31.0));
    }

    internal static void Cloud(Image<Rgba32> image, int x, int y, Rgba32 color)
    {
        Box(image, x, y + 2, 13, 2, color);
        Box(image, x + 2, y + 1, 8, 2, color);
        Box(image, x + 5, y, 4, 2, color);
    }

    internal static void Text(Image<Rgba32> image, string text, int y, Rgba32 color) =>
        RailDmiText.Draw(image, text, (64 - RailDmiText.MeasureWidth(text)) / 2, y, color);

    internal static int Wrap(double value, int span) => (int)((value % span + span) % span);
}

internal abstract class PixelStoryScene(string name, double duration = 20) : ISpecialScene
{
    protected double Seconds { get; private set; }
    public bool IsActive { get; protected set; }
    public bool HidesTime => IsActive;
    public bool RainbowSnow => false;
    public string Name => name;

    public virtual void Activate()
    {
        Seconds = 0;
        IsActive = true;
    }

    public virtual void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive || timeSpan <= TimeSpan.Zero) return;
        Seconds = Math.Min(duration, Seconds + timeSpan.TotalSeconds);
        if (Seconds >= duration) IsActive = false;
    }

    public void Draw(Image<Rgba32> image)
    {
        if (IsActive) Render(image);
    }

    protected abstract void Render(Image<Rgba32> image);
}
