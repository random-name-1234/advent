using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

internal sealed class FadingSceneTransitionRenderer : ISceneTransitionRenderer, IDisposable
{
    private readonly Image<Rgba32> clockScratch;
    private readonly Image<Rgba32> scratch;

    public FadingSceneTransitionRenderer(int width, int height)
    {
        scratch = new Image<Rgba32>(width, height);
        clockScratch = new Image<Rgba32>(width, height);
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

        if (activeScene is IClockTransitionScene clockTransitionScene)
        {
            activeScene.Draw(image);
            DrawOverlayWithOpacity(image, renderClockOverlay, clockTransitionScene.ClockOpacity);
            return new SceneRenderFrame(activeScene, HidesTime: activeScene.HidesTime, ClockHandledByTransition: true);
        }

        activeScene.Draw(image);
        return new SceneRenderFrame(activeScene, hidesTime, ClockHandledByTransition: false);
    }

    public void Dispose()
    {
        clockScratch.Dispose();
        scratch.Dispose();
    }

    private void ClearScratch()
    {
        ClearImage(scratch);
    }

    private void DrawOverlayWithOpacity(
        Image<Rgba32> image,
        Action<Image<Rgba32>> renderOverlay,
        float opacity)
    {
        if (opacity <= 0f)
            return;

        ClearImage(clockScratch);
        renderOverlay(clockScratch);
        image.Mutate(ctx => ctx.DrawImage(clockScratch, new Point(0, 0), opacity));
    }

    private static void ClearImage(Image<Rgba32> image)
    {
        for (var y = 0; y < image.Height; y++)
        for (var x = 0; x < image.Width; x++)
            image[x, y] = Color.Black;
    }
}
