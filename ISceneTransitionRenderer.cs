using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal interface ISceneTransitionRenderer
{
    SceneRenderFrame Render(Image<Rgba32> image, ISpecialScene? activeScene, Action<Image<Rgba32>> renderClockOverlay);
}
