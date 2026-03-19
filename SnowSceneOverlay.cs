using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

internal sealed class SnowSceneOverlay : ISceneOverlay
{
    private readonly bool enabled = DateTime.Now.Month is 6 or 12;
    private readonly SnowMachine snowMachine = new();

    public void Advance(TimeSpan timeSpan)
    {
        if (enabled)
            snowMachine.Elapsed(timeSpan);
    }

    public bool ShouldRender(SceneRenderFrame frame)
    {
        return enabled;
    }

    public void Render(Image<Rgba32> image, SceneRenderFrame frame)
    {
        var month = DateTime.Now.Month;
        if (month == 6) snowMachine.RainbowSnow = true;
        if (month == 12) snowMachine.RainbowSnow = false;

        foreach (var flake in snowMachine.Flakes)
        {
            image.Mutate(ctx => ctx.Fill(
                flake.Color,
                new EllipsePolygon(flake.Position.X, 32f - flake.Position.Y, flake.Width, flake.Width)));
        }
    }
}
