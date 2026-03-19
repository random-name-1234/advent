using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class StaticImageScene : ISpecialScene
{
    private static readonly TimeSpan DefaultSceneDuration = TimeSpan.FromSeconds(6);

    private readonly Image<Rgba32> image;
    private readonly string name;
    private readonly TimeSpan sceneDuration;
    private readonly Point topLeft = new(0, 0);
    private TimeSpan elapsedThisScene;

    public StaticImageScene(string imageFilePath, string? sceneName = null, TimeSpan? sceneDurationOverride = null)
    {
        image = Image.Load<Rgba32>(imageFilePath);
        name = string.IsNullOrWhiteSpace(sceneName) ? "Static Image" : sceneName;
        sceneDuration = sceneDurationOverride is { } duration && duration > TimeSpan.Zero ? duration : DefaultSceneDuration;
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
        img.Mutate(x => x.DrawImage(image, topLeft, 1f));
    }
}
