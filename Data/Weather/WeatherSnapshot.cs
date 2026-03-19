namespace advent.Data.Weather;

internal sealed record WeatherSnapshot(
    float CurrentTemperatureC,
    int CurrentWeatherCode,
    bool IsDay,
    DailyForecast[] Forecasts);

internal readonly record struct DailyForecast(
    string DayLabel,
    int WeatherCode,
    float MaxTempC,
    float MinTempC);
