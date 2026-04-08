using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public sealed class SceneRenderer : IDisposable
{
    private readonly ClockRenderer clockRenderer = new();
    private readonly Action<Image<Rgba32>> drawClockOverlay;
    private readonly IReadOnlyList<ISceneOverlay> overlays;
    private readonly ISceneTransitionRenderer transitionRenderer;

    public SceneRenderer(Action<Image<Rgba32>>? clockOverlayRenderer = null)
    {
        Img = new Image<Rgba32>(MatrixConstants.Width, MatrixConstants.Height);
        drawClockOverlay = clockOverlayRenderer ?? clockRenderer.Draw;
        transitionRenderer = new FadingSceneTransitionRenderer(Img.Width, Img.Height);
        overlays =
        [
            new SnowSceneOverlay(),
            new ClockSceneOverlay(drawClockOverlay)
        ];
    }

    private readonly Lock frameLock = new();

    public Image<Rgba32> Img { get; }

    public void AdvanceAndRender(TimeSpan timeSpan, ISpecialScene? activeScene)
    {
        lock (frameLock)
        {
            Img.Mutate(ctx => ctx.Clear(Color.Black));

            foreach (var overlay in overlays)
                overlay.Advance(timeSpan);

            var frame = transitionRenderer.Render(Img, activeScene, drawClockOverlay);
            foreach (var overlay in overlays)
            {
                if (overlay.ShouldRender(frame))
                    overlay.Render(Img, frame);
            }
        }
    }

    public byte[] CaptureFramePng()
    {
        using var snapshot = CaptureFrame();
        using var ms = new MemoryStream();
        snapshot.SaveAsPng(ms);
        return ms.ToArray();
    }

    internal Image<Rgba32> CaptureFrame()
    {
        lock (frameLock)
            return Img.Clone();
    }

    public void Dispose()
    {
        if (transitionRenderer is IDisposable disposableTransitionRenderer)
            disposableTransitionRenderer.Dispose();
        Img.Dispose();
    }
}
