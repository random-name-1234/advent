using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed class ClockSceneOverlay(Action<Image<Rgba32>> renderClockOverlay) : ISceneOverlay
{
    public void Advance(TimeSpan timeSpan)
    {
    }

    public bool ShouldRender(SceneRenderFrame frame)
    {
        return !frame.HidesTime && !frame.ClockHandledByTransition;
    }

    public void Render(Image<Rgba32> image, SceneRenderFrame frame)
    {
        renderClockOverlay(image);
    }
}
