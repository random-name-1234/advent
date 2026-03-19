using System.Globalization;

namespace advent.Data.Weather;

internal sealed record WeatherOptions(double Latitude, double Longitude)
{
    public static WeatherOptions FromEnvironment() => new(
        ReadCoordinate("ADVENT_WEATHER_LATITUDE", 52.2053),
        ReadCoordinate("ADVENT_WEATHER_LONGITUDE", 0.1218));

    private static double ReadCoordinate(string environmentVariable, double fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate)
            ? coordinate
            : fallback;
    }
}
