using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed record WeatherPanelLayout(
    IReadOnlyList<MatrixTextRun> Text,
    string Temperature,
    Rectangle TemperatureBounds,
    Rgba32 TemperatureColor,
    Rectangle IconBounds,
    int WeatherCode,
    bool IsDay);
