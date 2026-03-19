using System;
using System.Collections.Generic;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public sealed class SceneRenderer : IDisposable
{
    private const string WidestClockSample = "88:88:88";
    private const float PreferredClockFontSize = 16f;
    private const float MinimumClockFontSize = 8f;
    private const float ClockMaxWidth = 62f;

    private readonly Action<Image<Rgba32>> drawClockOverlay;
    private readonly Font font;
    private readonly IReadOnlyList<ISceneOverlay> overlays;
    private readonly ISceneTransitionRenderer transitionRenderer;

    public SceneRenderer(Action<Image<Rgba32>>? clockOverlayRenderer = null)
    {
        Img = new Image<Rgba32>(64, 32);
        drawClockOverlay = clockOverlayRenderer ?? DrawClockOverlay;
        font = AppFonts.CreateFitting(WidestClockSample, PreferredClockFontSize, MinimumClockFontSize, ClockMaxWidth);
        transitionRenderer = new FadingSceneTransitionRenderer(Img.Width, Img.Height);
        overlays =
        [
            new SnowSceneOverlay(),
            new ClockSceneOverlay(drawClockOverlay)
        ];
    }

    public Image<Rgba32> Img { get; }

    public void AdvanceAndRender(TimeSpan timeSpan, ISpecialScene? activeScene)
    {
        Img.Mutate(ctx =>
            ctx.FillPolygon(Color.Black, new PointF(0, 0), new PointF(64, 0), new PointF(64, 32), new PointF(0, 32)));

        foreach (var overlay in overlays)
            overlay.Advance(timeSpan);

        var frame = transitionRenderer.Render(Img, activeScene, drawClockOverlay);
        foreach (var overlay in overlays)
        {
            if (overlay.ShouldRender(frame))
                overlay.Render(Img, frame);
        }
    }

    public void Dispose()
    {
        if (transitionRenderer is IDisposable disposableTransitionRenderer)
            disposableTransitionRenderer.Dispose();
        Img.Dispose();
    }

    private void DrawClockOverlay(Image<Rgba32> img)
    {
        var timeToDisplay = DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss");
        img.Mutate(ctx => ctx.DrawText(timeToDisplay, font, Color.Aqua, new Point(0, 0)));
    }
}
