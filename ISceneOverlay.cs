using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal interface ISceneOverlay
{
    void Advance(TimeSpan timeSpan);
    bool ShouldRender(SceneRenderFrame frame);
    void Render(Image<Rgba32> image, SceneRenderFrame frame);
}
