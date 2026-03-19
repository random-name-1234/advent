namespace advent.Data.Weather;

internal interface IWeatherSnapshotSource
{
    bool TryGetSnapshot(out WeatherSnapshot snapshot);
}
