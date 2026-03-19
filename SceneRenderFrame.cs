using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal readonly record struct SceneRenderFrame(
    ISpecialScene? ActiveScene,
    bool HidesTime,
    bool ClockHandledByTransition);
