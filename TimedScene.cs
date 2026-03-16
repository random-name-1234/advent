using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed class TimedScene : ISpecialScene
{
    private readonly ISpecialScene innerScene;
    private readonly TimeSpan maxDuration;
    private TimeSpan elapsedThisScene;

    public TimedScene(ISpecialScene innerScene, TimeSpan? maxDuration = null)
    {
        this.innerScene = innerScene ?? throw new ArgumentNullException(nameof(innerScene));
        this.maxDuration = maxDuration ?? SceneTiming.MaxSceneDuration;
        if (this.maxDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maxDuration), "Timed scene duration must be positive.");
    }

    public bool IsActive { get; private set; }
    public bool HidesTime => IsActive && innerScene.HidesTime;
    public bool RainbowSnow => IsActive && innerScene.RainbowSnow;
    public string Name => innerScene.Name;

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
        innerScene.Activate();
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        var remaining = maxDuration - elapsedThisScene;
        if (remaining <= TimeSpan.Zero)
        {
            IsActive = false;
            return;
        }

        var step = timeSpan <= remaining ? timeSpan : remaining;
        elapsedThisScene += step;
        innerScene.Elapsed(step);

        if (!innerScene.IsActive || elapsedThisScene >= maxDuration)
            IsActive = false;
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive)
            return;

        innerScene.Draw(img);
    }
}
