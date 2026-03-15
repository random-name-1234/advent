using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class ErrorScene : ISpecialScene
{
    private static readonly TimeSpan sadFaceAfter = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan duration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan stopAt = TimeSpan.FromSeconds(7);
    private readonly Image<Rgba32> error;
    private readonly Image<Rgba32> sadFace;

    private TimeSpan elapsedThisScene;

    public ErrorScene()
    {
        IsActive = false;
        HidesTime = false;
        error = Image.Load<Rgba32>("error.png");
        sadFace = Image.Load<Rgba32>("sad-face.png");
    }

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => false;
    public string Name => "Error";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
        HidesTime = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        elapsedThisScene += timeSpan;
        if (elapsedThisScene > stopAt)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (IsActive)
        {
            var alpha = 1f;
            if (elapsedThisScene > duration)
                alpha = (float)Math.Max(0.0, Math.Min(1.0, (stopAt - elapsedThisScene).TotalSeconds));

            img.Mutate(x => x.DrawImage(error, new Point(0, 0), alpha));

            if (elapsedThisScene > sadFaceAfter) img.Mutate(x => x.DrawImage(sadFace, new Point(0, 0), alpha));
        }
    }
}