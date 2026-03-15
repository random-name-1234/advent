using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class FadeInOutScene : ISpecialScene
{
    private static readonly TimeSpan fadeDuration = TimeSpan.FromSeconds(1);

    private readonly Fade fadeDirection;

    private TimeSpan elapsedThisScene;

    public FadeInOutScene(Fade fadeDirection)
    {
        this.fadeDirection = fadeDirection;
        IsActive = false;
        HidesTime = false;
    }

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }
    public string Name => "FadeInOut";

    public bool RainbowSnow => false;

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        elapsedThisScene += timeSpan;
        if (elapsedThisScene > fadeDuration)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        double fraction;
        if (fadeDirection == Fade.In)
            fraction = 1.0 - elapsedThisScene.TotalMilliseconds / fadeDuration.TotalMilliseconds;
        else
            fraction = elapsedThisScene.TotalMilliseconds / fadeDuration.TotalMilliseconds;

        fraction = Math.Min(1.0, Math.Max(0.0, fraction));
        if (IsActive)
        {
            var colour = new Rgba32(0, 0, 0, (float)fraction);
            img.Mutate(x =>
                x.FillPolygon(colour, new PointF(0, 0), new PointF(64, 0), new PointF(64, 32), new PointF(0, 32)));
        }
    }
}