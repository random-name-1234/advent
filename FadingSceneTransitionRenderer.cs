using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

internal sealed class FadingSceneTransitionRenderer : ISceneTransitionRenderer, IDisposable
{
    private readonly Image<Rgba32> scratch;

    public FadingSceneTransitionRenderer(int width, int height)
    {
        scratch = new Image<Rgba32>(width, height);
    }

    public SceneRenderFrame Render(Image<Rgba32> image, ISpecialScene? activeScene, Action<Image<Rgba32>> renderClockOverlay)
    {
        if (activeScene is null)
            return new SceneRenderFrame(null, HidesTime: false, ClockHandledByTransition: false);

        var hidesTime = activeScene.HidesTime;
        if (activeScene is FadingScene fadingScene &&
            fadingScene.CrossfadesClock &&
            fadingScene.Opacity > 0f &&
            fadingScene.Opacity < 1f)
        {
            renderClockOverlay(image);
            ClearScratch();
            activeScene.Draw(scratch);
            image.Mutate(ctx => ctx.DrawImage(scratch, new Point(0, 0), fadingScene.Opacity));
            return new SceneRenderFrame(activeScene, HidesTime: false, ClockHandledByTransition: true);
        }

        activeScene.Draw(image);
        return new SceneRenderFrame(activeScene, hidesTime, ClockHandledByTransition: false);
    }

    public void Dispose()
    {
        scratch.Dispose();
    }

    private void ClearScratch()
    {
        for (var y = 0; y < scratch.Height; y++)
        for (var x = 0; x < scratch.Width; x++)
            scratch[x, y] = Color.Black;
    }
}
