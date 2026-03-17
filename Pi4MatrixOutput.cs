using MatrixApi;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using MatrixColor = MatrixApi.Color;

namespace advent;

internal sealed class Pi4MatrixOutput : IMatrixOutput
{
    private readonly RGBLedCanvas canvas;
    private readonly RGBLedMatrix matrix;

    public Pi4MatrixOutput(RGBLedMatrixOptions options)
    {
        matrix = new RGBLedMatrix(options);
        canvas = matrix.CreateOffscreenCanvas();
    }

    public string Name => "PI4";

    public void Present(Image<Rgba32> frame)
    {
        frame.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var pixel = row[x];
                    canvas.SetPixel(x, y, new MatrixColor(pixel.R, pixel.G, pixel.B));
                }
            }
        });

        matrix.SwapOnVsync(canvas);
    }

    public void Dispose()
    {
        matrix.Dispose();
    }
}
