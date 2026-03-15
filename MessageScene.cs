using System;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class MessageScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;
    private const float ScrollSpeedPixelsPerSecond = 24f;
    private static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(25);

    private readonly Font font;
    private readonly string message;
    private readonly string name;
    private readonly TimeSpan? sceneDurationOverride;
    private readonly Rgba32 textColor;

    private TimeSpan elapsedThisScene;
    private TimeSpan sceneDuration;
    private float textHeight;
    private float textWidth;

    public MessageScene(string message, TimeSpan? sceneDurationOverride = null, Rgba32? textColor = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty.", nameof(message));

        this.message = message.Trim();
        name = $"Message: {BuildSceneLabel(this.message)}";
        this.sceneDurationOverride = sceneDurationOverride;
        this.textColor = textColor ?? Color.White;
        font = AppFonts.Create(12);
    }

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => name;

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
        HidesTime = true;

        var textSize = TextMeasurer.MeasureSize(message, new TextOptions(font));
        textWidth = Math.Max(1f, textSize.Width);
        textHeight = Math.Max(1f, textSize.Height);
        sceneDuration = sceneDurationOverride ?? ComputeSceneDuration();
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > sceneDuration)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive)
            return;

        var progress = elapsedThisScene.TotalMilliseconds / sceneDuration.TotalMilliseconds;
        progress = Math.Clamp(progress, 0d, 1d);

        var startX = Width;
        var endX = -textWidth - 2f;
        var x = (float)(startX + (endX - startX) * progress);
        var y = (Height - textHeight) / 2f;

        img.Mutate(xctx => xctx.DrawText(message, font, textColor, new PointF(x, y)));
    }

    private TimeSpan ComputeSceneDuration()
    {
        var travelDistance = Width + textWidth + 4f;
        var seconds = travelDistance / ScrollSpeedPixelsPerSecond;
        var duration = TimeSpan.FromSeconds(seconds);

        if (duration < MinDuration)
            return MinDuration;
        if (duration > MaxDuration)
            return MaxDuration;
        return duration;
    }

    private static string BuildSceneLabel(string text)
    {
        const int max = 16;
        if (text.Length <= max)
            return text;

        return text[..(max - 1)] + "…";
    }
}
