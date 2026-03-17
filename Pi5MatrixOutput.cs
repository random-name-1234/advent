using Pi5MatrixSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed class Pi5MatrixOutput : IMatrixOutput
{
    private readonly Pi5Matrix matrix;

    public Pi5MatrixOutput(Pi5MatrixOptions options)
    {
        matrix = new Pi5Matrix(options);
    }

    public string Name => "PI5";

    public void Present(Image<Rgba32> frame)
    {
        if (frame.Width != matrix.Width || frame.Height != matrix.Height)
            throw new InvalidOperationException(
                $"Frame size {frame.Width}x{frame.Height} does not match Pi 5 matrix geometry {matrix.Width}x{matrix.Height}.");

        if (matrix.Options.Colorspace != Pi5Colorspace.Rgb888Packed)
            throw new InvalidOperationException("Pi5MatrixOutput currently supports only Rgb888Packed.");

        var buffer = matrix.FrameBuffer;
        var offset = 0;
        frame.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    buffer[offset++] = pixel.R;
                    buffer[offset++] = pixel.G;
                    buffer[offset++] = pixel.B;
                }
            }
        });

        matrix.Show();
    }

    public void Dispose()
    {
        matrix.Dispose();
    }
}
