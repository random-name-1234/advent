using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed class ScalingMatrixOutput : IMatrixOutput
{
    private readonly IMatrixOutput inner;
    private readonly int virtualWidth;
    private readonly int virtualHeight;
    private readonly Image<Rgba32>? scaledBuffer;
    private readonly int scaleX;
    private readonly int scaleY;

    public ScalingMatrixOutput(IMatrixOutput inner, int virtualWidth, int virtualHeight,
        int hardwareWidth, int hardwareHeight)
    {
        this.inner = inner;
        this.virtualWidth = virtualWidth;
        this.virtualHeight = virtualHeight;

        if (hardwareWidth != virtualWidth || hardwareHeight != virtualHeight)
        {
            scaleX = hardwareWidth / virtualWidth;
            scaleY = hardwareHeight / virtualHeight;
            scaledBuffer = new Image<Rgba32>(hardwareWidth, hardwareHeight);
        }
    }

    public string Name => inner.Name;

    public void Present(Image<Rgba32> frame)
    {
        if (scaledBuffer is null)
        {
            inner.Present(frame);
            return;
        }

        frame.ProcessPixelRows(scaledBuffer, (srcAccessor, dstAccessor) =>
        {
            for (var y = 0; y < srcAccessor.Height; y++)
            {
                var srcRow = srcAccessor.GetRowSpan(y);
                for (var dy = 0; dy < scaleY; dy++)
                {
                    var dstRow = dstAccessor.GetRowSpan(y * scaleY + dy);
                    for (var x = 0; x < srcRow.Length; x++)
                    {
                        var pixel = srcRow[x];
                        for (var dx = 0; dx < scaleX; dx++)
                            dstRow[x * scaleX + dx] = pixel;
                    }
                }
            }
        });

        inner.Present(scaledBuffer);
    }

    public void Dispose()
    {
        scaledBuffer?.Dispose();
        inner.Dispose();
    }
}
