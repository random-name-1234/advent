using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public class RainbowSnowScene : ISpecialScene
{
    private static readonly TimeSpan sceneLength = TimeSpan.FromSeconds(10);
    private TimeSpan elapsedThisScene;

    public RainbowSnowScene()
    {
        IsActive = false;
        HidesTime = false;
    }

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => true;
    public string Name => "Rainbow";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
    }

    public void Draw(Image<Rgba32> img)
    {
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        elapsedThisScene += timeSpan;
        if (elapsedThisScene > sceneLength)
        {
            IsActive = false;
            HidesTime = false;
        }
    }
}