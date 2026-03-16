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
    private const int PanelCount = 3;

    private static readonly TimeSpan SceneDuration = SceneTiming.MaxSceneDuration;
    private static readonly TimeSpan PanelDuration =
        TimeSpan.FromMilliseconds(SceneDuration.TotalMilliseconds / PanelCount);
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(900);
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

    private static readonly Font HeroTempFont = AppFonts.Create(13f);
    private static readonly DrawingOptions CrispDrawingOptions = new()
    {
        GraphicsOptions = new GraphicsOptions
        {
            Antialias = false
        }
    };

    private static readonly Rgba32 BackgroundColor = new(0, 0, 0);
    private static readonly Rgba32 HeaderColor = new(255, 178, 72);
    private static readonly Rgba32 PrimaryTextColor = new(255, 232, 188);
    private static readonly Rgba32 SecondaryTextColor = new(146, 118, 76);
    private static readonly Rgba32 DividerColor = new(36, 23, 11);
    private static readonly Rgba32 HighTextColor = new(255, 206, 120);
    private static readonly Rgba32 LowTextColor = new(160, 204, 255);

    private Image<Rgba32>? currentPanelBuffer;
    private TimeSpan elapsedThisScene;
    private Task<WeatherSnapshot>? fetchTask;
    private Image<Rgba32>? nextPanelBuffer;
    private WeatherSnapshot? snapshot;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Weather";

    public void Activate()
    {
        EnsurePanelBuffers();
        elapsedThisScene = TimeSpan.Zero;
        fetchTask = null;
        IsActive = true;
        HidesTime = true;

        snapshot = TryGetCachedSnapshot();
        var cacheIsStale = snapshot is null || IsCacheStale();
        if (cacheIsStale)
            fetchTask = FetchWeatherAsync();
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
            DisposePanelBuffers();
            return;
        }

        if (fetchTask is null || !fetchTask.IsCompleted)
            return;

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
        if (!IsActive)
            return;

        if (snapshot is null)
        {
            DrawLoadingState(img);
            return;
        }

        DrawForecastPanels(img, snapshot);
    }

    private void DrawLoadingState(Image<Rgba32> img)
    {
        FillRect(img, 0, 0, Width, Height, BackgroundColor);
        DrawHeader(img, "WEATHER", "LIVE");
        DrawWeatherIcon(img, 4, 7, 19, 2, true);

        DrawPixelCenteredText(img, "UPDATING", PrimaryTextColor, 43, 12);
        DrawPixelCenteredText(img, "FORECAST", SecondaryTextColor, 43, 19);

        var phase = (int)(elapsedThisScene.TotalMilliseconds / 200) % 3;
        for (var i = 0; i < 3; i++)
        {
            var dotColor = i <= phase ? HeaderColor : SecondaryTextColor;
            FillCircle(img, 33 + i * 5, 26, 1, dotColor);
        }
    }

    private void DrawForecastPanels(Image<Rgba32> img, WeatherSnapshot weather)
    {
        var panelIndex = Math.Min(PanelCount - 1, (int)(elapsedThisScene.TotalMilliseconds / PanelDuration.TotalMilliseconds));
        var panelStart = TimeSpan.FromMilliseconds(panelIndex * PanelDuration.TotalMilliseconds);
        var timeIntoPanel = elapsedThisScene - panelStart;
        var transitionStart = PanelDuration - TransitionDuration;

        EnsurePanelBuffers();
        DrawForecastPanel(currentPanelBuffer!, weather, panelIndex);

        if (panelIndex < PanelCount - 1 && timeIntoPanel > transitionStart)
        {
            var progress = (float)((timeIntoPanel - transitionStart).TotalMilliseconds /
                                   TransitionDuration.TotalMilliseconds);
            progress = Math.Clamp(progress, 0f, 1f);

            DrawForecastPanel(nextPanelBuffer!, weather, panelIndex + 1);

            var currentOffset = -(int)MathF.Round(progress * Width);
            var nextOffset = Width + currentOffset;
            Blit(img, currentPanelBuffer!, currentOffset);
            Blit(img, nextPanelBuffer!, nextOffset);
            DrawPageIndicators(img, panelIndex, progress);
            return;
        }

        Blit(img, currentPanelBuffer!, 0);
        DrawPageIndicators(img, panelIndex, 0f);
    }

    private void DrawForecastPanel(Image<Rgba32> img, WeatherSnapshot weather, int panelIndex)
    {
        FillRect(img, 0, 0, Width, Height, BackgroundColor);

        var forecast = GetForecast(weather, panelIndex);
        var isToday = panelIndex == 0;
        var weatherCode = isToday ? weather.CurrentWeatherCode : forecast.WeatherCode;
        var time = (float)elapsedThisScene.TotalSeconds;

        var headerText = forecast.DayLabel;
        var metricText = isToday ? "NOW" : "HIGH";
        DrawHeader(img, headerText, metricText);

        var bob = (int)MathF.Round(MathF.Sin(time * 2.1f + panelIndex * 0.9f) * 1.2f);
        DrawWeatherIcon(img, 4, 7 + bob, 19, weatherCode, isToday ? weather.IsDay : true);

        var heroValue = isToday ? weather.CurrentTemperatureC : forecast.MaxTempC;
        DrawHeroTemperature(img, $"{Math.Round(heroValue, MidpointRounding.AwayFromZero):0}C");
        DrawConditionRow(img, ConditionLabel(weatherCode));
        DrawTemperatureSummary(img, forecast.MaxTempC, forecast.MinTempC);
    }

    private static DailyForecast GetForecast(WeatherSnapshot weather, int panelIndex)
    {
        if (weather.Forecasts.Length == 0)
            return new DailyForecast(panelIndex == 0 ? "TODAY" : $"DAY {panelIndex + 1}", weather.CurrentWeatherCode,
                weather.CurrentTemperatureC, weather.CurrentTemperatureC);

        if (panelIndex < weather.Forecasts.Length)
            return weather.Forecasts[panelIndex];

        return weather.Forecasts[^1];
    }

    private static void DrawHeader(
        Image<Rgba32> img,
        string title,
        string metric)
    {
        FillRect(img, 0, 0, Width, 8, BackgroundColor);
        FillRect(img, 0, 7, Width, 1, DividerColor);

        DrawPixelText(img, title, HeaderColor, 2, 1);

        var badgeWidth = RailDmiText.MeasureWidth(metric) + 4;
        var badgeX = Width - badgeWidth - 3;
        FillRect(img, badgeX, 1, badgeWidth, RailDmiText.Height, DividerColor);
        DrawPixelText(img, metric, PrimaryTextColor, badgeX + 2, 1);
    }

    private static void DrawHeroTemperature(Image<Rgba32> img, string text)
    {
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(HeroTempFont));
        var x = Width - textSize.Width - 7f;
        var y = 8f;
        DrawText(img, text, HeroTempFont, SecondaryTextColor, (int)x + 1, (int)y + 1);
        DrawText(img, text, HeroTempFont, PrimaryTextColor, (int)x, (int)y);
    }

    private static void DrawConditionRow(Image<Rgba32> img, string condition)
    {
        const int rowY = 20;
        FillRect(img, 0, rowY, Width, 1, DividerColor);
        DrawPixelCenteredText(img, condition, PrimaryTextColor, Width / 2, rowY + 2);
    }

    private static void DrawTemperatureSummary(Image<Rgba32> img, float highTempC, float lowTempC)
    {
        const int rowY = 25;
        FillRect(img, 0, rowY - 1, Width, 1, DividerColor);

        var highText = $"HI {Math.Round(highTempC, MidpointRounding.AwayFromZero):0}C";
        var lowText = $"LO {Math.Round(lowTempC, MidpointRounding.AwayFromZero):0}C";

        DrawPixelText(img, highText, HighTextColor, 3, rowY + 1);
        DrawPixelRightAlignedText(img, lowText, LowTextColor, Width - 3, rowY + 1);
    }

    private static void DrawPixelCenteredText(Image<Rgba32> img, string text, Rgba32 color, int centerX, int y)
    {
        RailDmiText.DrawCentered(img, text, centerX, y, color);
    }

    private static void DrawPageIndicators(Image<Rgba32> img, int panelIndex, float transitionProgress)
    {
        var y = Height - 1;
        for (var i = 0; i < PanelCount; i++)
        {
            var intensity = i == panelIndex ? 1f : 0.25f;
            if (i == panelIndex + 1)
                intensity = Math.Max(intensity, transitionProgress);
            if (i == panelIndex)
                intensity = Math.Max(0.25f, 1f - transitionProgress * 0.75f);

            var color = Scale(HeaderColor, intensity);
            FillRect(img, 24 + i * 6, y, 4, 1, color);
        }
    }

    private static void DrawPixelText(Image<Rgba32> img, string text, Rgba32 color, int x, int y)
    {
        RailDmiText.Draw(img, text, x, y, color);
    }

    private static void DrawPixelRightAlignedText(Image<Rgba32> img, string text, Rgba32 color, int rightX, int y)
    {
        RailDmiText.DrawRightAligned(img, text, rightX, y, color);
    }

    private static void DrawText(Image<Rgba32> img, string text, Font font, Rgba32 color, int x, int y)
    {
        img.Mutate(ctx => ctx.DrawText(CrispDrawingOptions, text, font, color, new PointF(x, y)));
    }

    private static void Blit(Image<Rgba32> destination, Image<Rgba32> source, int offsetX)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var targetX = x + offsetX;
            if ((uint)targetX >= Width)
                continue;

            destination[targetX, y] = source[x, y];
        }
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
        var forecasts = new List<DailyForecast>(PanelCount);

        for (var i = 0; i < count && forecasts.Count < PanelCount; i++)
        {
            forecasts.Add(new DailyForecast(
                BuildPanelLabel(time[i], i),
                codes[i],
                maxTemps[i],
                minTemps[i]));
        }

        if (forecasts.Count == 0)
        {
            forecasts.Add(new DailyForecast("TODAY", currentWeatherCode, currentTemperature, currentTemperature));
        }

        return new WeatherSnapshot(currentTemperature, currentWeatherCode, isDay, forecasts.ToArray());
    }

    private static string BuildPanelLabel(string isoDate, int offset)
    {
        if (offset == 0)
            return "TODAY";
        if (offset == 1)
            return "TOM";

        if (DateTime.TryParseExact(
                isoDate,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
            return parsedDate.ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant();

        return DateTime.UtcNow.Date.AddDays(offset).ToString("ddd", CultureInfo.InvariantCulture).ToUpperInvariant();
    }

    private static string ConditionLabel(int weatherCode)
    {
        return MapWeatherType(weatherCode) switch
        {
            WeatherType.Clear => "CLEAR",
            WeatherType.PartlyCloudy => "PARTLY",
            WeatherType.Cloudy => "CLOUDY",
            WeatherType.Fog => "FOG",
            WeatherType.Drizzle => "DRIZZLE",
            WeatherType.Rain => "RAIN",
            WeatherType.Snow => "SNOW",
            WeatherType.Thunder => "STORM",
            _ => "OUTLOOK"
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
        foreach (var item in element.EnumerateArray())
            values[i++] = item.GetString() ?? string.Empty;

        return values;
    }

    private static int[] ReadIntArray(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element) ||
            element.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        var values = new int[element.GetArrayLength()];
        var i = 0;
        foreach (var item in element.EnumerateArray())
            values[i++] = item.TryGetInt32(out var value) ? value : 0;

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
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate))
            return fallback;

        return coordinate;
    }

    private static WeatherSnapshot? TryGetCachedSnapshot()
    {
        lock (CacheLock)
        {
            if (cachedSnapshot == null)
                return null;

            if (DateTimeOffset.UtcNow - cacheUpdatedAtUtc > CacheLifetime)
                return null;

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

    private void EnsurePanelBuffers()
    {
        currentPanelBuffer ??= new Image<Rgba32>(Width, Height);
        nextPanelBuffer ??= new Image<Rgba32>(Width, Height);
    }

    private void DisposePanelBuffers()
    {
        currentPanelBuffer?.Dispose();
        currentPanelBuffer = null;

        nextPanelBuffer?.Dispose();
        nextPanelBuffer = null;
    }

    private static Rgba32 Scale(Rgba32 color, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(
            (byte)Math.Clamp((int)Math.Round(color.R * clamped), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.G * clamped), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.B * clamped), 0, 255));
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

                DrawCloud(img, x + size / 4, y + size / 3, size - 2, new Rgba32(214, 214, 220));
                break;

            case WeatherType.Cloudy:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(210, 210, 216));
                break;

            case WeatherType.Fog:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(198, 198, 204));
                DrawFogLines(img, x + 1, y + size - 2, size - 2);
                break;

            case WeatherType.Drizzle:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(210, 210, 216));
                DrawRainDrops(img, x + 2, y + size - 1, size - 4, 2);
                break;

            case WeatherType.Rain:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(210, 210, 216));
                DrawRainDrops(img, x + 2, y + size - 1, size - 4, 3);
                break;

            case WeatherType.Snow:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(226, 226, 232));
                DrawSnowFlakes(img, x + 2, y + size - 1, size - 4);
                break;

            case WeatherType.Thunder:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(210, 210, 216));
                DrawThunderBolt(img, x + size / 2, y + size / 2 + 1);
                break;

            default:
                DrawCloud(img, x + 1, y + size / 3, size - 1, new Rgba32(210, 210, 216));
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
        FillCircle(img, centerX + 1, centerY - 1, radius, BackgroundColor);
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
        var color = new Rgba32(160, 160, 168);
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
            if (x0 == x1 && y0 == y1)
                break;

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
            if (dx * dx + dy * dy <= radiusSquared)
                SetPixel(img, x, y, color);
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
        if ((uint)x >= Width || (uint)y >= Height)
            return;

        img[x, y] = color;
    }

    private sealed record WeatherSnapshot(
        float CurrentTemperatureC,
        int CurrentWeatherCode,
        bool IsDay,
        DailyForecast[] Forecasts);

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
