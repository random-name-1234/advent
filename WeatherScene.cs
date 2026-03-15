using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class WeatherScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(22);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private static readonly Lock CacheLock = new();
    private static DateTimeOffset cacheUpdatedAtUtc = DateTimeOffset.MinValue;
    private static WeatherSnapshot? cachedSnapshot;

    private static readonly double Latitude = ReadCoordinate("ADVENT_WEATHER_LATITUDE", 52.2053);
    private static readonly double Longitude = ReadCoordinate("ADVENT_WEATHER_LONGITUDE", 0.1218);

    private static readonly Font CurrentTempFont = AppFonts.Create(10f);
    private static readonly Font DayFont = AppFonts.Create(7f);
    private static readonly Font TempFont = AppFonts.Create(7f);

    private TimeSpan elapsedThisScene;
    private Task<WeatherSnapshot>? fetchTask;
    private WeatherSnapshot? snapshot;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Weather";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
        HidesTime = true;

        snapshot = TryGetCachedSnapshot();
        var cacheIsStale = snapshot is null || IsCacheStale();
        if (cacheIsStale) fetchTask = FetchWeatherAsync();
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive) return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
            return;
        }

        if (fetchTask is null || !fetchTask.IsCompleted) return;

        if (fetchTask.IsCompletedSuccessfully)
        {
            snapshot = fetchTask.Result;
            UpdateCache(snapshot);
        }
        else
        {
            var baseException = fetchTask.Exception?.GetBaseException();
            Console.WriteLine($"Weather fetch failed: {baseException?.Message ?? "Unknown error"}");
        }

        fetchTask = null;
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        var currentSnapshot = snapshot;
        var showDayPalette = currentSnapshot?.IsDay ?? true;
        DrawBackground(img, showDayPalette);

        if (currentSnapshot is null)
        {
            DrawLoadingState(img);
            return;
        }

        DrawWeatherPanels(img, currentSnapshot);
    }

    private void DrawLoadingState(Image<Rgba32> img)
    {
        DrawWeatherIcon(img, 24, 6, 14, 3, true);

        var phase = (int)(elapsedThisScene.TotalMilliseconds / 180) % 4;
        for (var i = 0; i < 4; i++)
        {
            var color = i <= phase ? new Rgba32(220, 232, 248) : new Rgba32(75, 92, 128);
            FillCircle(img, 20 + i * 8, 27, 2, color);
        }
    }

    private static void DrawWeatherPanels(Image<Rgba32> img, WeatherSnapshot weather)
    {
        const int panelCount = 3;
        for (var panelIndex = 0; panelIndex < panelCount; panelIndex++)
        {
            var (x, width) = GetPanelBounds(panelIndex);
            DrawPanelFrame(img, weather.IsDay, x, width);
        }

        var (currentX, currentWidth) = GetPanelBounds(0);
        DrawCurrentPanel(img, weather, currentX, currentWidth);

        for (var i = 0; i < 2; i++)
        {
            var (forecastX, forecastWidth) = GetPanelBounds(i + 1);
            if (i < weather.Upcoming.Length)
            {
                DrawForecastPanel(img, weather, weather.Upcoming[i], forecastX, forecastWidth);
            }
            else
            {
                DrawForecastUnavailablePanel(img, forecastX);
            }
        }
    }

    private static (int X, int Width) GetPanelBounds(int panelIndex)
    {
        const int panelCount = 3;
        var baseWidth = Width / panelCount;
        var remainder = Width % panelCount;

        var x = 0;
        for (var i = 0; i < panelIndex; i++) x += baseWidth + (i < remainder ? 1 : 0);

        var panelWidth = baseWidth + (panelIndex < remainder ? 1 : 0);
        return (x, panelWidth);
    }

    private static void DrawPanelFrame(Image<Rgba32> img, bool isDay, int x, int width)
    {
        var panelOuter = isDay ? new Rgba32(19, 41, 78) : new Rgba32(10, 17, 42);
        var panelInner = isDay ? new Rgba32(7, 18, 43) : new Rgba32(4, 10, 30);
        var divider = isDay ? new Rgba32(82, 132, 184) : new Rgba32(63, 95, 145);

        FillRect(img, x, 0, width, Height, panelOuter);
        if (width > 2) FillRect(img, x + 1, 1, width - 2, Height - 2, panelInner);

        if (x == 0) FillRect(img, x, 0, 1, Height, divider);
        if (x + width < Width) FillRect(img, x + width - 1, 0, 1, Height, divider);
    }

    private static void DrawCurrentPanel(Image<Rgba32> img, WeatherSnapshot weather, int x, int width)
    {
        var labelColor = weather.IsDay ? new Rgba32(220, 236, 255) : new Rgba32(204, 219, 248);
        img.Mutate(ctx => ctx.DrawText("NOW", DayFont, labelColor, new PointF(x + 3, 2)));

        var iconSize = Math.Clamp(width - 10, 8, 12);
        var iconX = x + (width - iconSize) / 2;
        DrawWeatherIcon(img, iconX, 9, iconSize, weather.CurrentWeatherCode, weather.IsDay);

        var currentTemp = $"{Math.Round(weather.CurrentTemperatureC, MidpointRounding.AwayFromZero):0}C";
        var tempColor = weather.IsDay ? new Rgba32(246, 238, 200) : new Rgba32(221, 232, 255);
        var tempShadow = weather.IsDay ? new Rgba32(0, 0, 0) : new Rgba32(18, 28, 58);
        img.Mutate(ctx => ctx.DrawText(currentTemp, CurrentTempFont, tempShadow, new PointF(x + 3, 23)));
        img.Mutate(ctx => ctx.DrawText(currentTemp, CurrentTempFont, tempColor, new PointF(x + 2, 22)));
    }

    private static void DrawForecastPanel(Image<Rgba32> img, WeatherSnapshot weather, DailyForecast forecast, int x, int width)
    {
        var labelColor = weather.IsDay ? new Rgba32(220, 236, 255) : new Rgba32(204, 219, 248);
        img.Mutate(ctx => ctx.DrawText(forecast.DayLabel, DayFont, labelColor, new PointF(x + 8, 2)));

        var iconSize = Math.Clamp(width - 12, 7, 10);
        var iconX = x + (width - iconSize) / 2;
        DrawWeatherIcon(img, iconX, 9, iconSize, forecast.WeatherCode, weather.IsDay);

        var highTemp = $"{Math.Round(forecast.MaxTempC, MidpointRounding.AwayFromZero):0}";
        var lowTemp = $"{Math.Round(forecast.MinTempC, MidpointRounding.AwayFromZero):0}";
        var rangeLabel = $"{highTemp}/{lowTemp}";
        var rangeColor = weather.IsDay ? new Rgba32(246, 238, 200) : new Rgba32(221, 232, 255);
        var rangeShadow = weather.IsDay ? new Rgba32(0, 0, 0) : new Rgba32(18, 28, 58);
        img.Mutate(ctx => ctx.DrawText(rangeLabel, TempFont, rangeShadow, new PointF(x + 4, 24)));
        img.Mutate(ctx => ctx.DrawText(rangeLabel, TempFont, rangeColor, new PointF(x + 3, 23)));
    }

    private static void DrawForecastUnavailablePanel(Image<Rgba32> img, int x)
    {
        img.Mutate(ctx => ctx.DrawText("--", DayFont, new Rgba32(156, 176, 208), new PointF(x + 6, 14)));
    }

    private static async Task<WeatherSnapshot> FetchWeatherAsync()
    {
        var lat = Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        var url =
            $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4";

        using var response = await HttpClient.GetAsync(url).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

        var root = json.RootElement;
        if (root.TryGetProperty("error", out var hasError) &&
            hasError.ValueKind == JsonValueKind.True)
        {
            var reason = root.TryGetProperty("reason", out var reasonEl)
                ? reasonEl.GetString()
                : "Unknown weather API error.";
            throw new InvalidOperationException(reason);
        }

        var current = root.GetProperty("current");
        var currentTemperature = ReadRequiredFloat(current, "temperature_2m");
        var currentWeatherCode = ReadRequiredInt(current, "weather_code");
        var isDay = ReadRequiredInt(current, "is_day") == 1;

        var daily = root.GetProperty("daily");
        var time = ReadStringArray(daily, "time");
        var codes = ReadIntArray(daily, "weather_code");
        var maxTemps = ReadFloatArray(daily, "temperature_2m_max");
        var minTemps = ReadFloatArray(daily, "temperature_2m_min");

        var count = Math.Min(time.Length, Math.Min(codes.Length, Math.Min(maxTemps.Length, minTemps.Length)));
        var upcoming = new List<DailyForecast>(3);

        for (var i = 1; i < count && upcoming.Count < 3; i++)
        {
            var dayLabel = BuildDayLabel(time[i], i);
            upcoming.Add(new DailyForecast(dayLabel, codes[i], maxTemps[i], minTemps[i]));
        }

        if (upcoming.Count == 0 && count > 0)
            upcoming.Add(new DailyForecast(BuildDayLabel(time[0], 0), codes[0], maxTemps[0], minTemps[0]));

        return new WeatherSnapshot(currentTemperature, currentWeatherCode, isDay, upcoming.ToArray());
    }

    private static string BuildDayLabel(string isoDate, int fallbackOffset)
    {
        if (DateTime.TryParseExact(
                isoDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
            return DayToSingleLetter(parsedDate.DayOfWeek);

        return DayToSingleLetter(DateTime.UtcNow.Date.AddDays(fallbackOffset).DayOfWeek);
    }

    private static string DayToSingleLetter(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "M",
            DayOfWeek.Tuesday => "T",
            DayOfWeek.Wednesday => "W",
            DayOfWeek.Thursday => "T",
            DayOfWeek.Friday => "F",
            DayOfWeek.Saturday => "S",
            DayOfWeek.Sunday => "S",
            _ => "?"
        };
    }

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
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var values = new string[element.GetArrayLength()];
        var i = 0;
        foreach (var item in element.EnumerateArray()) values[i++] = item.GetString() ?? string.Empty;

        return values;
    }

    private static int[] ReadIntArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        var values = new int[element.GetArrayLength()];
        var i = 0;
        foreach (var item in element.EnumerateArray()) values[i++] = item.TryGetInt32(out var value) ? value : 0;

        return values;
    }

    private static float[] ReadFloatArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Array)
            return Array.Empty<float>();

        var values = new float[element.GetArrayLength()];
        var i = 0;
        foreach (var item in element.EnumerateArray())
            values[i++] = item.TryGetDouble(out var value) ? (float)value : 0f;

        return values;
    }

    private static double ReadCoordinate(string environmentVariable, double fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate))
            return fallback;

        return coordinate;
    }

    private static WeatherSnapshot? TryGetCachedSnapshot()
    {
        lock (CacheLock)
        {
            if (cachedSnapshot == null) return null;

            if (DateTimeOffset.UtcNow - cacheUpdatedAtUtc > CacheLifetime) return null;

            return cachedSnapshot;
        }
    }

    private static bool IsCacheStale()
    {
        lock (CacheLock)
        {
            return cachedSnapshot == null || DateTimeOffset.UtcNow - cacheUpdatedAtUtc > CacheLifetime;
        }
    }

    private static void UpdateCache(WeatherSnapshot weatherSnapshot)
    {
        lock (CacheLock)
        {
            cachedSnapshot = weatherSnapshot;
            cacheUpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void DrawBackground(Image<Rgba32> img, bool isDay)
    {
        var top = isDay ? new Rgba32(20, 70, 122) : new Rgba32(7, 14, 34);
        var bottom = isDay ? new Rgba32(10, 38, 78) : new Rgba32(11, 16, 48);

        for (var y = 0; y < Height; y++)
        {
            var t = y / (float)(Height - 1);
            var r = Lerp(top.R, bottom.R, t);
            var g = Lerp(top.G, bottom.G, t);
            var b = Lerp(top.B, bottom.B, t);

            for (var x = 0; x < Width; x++) img[x, y] = new Rgba32(r, g, b);
        }
    }

    private static byte Lerp(byte start, byte end, float t)
    {
        return (byte)Math.Clamp((int)Math.Round(start + (end - start) * t), 0, 255);
    }

    private static void DrawWeatherIcon(Image<Rgba32> img, int x, int y, int size, int weatherCode, bool isDay)
    {
        var icon = MapWeatherType(weatherCode);
        switch (icon)
        {
            case WeatherType.Clear:
                if (isDay)
                    DrawSun(img, x, y, size);
                else
                    DrawMoon(img, x, y, size);

                break;

            case WeatherType.PartlyCloudy:
                if (isDay)
                    DrawSun(img, x, y, size);
                else
                    DrawMoon(img, x, y, size);

                DrawCloud(img, x + size / 4, y + size / 3, size - 2, new Rgba32(212, 220, 238));
                break;

            case WeatherType.Cloudy:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(205, 215, 233));
                break;

            case WeatherType.Fog:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(200, 208, 228));
                DrawFogLines(img, x + 1, y + size - 2, size - 2);
                break;

            case WeatherType.Drizzle:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(205, 215, 233));
                DrawRainDrops(img, x + 2, y + size - 1, size - 4, 2);
                break;

            case WeatherType.Rain:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(205, 215, 233));
                DrawRainDrops(img, x + 2, y + size - 1, size - 4, 3);
                break;

            case WeatherType.Snow:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(220, 230, 245));
                DrawSnowFlakes(img, x + 2, y + size - 1, size - 4);
                break;

            case WeatherType.Thunder:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(205, 215, 233));
                DrawThunderBolt(img, x + size / 2, y + size / 2 + 1);
                break;

            default:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(205, 215, 233));
                break;
        }
    }

    private static WeatherType MapWeatherType(int weatherCode)
    {
        return weatherCode switch
        {
            0 => WeatherType.Clear,
            1 or 2 => WeatherType.PartlyCloudy,
            3 => WeatherType.Cloudy,
            45 or 48 => WeatherType.Fog,
            51 or 53 or 55 or 56 or 57 => WeatherType.Drizzle,
            61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => WeatherType.Rain,
            71 or 73 or 75 or 77 or 85 or 86 => WeatherType.Snow,
            95 or 96 or 99 => WeatherType.Thunder,
            _ => WeatherType.Unknown
        };
    }

    private static void DrawSun(Image<Rgba32> img, int x, int y, int size)
    {
        var centerX = x + size / 2;
        var centerY = y + size / 2;
        var coreRadius = Math.Max(2, size / 4);

        var rayColor = new Rgba32(255, 208, 84);
        var coreColor = new Rgba32(255, 236, 120);

        DrawLine(img, centerX - coreRadius - 2, centerY, centerX - coreRadius - 1, centerY, rayColor);
        DrawLine(img, centerX + coreRadius + 1, centerY, centerX + coreRadius + 2, centerY, rayColor);
        DrawLine(img, centerX, centerY - coreRadius - 2, centerX, centerY - coreRadius - 1, rayColor);
        DrawLine(img, centerX, centerY + coreRadius + 1, centerX, centerY + coreRadius + 2, rayColor);

        DrawLine(img, centerX - coreRadius - 1, centerY - coreRadius - 1, centerX - coreRadius, centerY - coreRadius,
            rayColor);
        DrawLine(img, centerX + coreRadius, centerY - coreRadius, centerX + coreRadius + 1, centerY - coreRadius - 1,
            rayColor);
        DrawLine(img, centerX - coreRadius - 1, centerY + coreRadius + 1, centerX - coreRadius, centerY + coreRadius,
            rayColor);
        DrawLine(img, centerX + coreRadius, centerY + coreRadius, centerX + coreRadius + 1, centerY + coreRadius + 1,
            rayColor);

        FillCircle(img, centerX, centerY, coreRadius, coreColor);
    }

    private static void DrawMoon(Image<Rgba32> img, int x, int y, int size)
    {
        var centerX = x + size / 2;
        var centerY = y + size / 2;
        var radius = Math.Max(2, size / 4);

        FillCircle(img, centerX, centerY, radius, new Rgba32(244, 246, 255));
        FillCircle(img, centerX + 1, centerY - 1, radius, new Rgba32(10, 20, 52));
    }

    private static void DrawCloud(Image<Rgba32> img, int x, int y, int size, Rgba32 color)
    {
        var width = Math.Max(6, size);
        var baseY = y + size / 3;

        FillCircle(img, x + width / 4, baseY, 2, color);
        FillCircle(img, x + width / 2, baseY - 1, 3, color);
        FillCircle(img, x + 3 * width / 4, baseY, 2, color);
        FillRect(img, x + 1, baseY, width - 2, 3, color);
    }

    private static void DrawRainDrops(Image<Rgba32> img, int x, int y, int width, int drops)
    {
        var color = new Rgba32(120, 188, 255);
        for (var i = 0; i < drops; i++)
        {
            var dropX = x + i * Math.Max(3, width / Math.Max(1, drops - 1));
            DrawLine(img, dropX, y, dropX - 1, y + 2, color);
        }
    }

    private static void DrawSnowFlakes(Image<Rgba32> img, int x, int y, int width)
    {
        var color = new Rgba32(235, 245, 255);
        var firstX = x + 1;
        var secondX = x + Math.Max(4, width - 1);

        DrawSnowFlake(img, firstX, y + 1, color);
        DrawSnowFlake(img, secondX, y + 2, color);
    }

    private static void DrawSnowFlake(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        SetPixel(img, x, y, color);
        SetPixel(img, x - 1, y, color);
        SetPixel(img, x + 1, y, color);
        SetPixel(img, x, y - 1, color);
        SetPixel(img, x, y + 1, color);
    }

    private static void DrawThunderBolt(Image<Rgba32> img, int x, int y)
    {
        var color = new Rgba32(255, 219, 106);
        DrawLine(img, x, y, x - 2, y + 2, color);
        DrawLine(img, x - 2, y + 2, x, y + 2, color);
        DrawLine(img, x, y + 2, x - 1, y + 4, color);
    }

    private static void DrawFogLines(Image<Rgba32> img, int x, int y, int width)
    {
        var color = new Rgba32(175, 188, 205);
        FillRect(img, x, y, width, 1, color);
        FillRect(img, x + 1, y + 2, Math.Max(2, width - 2), 1, color);
    }

    private static void DrawLine(Image<Rgba32> img, int x0, int y0, int x1, int y1, Rgba32 color)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var err = dx + dy;

        while (true)
        {
            SetPixel(img, x0, y0, color);
            if (x0 == x1 && y0 == y1) break;

            var e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static void FillCircle(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        var radiusSquared = radius * radius;
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            var dx = x - centerX;
            var dy = y - centerY;
            if (dx * dx + dy * dy <= radiusSquared) SetPixel(img, x, y, color);
        }
    }

    private static void FillRect(Image<Rgba32> img, int x, int y, int width, int height, Rgba32 color)
    {
        for (var yy = y; yy < y + height; yy++)
        for (var xx = x; xx < x + width; xx++)
            SetPixel(img, xx, yy, color);
    }

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x >= Width || (uint)y >= Height) return;

        img[x, y] = color;
    }

    private sealed record WeatherSnapshot(
        float CurrentTemperatureC,
        int CurrentWeatherCode,
        bool IsDay,
        DailyForecast[] Upcoming);

    private readonly record struct DailyForecast(
        string DayLabel,
        int WeatherCode,
        float MaxTempC,
        float MinTempC);

    private enum WeatherType
    {
        Unknown,
        Clear,
        PartlyCloudy,
        Cloudy,
        Fog,
        Drizzle,
        Rain,
        Snow,
        Thunder
    }
}
