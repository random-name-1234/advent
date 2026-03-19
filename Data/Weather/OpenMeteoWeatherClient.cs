using System.Globalization;
using System.Text.Json;

namespace advent.Data.Weather;

internal static class OpenMeteoWeatherClient
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    public static TimeSpan PreparationTimeout => HttpClient.Timeout + TimeSpan.FromSeconds(2);

    public static async Task<WeatherSnapshot> FetchSnapshotAsync(
        WeatherOptions options,
        int forecastCount,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(forecastCount);

        var lat = options.Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = options.Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        var url =
            $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4";

        using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var root = json.RootElement;
        if (root.TryGetProperty("error", out var hasError) &&
            hasError.ValueKind is JsonValueKind.True)
        {
            var reason = root.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString()
                : "Unknown weather API error.";
            throw new InvalidOperationException(reason);
        }

        var current = root.GetProperty("current");
        var currentTemperature = ReadRequiredFloat(current, "temperature_2m");
        var currentWeatherCode = ReadRequiredInt(current, "weather_code");
        var isDay = ReadRequiredInt(current, "is_day") is 1;

        var daily = root.GetProperty("daily");
        var dates = ReadStringArray(daily, "time");
        var codes = ReadIntArray(daily, "weather_code");
        var maxTemps = ReadFloatArray(daily, "temperature_2m_max");
        var minTemps = ReadFloatArray(daily, "temperature_2m_min");

        var itemCount = Math.Min(dates.Length, Math.Min(codes.Length, Math.Min(maxTemps.Length, minTemps.Length)));
        var forecasts = new List<DailyForecast>(forecastCount);

        for (var index = 0; index < itemCount && forecasts.Count < forecastCount; index++)
        {
            forecasts.Add(new DailyForecast(
                BuildPanelLabel(dates[index], index),
                codes[index],
                maxTemps[index],
                minTemps[index]));
        }

        if (forecasts.Count is 0)
            forecasts.Add(new DailyForecast("TODAY", currentWeatherCode, currentTemperature, currentTemperature));

        return new WeatherSnapshot(currentTemperature, currentWeatherCode, isDay, [.. forecasts]);
    }

    private static string BuildPanelLabel(string isoDate, int offset) => offset switch
    {
        0 => "TODAY",
        1 => "TOM",
        _ when DateTime.TryParseExact(
                isoDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate)
            => parsedDate.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant(),
        _ => DateTime.UtcNow.Date.AddDays(offset).ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant()
    };

    private static float ReadRequiredFloat(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            !element.TryGetDouble(out var value))
            throw new InvalidOperationException($"Weather API response missing '{propertyName}'.");

        return (float)value;
    }

    private static int ReadRequiredInt(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            !element.TryGetInt32(out var value))
            throw new InvalidOperationException($"Weather API response missing '{propertyName}'.");

        return value;
    }

    private static string[] ReadStringArray(JsonElement parent, string propertyName)
        => ReadArray(parent, propertyName, static item => item.GetString() ?? string.Empty);

    private static int[] ReadIntArray(JsonElement parent, string propertyName)
        => ReadArray(parent, propertyName, static item => item.TryGetInt32(out var value) ? value : 0);

    private static float[] ReadFloatArray(JsonElement parent, string propertyName)
        => ReadArray(parent, propertyName, static item => item.TryGetDouble(out var value) ? (float)value : 0f);

    private static T[] ReadArray<T>(JsonElement parent, string propertyName, Func<JsonElement, T> map)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is not JsonValueKind.Array)
            return [];

        var values = new T[element.GetArrayLength()];
        var index = 0;
        foreach (var item in element.EnumerateArray())
            values[index++] = map(item);

        return values;
    }
}
