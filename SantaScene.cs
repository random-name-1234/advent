using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class SantaScene : ISpecialScene
{
    private const string SantaFileName = "santa-256x32.png";
    private const string Legs1FileName = "santa-256x32-legs1.png";
    private const string Legs2FileName = "santa-256x32-legs2.png";

    private const int StartXOffset = 63;
    private const int TotalMovementDistance = 318;
    private static readonly TimeSpan timeToMove = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan moveLegsInterval = TimeSpan.FromSeconds(0.6);
    private readonly Image<Rgba32> legs1;
    private readonly Image<Rgba32> legs2;
    private readonly Image<Rgba32> santa;

    private TimeSpan elapsedThisScene;

    public SantaScene()
    {
        IsActive = false;
        HidesTime = false;
        santa = Image.Load<Rgba32>(AssetPaths.Santa(SantaFileName));
        legs1 = Image.Load<Rgba32>(AssetPaths.Santa(Legs1FileName));
        legs2 = Image.Load<Rgba32>(AssetPaths.Santa(Legs2FileName));
    }

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => false;
    public string Name => "Santa";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
        HidesTime = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        elapsedThisScene += timeSpan;
        if (elapsedThisScene > timeToMove)
        {
            IsActive = false;
            HidesTime = false;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        var x = elapsedThisScene.TotalMilliseconds / timeToMove.TotalMilliseconds * TotalMovementDistance;
        var position = new Point(StartXOffset - (int)x, 0);
        var switchLegs = (int)(elapsedThisScene.TotalSeconds / moveLegsInterval.TotalSeconds) % 2 == 0;
        if (IsActive)
        {
            img.Mutate(x => x.DrawImage(santa, position, 1f));
            if (switchLegs)
                img.Mutate(x => x.DrawImage(legs1, position, 1f));
            else
                img.Mutate(x => x.DrawImage(legs2, position, 1f));
        }
    }
}
