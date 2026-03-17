using Xunit;

namespace advent.Tests;

public class AppFontsTests
{
    [Fact]
    public void CreateFitting_ReturnsFontThatFitsClockWidth()
    {
        var font = AppFonts.CreateFitting("88:88:88", 16f, 8f, 62f);

        var measuredWidth = AppFonts.MeasureWidth("88:88:88", font);

        Assert.InRange(font.Size, 8f, 16f);
        Assert.True(measuredWidth <= 62f, $"Expected the widest clock text to fit within 62px, but measured {measuredWidth}.");
    }

    [Fact]
    public void CreateFitting_UsesPreferredSizeWhenItAlreadyFits()
    {
        var font = AppFonts.CreateFitting("88:88:88", 16f, 8f, 500f);

        Assert.Equal(16f, font.Size);
    }
}
