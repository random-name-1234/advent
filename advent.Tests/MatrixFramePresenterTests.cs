using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class MatrixFramePresenterTests
{
    [Fact]
    public void CompactProfileReturnsCloneAtLogicalSize()
    {
        var presenter = new MatrixFramePresenter(MatrixProfile.Compact64x32);
        using var logicalFrame = new Image<Rgba32>(64, 32);
        var marker = new Rgba32(12, 34, 56);
        logicalFrame[3, 4] = marker;

        using var presentedFrame = presenter.CreatePresentedFrame(logicalFrame);

        Assert.Equal(64, presentedFrame.Width);
        Assert.Equal(32, presentedFrame.Height);
        Assert.Equal(marker, presentedFrame[3, 4]);
        Assert.NotSame(logicalFrame, presentedFrame);
    }

    [Fact]
    public void LargeProfileScalesPixelsIntoTwoByTwoBlocks()
    {
        var presenter = new MatrixFramePresenter(MatrixProfile.Landscape128x64);
        using var logicalFrame = new Image<Rgba32>(64, 32);
        var marker = new Rgba32(200, 100, 50);
        logicalFrame[2, 3] = marker;

        using var presentedFrame = presenter.CreatePresentedFrame(logicalFrame);

        Assert.Equal(128, presentedFrame.Width);
        Assert.Equal(64, presentedFrame.Height);
        Assert.Equal(marker, presentedFrame[4, 6]);
        Assert.Equal(marker, presentedFrame[5, 6]);
        Assert.Equal(marker, presentedFrame[4, 7]);
        Assert.Equal(marker, presentedFrame[5, 7]);
    }
}
