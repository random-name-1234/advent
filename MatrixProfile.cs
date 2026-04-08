namespace advent;

internal sealed record MatrixProfile(string Name, int Width, int Height)
{
    public static MatrixProfile Compact64x32 { get; } =
        new("64x32", MatrixConstants.Width, MatrixConstants.Height);

    public static MatrixProfile Landscape128x64 { get; } =
        new("128x64", MatrixConstants.Width * 2, MatrixConstants.Height * 2);

    public static MatrixProfile Default => Compact64x32;

    public int LogicalWidth => MatrixConstants.Width;

    public int LogicalHeight => MatrixConstants.Height;

    public int HorizontalScale => Width / LogicalWidth;

    public int VerticalScale => Height / LogicalHeight;

    public bool UsesLogicalCanvas => Width == LogicalWidth && Height == LogicalHeight;

    public string PresentationDescription => UsesLogicalCanvas
        ? "native logical canvas"
        : $"{HorizontalScale}x{VerticalScale} nearest-neighbour scaling";

    public static MatrixProfile FromConfiguration(string? rawSize)
    {
        if (string.IsNullOrWhiteSpace(rawSize))
            return Default;

        return rawSize.Trim().ToLowerInvariant() switch
        {
            "64x32" => Compact64x32,
            "128x64" => Landscape128x64,
            _ => throw new ArgumentException(
                $"Unsupported matrix size '{rawSize}'. Supported sizes are 64x32 and 128x64.",
                nameof(rawSize))
        };
    }
}
