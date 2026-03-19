using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class CatScene : ISpecialScene
{
    private static readonly Point visiblePosition = new(0, -29);
    private static readonly Point hiddenPosition = new(0, 32);
    private static readonly TimeSpan moveUpDownDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan lookLeftTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan lookRightTime = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan lookLeftTime2 = TimeSpan.FromSeconds(4.5);
    private static readonly TimeSpan lookRightTime2 = TimeSpan.FromSeconds(5.5);
    private static readonly TimeSpan lookAheadTime = TimeSpan.FromSeconds(7);
    private static readonly TimeSpan moveDownTime = TimeSpan.FromSeconds(8);
    private readonly Image<Rgba32> eyes;
    private readonly Image<Rgba32> face;

    private TimeSpan elapsedThisScene;
    private Point eyesPosition;
    private Point facePosition;

    public CatScene()
    {
        IsActive = false;
        HidesTime = false;
        face = Image.Load<Rgba32>(AssetPaths.Cat("cat2-face.png"));
        eyes = Image.Load<Rgba32>(AssetPaths.Cat("cat2-eyes.png"));
    }

    public bool IsActive { get; private set; }

    public bool HidesTime { get; private set; }

    public bool RainbowSnow => false;

    public string Name => "Cat";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
        HidesTime = true;
        facePosition = hiddenPosition;
        eyesPosition = hiddenPosition;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        elapsedThisScene += timeSpan;
        if (elapsedThisScene > moveDownTime + moveUpDownDuration)
        {
            IsActive = false;
            HidesTime = false;
            facePosition = hiddenPosition;
            eyesPosition = hiddenPosition;
        }
        else if (moveDownTime < elapsedThisScene)
        {
            var offsetMultiplier = (float)((elapsedThisScene - moveDownTime).TotalMilliseconds /
                                           moveUpDownDuration.TotalMilliseconds);
            facePosition = new Point(0, visiblePosition.Y + (int)(61 * offsetMultiplier));
            eyesPosition = new Point(0, visiblePosition.Y + (int)(61 * offsetMultiplier));
        }
        else if (lookAheadTime < elapsedThisScene)
        {
            facePosition = visiblePosition;
            eyesPosition = visiblePosition;
        }
        else if (lookRightTime2 < elapsedThisScene)
        {
            facePosition = visiblePosition;
            eyesPosition = new Point(2, visiblePosition.Y);
        }
        else if (lookLeftTime2 < elapsedThisScene)
        {
            facePosition = visiblePosition;
            eyesPosition = new Point(-2, visiblePosition.Y);
        }
        else if (lookRightTime < elapsedThisScene)
        {
            facePosition = visiblePosition;
            eyesPosition = new Point(2, visiblePosition.Y);
        }
        else if (lookLeftTime < elapsedThisScene)
        {
            facePosition = visiblePosition;
            eyesPosition = new Point(-2, visiblePosition.Y);
        }
        else if (elapsedThisScene < moveUpDownDuration)
        {
            var offsetMultiplier =
                1f - (float)(elapsedThisScene.TotalMilliseconds / moveUpDownDuration.TotalMilliseconds);
            facePosition = new Point(0, visiblePosition.Y + (int)(61 * offsetMultiplier));
            eyesPosition = new Point(0, visiblePosition.Y + (int)(61 * offsetMultiplier));
        }
        else
        {
            facePosition = visiblePosition;
            eyesPosition = visiblePosition;
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (IsActive)
        {
            img.Mutate(x => x.DrawImage(eyes, eyesPosition, 1f));
            img.Mutate(x => x.DrawImage(face, facePosition, 1f));
        }
    }
}
