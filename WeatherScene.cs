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

    private static readonly Font HeaderFont = AppFonts.Create(8f);
    private static readonly Font MetricFont = AppFonts.Create(6f);
    private static readonly Font HeroTempFont = AppFonts.Create(13f);
    private static readonly Font DetailFont = AppFonts.Create(6.5f);

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
        var time = (float)elapsedThisScene.TotalSeconds;
        DrawPanelBackdrop(img, isDay: true, WeatherType.PartlyCloudy, time, 0);
        DrawHeader(img, "WEATHER", "LIVE", new Rgba32(240, 247, 255), new Rgba32(255, 223, 120));
        DrawWeatherIcon(img, 4, 7, 19, 2, true);

        var pulse = 0.45f + 0.55f * (0.5f + 0.5f * MathF.Sin(time * 4.2f));
        var loader = Scale(new Rgba32(228, 238, 255), pulse);
        DrawCenteredText(img, "Updating", DetailFont, loader, 34, 12);
        DrawCenteredText(img, "forecast", DetailFont, loader, 34, 19);

        var phase = (int)(elapsedThisScene.TotalMilliseconds / 200) % 3;
        for (var i = 0; i < 3; i++)
        {
            var dotColor = i <= phase ? new Rgba32(245, 248, 255) : new Rgba32(94, 121, 168);
            FillCircle(img, 33 + i * 5, 26, 1, dotColor);
        }
    }

    private void DrawForecastPanels(Image<Rgba32> img, WeatherSnapshot weather)
    {
        var panelIndex = Math.Min(PanelCount - 1, (int)(elapsedThisScene.TotalMilliseconds / PanelDuration.TotalMilliseconds));
        var panelStart = TimeSpan.FromMilliseconds(panelIndex * PanelDuration.TotalMilliseconds);
        var timeIntoPanel = elapsedThisScene - panelStart;
        var transitionStart = PanelDuration - TransitionDuration;

        using var currentPanel = new Image<Rgba32>(Width, Height);
        DrawForecastPanel(currentPanel, weather, panelIndex);

        if (panelIndex < PanelCount - 1 && timeIntoPanel > transitionStart)
        {
            var progress = (float)((timeIntoPanel - transitionStart).TotalMilliseconds /
                                   TransitionDuration.TotalMilliseconds);
            progress = Math.Clamp(progress, 0f, 1f);

            using var nextPanel = new Image<Rgba32>(Width, Height);
            DrawForecastPanel(nextPanel, weather, panelIndex + 1);

            var currentOffset = -(int)MathF.Round(progress * Width);
            var nextOffset = Width + currentOffset;
            Blit(img, currentPanel, currentOffset);
            Blit(img, nextPanel, nextOffset);
            DrawPageIndicators(img, panelIndex, progress);
            return;
        }

        Blit(img, currentPanel, 0);
        DrawPageIndicators(img, panelIndex, 0f);
    }

    private void DrawForecastPanel(Image<Rgba32> img, WeatherSnapshot weather, int panelIndex)
    {
        var forecast = GetForecast(weather, panelIndex);
        var isToday = panelIndex == 0;
        var weatherCode = isToday ? weather.CurrentWeatherCode : forecast.WeatherCode;
        var weatherType = MapWeatherType(weatherCode);
        var useDayPalette = isToday ? weather.IsDay : true;
        var time = (float)elapsedThisScene.TotalSeconds;

        DrawPanelBackdrop(img, useDayPalette, weatherType, time, panelIndex);

        var headerText = forecast.DayLabel;
        var metricText = isToday ? "NOW" : "HIGH";
        var headerColor = useDayPalette ? new Rgba32(242, 247, 255) : new Rgba32(224, 235, 252);
        var metricColor = useDayPalette ? new Rgba32(255, 225, 136) : new Rgba32(189, 209, 255);
        DrawHeader(img, headerText, metricText, headerColor, metricColor);

        var bob = (int)MathF.Round(MathF.Sin(time * 2.1f + panelIndex * 0.9f) * 1.2f);
        DrawWeatherIcon(img, 4, 7 + bob, 19, weatherCode, useDayPalette);

        var heroValue = isToday ? weather.CurrentTemperatureC : forecast.MaxTempC;
        DrawHeroTemperature(img, $"{Math.Round(heroValue, MidpointRounding.AwayFromZero):0}C", useDayPalette);
        DrawInfoBand(img, ConditionLabel(weatherCode), forecast.MaxTempC, forecast.MinTempC);
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
        string metric,
        Rgba32 titleColor,
        Rgba32 metricColor)
    {
        FillRect(img, 0, 0, Width, 8, new Rgba32(5, 11, 21));
        FillRect(img, 0, 8, Width, 1, new Rgba32(108, 150, 208));

        img.Mutate(ctx => ctx.DrawText(title, HeaderFont, titleColor, new PointF(3, -1)));

        var metricSize = TextMeasurer.MeasureSize(metric, new TextOptions(MetricFont));
        var badgeWidth = (int)MathF.Ceiling(metricSize.Width) + 6;
        var badgeX = Width - badgeWidth - 3;
        FillRect(img, badgeX, 1, badgeWidth, 6, new Rgba32(20, 37, 59));
        FillRect(img, badgeX + 1, 2, badgeWidth - 2, 4, new Rgba32(44, 72, 108));
        img.Mutate(ctx => ctx.DrawText(metric, MetricFont, metricColor, new PointF(badgeX + 3, 0)));
    }

    private static void DrawHeroTemperature(Image<Rgba32> img, string text, bool isDay)
    {
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(HeroTempFont));
        var x = Width - textSize.Width - 7f;
        var y = 8f;
        var shadow = isDay ? new Rgba32(6, 18, 42) : new Rgba32(0, 0, 0);
        var color = isDay ? new Rgba32(255, 248, 224) : new Rgba32(240, 246, 255);
        img.Mutate(ctx => ctx.DrawText(text, HeroTempFont, shadow, new PointF(x + 1.5f, y + 1f)));
        img.Mutate(ctx => ctx.DrawText(text, HeroTempFont, color, new PointF(x, y)));
    }

    private static void DrawInfoBand(Image<Rgba32> img, string condition, float highTempC, float lowTempC)
    {
        const int bandY = 22;
        const int bandHeight = 8;
        FillRect(img, 0, bandY, Width, bandHeight, new Rgba32(3, 10, 18));
        FillRect(img, 0, bandY, Width, 1, new Rgba32(110, 150, 208));

        FillRect(img, 2, bandY + 2, 28, 5, new Rgba32(24, 42, 68));
        DrawCenteredText(img, condition, DetailFont, new Rgba32(238, 244, 255), 16, bandY + 1);

        DrawTempChip(
            img,
            33,
            bandY + 1,
            $"H{Math.Round(highTempC, MidpointRounding.AwayFromZero):0}",
            new Rgba32(255, 197, 101),
            new Rgba32(90, 48, 17),
            new Rgba32(18, 14, 10));
        DrawTempChip(
            img,
            49,
            bandY + 1,
            $"L{Math.Round(lowTempC, MidpointRounding.AwayFromZero):0}",
            new Rgba32(147, 210, 255),
            new Rgba32(17, 49, 80),
            new Rgba32(8, 15, 24));
    }

    private static void DrawTempChip(
        Image<Rgba32> img,
        int x,
        int y,
        string text,
        Rgba32 fill,
        Rgba32 border,
        Rgba32 textColor)
    {
        FillRect(img, x, y, 13, 6, border);
        FillRect(img, x + 1, y + 1, 11, 4, fill);
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(DetailFont));
        var textX = x + (13f - textSize.Width) / 2f;
        img.Mutate(ctx => ctx.DrawText(text, DetailFont, textColor, new PointF(textX, y - 1)));
    }

    private static void DrawCenteredText(Image<Rgba32> img, string text, Font font, Rgba32 color, int centerX, int y)
    {
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var left = Math.Clamp(centerX - size.Width / 2f, 2f, Width - size.Width - 2f);
        img.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(left, y)));
    }

    private static void DrawPageIndicators(Image<Rgba32> img, int panelIndex, float transitionProgress)
    {
        var y = Height - 2;
        for (var i = 0; i < PanelCount; i++)
        {
            var intensity = i == panelIndex ? 1f : 0.25f;
            if (i == panelIndex + 1)
                intensity = Math.Max(intensity, transitionProgress);
            if (i == panelIndex)
                intensity = Math.Max(0.25f, 1f - transitionProgress * 0.75f);

            var color = Scale(new Rgba32(232, 240, 255), intensity);
            FillRect(img, 24 + i * 6, y, 4, 1, color);
        }
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

    private static void DrawPanelBackdrop(Image<Rgba32> img, bool isDay, WeatherType weatherType, float time, int panelIndex)
    {
        var (top, bottom, accent) = GetBackdropPalette(isDay, weatherType);

        for (var y = 0; y < Height; y++)
        {
            var t = y / (float)(Height - 1);
            var row = new Rgba32(
                Lerp(top.R, bottom.R, t),
                Lerp(top.G, bottom.G, t),
                Lerp(top.B, bottom.B, t));

            for (var x = 0; x < Width; x++)
                img[x, y] = row;
        }

        DrawGlow(img, Width - 10, 4, isDay ? 5 : 4, Scale(accent, isDay ? 0.45f : 0.28f));
        DrawGlow(img, 9, 12, 4, Scale(accent, 0.18f));

        var accentShift = (int)MathF.Round(MathF.Sin(time * 1.5f + panelIndex) * 2f);
        FillRect(img, 6 + accentShift, 19, 18, 1, Scale(accent, 0.22f));
        FillRect(img, 35 - accentShift, 18, 14, 1, Scale(accent, 0.16f));

        switch (weatherType)
        {
            case WeatherType.Clear:
                DrawClearBackdrop(img, isDay, accent);
                break;
            case WeatherType.PartlyCloudy:
                DrawClearBackdrop(img, isDay, accent);
                DrawCloud(img, 38, 10, 10, new Rgba32(214, 224, 239));
                break;
            case WeatherType.Cloudy:
                DrawCloud(img, 40, 10, 11, new Rgba32(209, 220, 234));
                DrawCloud(img, 48, 14, 8, new Rgba32(190, 203, 223));
                break;
            case WeatherType.Fog:
                DrawCloud(img, 41, 10, 10, new Rgba32(206, 216, 232));
                DrawFogLines(img, 38, 16, 18);
                DrawFogLines(img, 41, 19, 14);
                break;
            case WeatherType.Drizzle:
            case WeatherType.Rain:
                DrawCloud(img, 42, 9, 10, new Rgba32(208, 219, 236));
                DrawBackdropRain(img, time, weatherType == WeatherType.Rain ? 4 : 3);
                break;
            case WeatherType.Snow:
                DrawCloud(img, 42, 9, 10, new Rgba32(225, 235, 246));
                DrawBackdropSnow(img, time);
                break;
            case WeatherType.Thunder:
                DrawCloud(img, 42, 9, 10, new Rgba32(198, 210, 228));
                DrawThunderBolt(img, 48, 15);
                DrawBackdropRain(img, time, 4);
                break;
        }
    }

    private static (Rgba32 Top, Rgba32 Bottom, Rgba32 Accent) GetBackdropPalette(bool isDay, WeatherType weatherType)
    {
        return weatherType switch
        {
            WeatherType.Clear or WeatherType.PartlyCloudy when isDay => (
                new Rgba32(58, 128, 215),
                new Rgba32(13, 59, 132),
                new Rgba32(255, 212, 104)),
            WeatherType.Clear or WeatherType.PartlyCloudy => (
                new Rgba32(8, 19, 52),
                new Rgba32(2, 8, 24),
                new Rgba32(208, 220, 255)),
            WeatherType.Cloudy or WeatherType.Fog => (
                new Rgba32(48, 92, 134),
                new Rgba32(15, 39, 72),
                new Rgba32(166, 196, 232)),
            WeatherType.Drizzle or WeatherType.Rain => (
                new Rgba32(36, 78, 121),
                new Rgba32(10, 28, 61),
                new Rgba32(119, 182, 255)),
            WeatherType.Snow => (
                new Rgba32(54, 106, 142),
                new Rgba32(14, 40, 74),
                new Rgba32(219, 238, 255)),
            WeatherType.Thunder => (
                new Rgba32(28, 41, 78),
                new Rgba32(8, 13, 31),
                new Rgba32(255, 208, 107)),
            _ => (
                new Rgba32(41, 86, 132),
                new Rgba32(10, 31, 63),
                new Rgba32(211, 225, 255))
        };
    }

    private static void DrawClearBackdrop(Image<Rgba32> img, bool isDay, Rgba32 accent)
    {
        if (isDay)
        {
            DrawGlow(img, Width - 10, 5, 4, Scale(accent, 0.55f));
            return;
        }

        FillCircle(img, Width - 10, 5, 2, new Rgba32(244, 246, 255));
        FillCircle(img, Width - 8, 4, 2, new Rgba32(8, 19, 52));
        SetPixel(img, 42, 5, new Rgba32(255, 255, 255));
        SetPixel(img, 49, 8, new Rgba32(214, 224, 255));
        SetPixel(img, 55, 5, new Rgba32(255, 255, 255));
    }

    private static void DrawBackdropRain(Image<Rgba32> img, float time, int streakCount)
    {
        var color = new Rgba32(118, 187, 255);
        var offset = (int)MathF.Round(time * 18f);
        for (var i = 0; i < streakCount; i++)
        {
            var x = 41 + i * 3;
            var y = 12 + (offset + i * 3) % 7;
            DrawLine(img, x, y, x - 1, y + 2, color);
        }
    }

    private static void DrawBackdropSnow(Image<Rgba32> img, float time)
    {
        var drift = (int)MathF.Round(time * 3f);
        var color = new Rgba32(236, 244, 255);
        DrawSnowFlake(img, 40 + drift % 2, 15, color);
        DrawSnowFlake(img, 45 + (drift + 1) % 3, 11, color);
        DrawSnowFlake(img, 50 + (drift + 2) % 2, 17, color);
        DrawSnowFlake(img, 55 + drift % 2, 13, color);
    }

    private static void DrawGlow(Image<Rgba32> img, int centerX, int centerY, int radius, Rgba32 color)
    {
        for (var y = centerY - radius; y <= centerY + radius; y++)
        for (var x = centerX - radius; x <= centerX + radius; x++)
        {
            var dx = x - centerX;
            var dy = y - centerY;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance > radius)
                continue;

            var strength = 1f - distance / radius;
            BlendPixel(img, x, y, color, strength * 0.45f);
        }
    }

    private static byte Lerp(byte start, byte end, float t)
    {
        return (byte)Math.Clamp((int)Math.Round(start + (end - start) * t), 0, 255);
    }

    private static void BlendPixel(Image<Rgba32> img, int x, int y, Rgba32 source, float amount)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return;

        var current = img[x, y];
        var t = Math.Clamp(amount, 0f, 1f);
        img[x, y] = new Rgba32(
            Lerp(current.R, source.R, t),
            Lerp(current.G, source.G, t),
            Lerp(current.B, source.B, t));
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
