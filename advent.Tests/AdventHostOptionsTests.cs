using Xunit;

namespace advent.Tests;

public class AdventHostOptionsTests
{
    [Fact]
    public void DefaultsTo64x32MatrixProfile()
    {
        var options = AdventHostOptions.FromArgs([], _ => null);

        Assert.Equal("64x32", options.MatrixProfile.Name);
        Assert.Equal(64, options.MatrixWidth);
        Assert.Equal(32, options.MatrixHeight);
    }

    [Fact]
    public void MatrixSizeArgumentSelects128x64Profile()
    {
        var options = AdventHostOptions.FromArgs(["--matrix-size=128x64"], _ => null);

        Assert.Equal("128x64", options.MatrixProfile.Name);
        Assert.Equal(128, options.MatrixWidth);
        Assert.Equal(64, options.MatrixHeight);
    }

    [Fact]
    public void MatrixSizeEnvironmentVariableIsUsedWhenArgumentIsMissing()
    {
        var options = AdventHostOptions.FromArgs([], name => name == "ADVENT_MATRIX_SIZE" ? "128x64" : null);

        Assert.Equal("128x64", options.MatrixProfile.Name);
        Assert.Equal(128, options.MatrixWidth);
        Assert.Equal(64, options.MatrixHeight);
    }

    [Fact]
    public void UnsupportedMatrixSizeFailsFast()
    {
        var ex = Assert.Throws<ArgumentException>(() => AdventHostOptions.FromArgs(["--matrix-size=96x48"], _ => null));

        Assert.Contains("64x32", ex.Message);
        Assert.Contains("128x64", ex.Message);
    }
}
