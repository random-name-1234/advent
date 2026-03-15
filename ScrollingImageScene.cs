using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class ScrollingImageScene : ISpecialScene
{
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(15);

    private readonly Image<Rgba32> image;
    private readonly string name;
    private TimeSpan elapsedThisScene;

    public ScrollingImageScene(string imageFilePath, string? sceneName = null)
    {
        image = Image.Load<Rgba32>(imageFilePath);
        name = string.IsNullOrWhiteSpace(sceneName) ? "Scrolling Image" : sceneName;
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
        if (elapsedThisScene > SceneDuration)
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
        var progress = elapsedThisScene.TotalMilliseconds / SceneDuration.TotalMilliseconds;
        var position = new Point(startX - (int)(progress * movementDistance), 0);

        img.Mutate(x => x.DrawImage(image, position, 1f));
        if (position.X <= -image.Width)
            IsActive = false;
    }
}
