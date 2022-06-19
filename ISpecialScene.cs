using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent
{
    public interface ISpecialScene
    {
        bool IsActive { get; }
        bool HidesTime { get; }
        bool RainbowSnow { get; }
        string Name { get; }
        void Activate();
        void Elapsed(TimeSpan timeSpan);
        void Draw(Image<Rgba32> img);
    }
}
