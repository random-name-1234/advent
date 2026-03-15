using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class StaticImageScene : ISpecialScene
{
    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(0.5);

    private readonly Image<Rgba32> image;
    private readonly string name;
    private readonly Point topLeft = new(0, 0);
    private TimeSpan elapsedThisScene;

    public StaticImageScene(string imageFilePath, string? sceneName = null)
    {
        image = Image.Load<Rgba32>(imageFilePath);
        name = string.IsNullOrWhiteSpace(sceneName) ? "Static Image" : sceneName;
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

        double fraction;
        if (elapsedThisScene < FadeDuration)
            fraction = elapsedThisScene.TotalMilliseconds / FadeDuration.TotalMilliseconds;
        else if (elapsedThisScene < SceneDuration - FadeDuration)
            fraction = 1.0;
        else
            fraction = (SceneDuration - elapsedThisScene).TotalMilliseconds / FadeDuration.TotalMilliseconds;

        fraction = Math.Min(1.0, Math.Max(0.0, fraction));
        img.Mutate(x => x.DrawImage(image, topLeft, (float)fraction));
    }
}
