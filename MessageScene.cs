using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class MessageScene : ISpecialScene
{
    private readonly MessageLayout layout;
    private readonly Rgba32 textColor;
    private TimeSpan elapsed;

    public MessageScene(string message, TimeSpan? sceneDurationOverride = null, Rgba32? textColor = null)
    {
        layout = MessageLayout.Create(message, sceneDurationOverride);
        var label = message.Trim();
        Name = $"Message: {(label.Length <= 16 ? label : label[..15] + "\u2026")}";
        this.textColor = textColor ?? new Rgba32(220, 230, 255);
    }

    public bool IsActive { get; private set; }
    public bool HidesTime => IsActive;
    public bool RainbowSnow => false;
    public string Name { get; }
    internal MessageLayout Layout => layout;

    public void Activate()
    {
        elapsed = TimeSpan.Zero;
        IsActive = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive || timeSpan <= TimeSpan.Zero) return;
        elapsed += timeSpan;
        if (elapsed >= layout.Duration) IsActive = false;
    }

    public void Draw(Image<Rgba32> image)
    {
        if (IsActive) DrawPage(image, layout, elapsed, textColor);
    }

    internal static void DrawPage(Image<Rgba32> image, MessageLayout layout, TimeSpan elapsed, Rgba32 color)
    {
        MatrixTextLayout.Clear(image);
        foreach (var run in layout.TextRuns(elapsed, color)) run.Draw(image);
        if (layout.Pages.Count < 2) return;
        var left = (64 - (layout.Pages.Count * 4 - 1)) / 2;
        for (var page = 0; page < layout.Pages.Count; page++)
        for (var x = 0; x < 3; x++)
            image[left + page * 4 + x, 30] = page == layout.PageAt(elapsed) ? color : new Rgba32(35, 40, 52);
    }
}
