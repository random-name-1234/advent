using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed class MatrixFramePresenter(MatrixProfile profile)
{
    public int LogicalWidth => profile.LogicalWidth;

    public int LogicalHeight => profile.LogicalHeight;

    public int PhysicalWidth => profile.Width;

    public int PhysicalHeight => profile.Height;

    public int HorizontalScale => profile.HorizontalScale;

    public int VerticalScale => profile.VerticalScale;

    public string PresentationDescription => profile.PresentationDescription;

    public Image<Rgba32> CapturePresentedFrame(SceneRenderer renderer)
    {
        using var logicalFrame = renderer.CaptureFrame();
        return CreatePresentedFrame(logicalFrame);
    }

    public byte[] CapturePresentedFramePng(SceneRenderer renderer)
    {
        using var presentedFrame = CapturePresentedFrame(renderer);
        using var ms = new MemoryStream();
        presentedFrame.SaveAsPng(ms);
        return ms.ToArray();
    }

    public Image<Rgba32> CreatePresentedFrame(Image<Rgba32> logicalFrame)
    {
        if (logicalFrame.Width != LogicalWidth || logicalFrame.Height != LogicalHeight)
        {
            throw new InvalidOperationException(
                $"Logical frame size {logicalFrame.Width}x{logicalFrame.Height} does not match expected scene canvas {LogicalWidth}x{LogicalHeight}.");
        }

        if (profile.UsesLogicalCanvas)
            return logicalFrame.Clone();

        var presentedFrame = new Image<Rgba32>(PhysicalWidth, PhysicalHeight);
        for (var y = 0; y < logicalFrame.Height; y++)
        {
            for (var x = 0; x < logicalFrame.Width; x++)
            {
                var pixel = logicalFrame[x, y];
                var startX = x * HorizontalScale;
                var startY = y * VerticalScale;

                for (var dy = 0; dy < VerticalScale; dy++)
                {
                    for (var dx = 0; dx < HorizontalScale; dx++)
                        presentedFrame[startX + dx, startY + dy] = pixel;
                }
            }
        }

        return presentedFrame;
    }
}
