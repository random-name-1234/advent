using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class ScrollingImageScene : ISpecialScene
{
    private const int MatrixWidth = 64;
    private const float ScrollSpeedPixelsPerSecond = 16f;
    private static readonly TimeSpan MinSceneDuration = TimeSpan.FromSeconds(4);

    private readonly Image<Rgba32> image;
    private readonly string name;
    private readonly TimeSpan sceneDuration;
    private TimeSpan elapsedThisScene;

    public ScrollingImageScene(string imageFilePath, string? sceneName = null, TimeSpan? sceneDurationOverride = null)
    {
        image = Image.Load<Rgba32>(imageFilePath);
        name = string.IsNullOrWhiteSpace(sceneName) ? "Scrolling Image" : sceneName;
        sceneDuration = sceneDurationOverride is { } duration && duration > TimeSpan.Zero
            ? ClampSceneDuration(duration)
            : ComputeSceneDuration();
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
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        elapsedThisScene += timeSpan;
        if (elapsedThisScene > sceneDuration)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        var startX = img.Width - 2;
        var movementDistance = startX + image.Width + 1;
        var progress = elapsedThisScene.TotalMilliseconds / sceneDuration.TotalMilliseconds;
        var position = new Point(startX - (int)(progress * movementDistance), 0);

        img.Mutate(x => x.DrawImage(image, position, 1f));
        if (position.X <= -image.Width)
            IsActive = false;
    }

    private TimeSpan ComputeSceneDuration()
    {
        var travelDistance = MatrixWidth - 2 + image.Width + 1;
        var duration = TimeSpan.FromSeconds(travelDistance / ScrollSpeedPixelsPerSecond);
        if (duration < MinSceneDuration)
            return MinSceneDuration;

        return ClampSceneDuration(duration);
    }

    private static TimeSpan ClampSceneDuration(TimeSpan duration)
    {
        return duration > SceneTiming.MaxSceneDuration ? SceneTiming.MaxSceneDuration : duration;
    }
}
