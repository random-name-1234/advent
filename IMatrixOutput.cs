using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal interface IMatrixOutput : IDisposable
{
    string Name { get; }

    void Present(Image<Rgba32> frame);
}
