namespace advent.Data.Weather;

internal sealed record WeatherSnapshot(
    float CurrentTemperatureC,
    float FeelsLikeC,
    float WindSpeedMph,
    int CurrentWeatherCode,
    bool IsDay,
    DailyForecast[] Forecasts);

internal readonly record struct DailyForecast(
    string DayLabel,
    int WeatherCode,
    float MaxTempC,
    float MinTempC,
    int PrecipitationProbability,
    float MaxWindSpeedMph);
