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

public class WeatherScene : ISpecialScene, IDeferredActivationScene
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
    private static readonly TimeSpan PreparationTimeout = HttpClient.Timeout + TimeSpan.FromSeconds(2);

    private static readonly Lock CacheLock = new();
    private static DateTimeOffset cacheUpdatedAtUtc = DateTimeOffset.MinValue;
    private static WeatherSnapshot? cachedSnapshot;

    private static readonly double Latitude = ReadCoordinate("ADVENT_WEATHER_LATITUDE", 52.2053);
    private static readonly double Longitude = ReadCoordinate("ADVENT_WEATHER_LONGITUDE", 0.1218);

    private static readonly Font HeroTempFont = AppFonts.Create(10.5f);
    private static readonly DrawingOptions CrispDrawingOptions = new()
    {
        GraphicsOptions = new GraphicsOptions
        {
            Antialias = false
        }
    };

    private static readonly Rgba32 BackgroundColor = new(0, 0, 0);
    private static readonly Rgba32 HeaderColor = new(248, 168, 66);
    private static readonly Rgba32 PrimaryTextColor = new(238, 214, 170);
    private static readonly Rgba32 SecondaryTextColor = new(128, 102, 66);
    private static readonly Rgba32 DividerColor = new(36, 23, 11);
    private static readonly Rgba32 HighTextColor = new(236, 190, 108);
    private static readonly Rgba32 LowTextColor = new(122, 176, 236);
    private static readonly string[] CloudSprite =
    [
        "000111100000",
        "001222221000",
        "012222222100",
        "122222222221",
        "122222222222",
        "033333333330",
        "003333333300"
    ];

    private static readonly string[] FogSprite =
    [
        "111111111000",
        "001111111111",
        "111111111000"
    ];

    private static readonly string[] DrizzleSprite =
    [
        "010001000000",
        "100010000000",
        "000100010000",
        "001000100000"
    ];

    private static readonly string[] RainSprite =
    [
        "010001000100",
        "100010001000",
        "000100010001",
        "001000100010"
    ];

    private static readonly string[] SnowSprite =
    [
        "010000001000",
        "111000111000",
        "010000001000",
        "000000000000"
    ];

    private static readonly string[] ThunderSprite =
    [
        "001100",
        "011000",
        "001100",
        "001000",
        "010000"
    ];

    private readonly Func<CancellationToken, Task<WeatherSnapshot>> fetchWeatherAsync;

    private Image<Rgba32>? currentPanelBuffer;
    private TimeSpan elapsedThisScene;
    private TimeSpan elapsedWaitingForData;
    private bool fetchAttemptStarted;
    private Task<WeatherSnapshot>? fetchTask;
    private Image<Rgba32>? nextPanelBuffer;
    private bool shouldSkipActivation;
    private WeatherSnapshot? snapshot;

    public WeatherScene()
        : this(static cancellationToken => FetchWeatherAsync(cancellationToken))
    {
    }

    private WeatherScene(Func<CancellationToken, Task<WeatherSnapshot>> fetchWeatherAsync)
    {
        this.fetchWeatherAsync = fetchWeatherAsync ?? throw new ArgumentNullException(nameof(fetchWeatherAsync));
    }

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Weather";
    public bool IsReadyToActivate => snapshot is not null;
    public bool ShouldSkipActivation => shouldSkipActivation;

    public void Prepare()
    {
        elapsedWaitingForData = TimeSpan.Zero;
        shouldSkipActivation = false;

        TryApplyFetchResult();
        snapshot ??= TryGetCachedSnapshot();
        if (snapshot is not null)
            return;

        StartFetchIfNeeded();
    }

    public void AdvancePreparation(TimeSpan timeSpan)
    {
        if (snapshot is not null || shouldSkipActivation)
            return;

        elapsedWaitingForData += timeSpan;
        TryApplyFetchResult();
        snapshot ??= TryGetCachedSnapshot();

        if (snapshot is not null)
            return;

        StartFetchIfNeeded();
        if (elapsedWaitingForData < PreparationTimeout)
            return;

        shouldSkipActivation = true;
        Console.WriteLine("Weather scene skipped: live data was not ready in time.");
    }

    public void Activate()
    {
        if (snapshot is null)
        {
            Prepare();
            TryApplyFetchResult();
            snapshot ??= TryGetCachedSnapshot();
        }

        if (snapshot is null)
        {
            IsActive = false;
            HidesTime = false;
            DisposePanelBuffers();
            return;
        }

        EnsurePanelBuffers();
        elapsedThisScene = TimeSpan.Zero;
        IsActive = true;
        HidesTime = true;
        shouldSkipActivation = false;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        TryApplyFetchResult();
        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
            DisposePanelBuffers();
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive || snapshot is null)
            return;

        DrawForecastPanels(img, snapshot);
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
            return;
        }

        Blit(img, currentPanelBuffer!, 0);
    }

    private void DrawForecastPanel(Image<Rgba32> img, WeatherSnapshot weather, int panelIndex)
    {
        FillRect(img, 0, 0, Width, Height, BackgroundColor);

        var forecast = GetForecast(weather, panelIndex);
        var isToday = panelIndex == 0;
        var weatherCode = isToday ? weather.CurrentWeatherCode : forecast.WeatherCode;
        var time = (float)elapsedThisScene.TotalSeconds;

        var headerText = forecast.DayLabel;
        var metricText = isToday ? "NOW" : "HI";
        DrawHeader(img, headerText, metricText);

        var bob = (int)MathF.Round(MathF.Sin(time * 2.1f + panelIndex * 0.9f) * 1.2f);
        DrawWeatherIcon(img, 1, 10 + bob, 12, weatherCode, isToday ? weather.IsDay : true);

        var heroValue = isToday ? weather.CurrentTemperatureC : forecast.MaxTempC;
        DrawHeroTemperature(
            img,
            $"{Math.Round(heroValue, MidpointRounding.AwayFromZero):0}C",
            isToday ? PrimaryTextColor : HighTextColor);
        DrawConditionField(img, ConditionLabel(weatherCode), elapsedThisScene, panelIndex);
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
        FillRect(img, 0, 0, Width, 6, BackgroundColor);
        FillRect(img, 0, 5, Width, 1, DividerColor);

        DrawPixelText(img, title, HeaderColor, 0, 0);

        var badgeWidth = RailDmiText.MeasureWidth(metric) + 4;
        var badgeX = Width - badgeWidth;
        FillRect(img, badgeX, 0, badgeWidth, RailDmiText.Height, DividerColor);
        DrawPixelText(img, metric, PrimaryTextColor, badgeX + 2, 0);
    }

    private static void DrawHeroTemperature(Image<Rgba32> img, string text, Rgba32 color)
    {
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(HeroTempFont));
        var x = Width - textSize.Width - 5f;
        var y = 8f;
        DrawText(img, text, HeroTempFont, color, (int)x, (int)y);
    }

    private static void DrawConditionField(
        Image<Rgba32> img,
        string condition,
        TimeSpan elapsedOnPage,
        int lane)
    {
        const int fieldX = 23;
        const int fieldY = 18;
        const int fieldWidth = 39;

        DrawPixelCenteredBouncingField(img, condition, PrimaryTextColor, fieldX, fieldY, fieldWidth, elapsedOnPage, lane);
    }

    private static void DrawTemperatureSummary(Image<Rgba32> img, float highTempC, float lowTempC)
    {
        const int rowY = 26;
        FillRect(img, 0, rowY - 1, Width, 1, DividerColor);

        var highText = $"HI {Math.Round(highTempC, MidpointRounding.AwayFromZero):0}C";
        var lowText = $"LO {Math.Round(lowTempC, MidpointRounding.AwayFromZero):0}C";

        DrawPixelText(img, highText, HighTextColor, 0, rowY + 1);
        DrawPixelRightAlignedText(img, lowText, LowTextColor, Width - 1, rowY + 1);
    }

    private static void DrawPixelCenteredText(Image<Rgba32> img, string text, Rgba32 color, int centerX, int y)
    {
        RailDmiText.DrawCentered(img, text, centerX, y, color);
    }

    private static void DrawPixelText(Image<Rgba32> img, string text, Rgba32 color, int x, int y)
    {
        RailDmiText.Draw(img, text, x, y, color);
    }

    private static void DrawPixelRightAlignedText(Image<Rgba32> img, string text, Rgba32 color, int rightX, int y)
    {
        RailDmiText.DrawRightAligned(img, text, rightX, y, color);
    }

    private static void DrawPixelCenteredBouncingField(
        Image<Rgba32> img,
        string text,
        Rgba32 color,
        int x,
        int y,
        int width,
        TimeSpan elapsedOnPage,
        int lane)
    {
        if (string.IsNullOrWhiteSpace(text) || width <= 0)
            return;

        var measuredWidth = RailDmiText.MeasureWidth(text);
        if (measuredWidth <= width)
        {
            RailDmiText.Draw(img, text, x + (width - measuredWidth) / 2, y, color);
            return;
        }

        using var field = new Image<Rgba32>(width, RailDmiText.Height);
        var overflow = measuredWidth - width;
        const float speed = 8f;
        const float holdDuration = 0.7f;
        var travelDuration = overflow / speed;
        var cycleDuration = holdDuration + travelDuration + holdDuration + travelDuration;
        var phase = ((float)elapsedOnPage.TotalSeconds + lane * 0.3f) % cycleDuration;

        float offset;
        if (phase < holdDuration)
        {
            offset = 0f;
        }
        else if (phase < holdDuration + travelDuration)
        {
            offset = (phase - holdDuration) * speed;
        }
        else if (phase < holdDuration + travelDuration + holdDuration)
        {
            offset = overflow;
        }
        else
        {
            var returnPhase = phase - holdDuration - travelDuration - holdDuration;
            offset = overflow - returnPhase * speed;
        }

        RailDmiText.Draw(field, text, -(int)MathF.Round(offset), 0, color);
        BlitOpaque(img, field, x, y);
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

    private static void BlitOpaque(Image<Rgba32> destination, Image<Rgba32> source, int left, int top)
    {
        for (var y = 0; y < source.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var pixel = source[x, y];
            if (pixel.A == 0 || (pixel.R == 0 && pixel.G == 0 && pixel.B == 0))
                continue;

            var destX = left + x;
            var destY = top + y;
            if ((uint)destX >= destination.Width || (uint)destY >= destination.Height)
                continue;

            destination[destX, destY] = pixel;
        }
    }

    private static async Task<WeatherSnapshot> FetchWeatherAsync(CancellationToken cancellationToken)
    {
        var lat = Latitude.ToString("0.####", CultureInfo.InvariantCulture);
        var lon = Longitude.ToString("0.####", CultureInfo.InvariantCulture);
        var url =
            $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code,is_day&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4";

        using var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

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
            WeatherType.PartlyCloudy => "PART CLOUD",
            WeatherType.Cloudy => "CLOUDY",
            WeatherType.Fog => "MIST",
            WeatherType.Drizzle => "DRIZZLE",
            WeatherType.Rain => "SHOWERS",
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

    private static void UpdateCache(WeatherSnapshot weatherSnapshot)
    {
        lock (CacheLock)
        {
            cachedSnapshot = weatherSnapshot;
            cacheUpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private void StartFetchIfNeeded()
    {
        if (fetchTask is not null || fetchAttemptStarted)
            return;

        fetchAttemptStarted = true;
        fetchTask = fetchWeatherAsync(CancellationToken.None);
    }

    private void TryApplyFetchResult()
    {
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
                DrawPartlyCloudyIcon(img, x, y, size, isDay);
                break;

            case WeatherType.Cloudy:
                DrawCloudIcon(img, x, y, size, new Rgba32(216, 216, 224));
                break;

            case WeatherType.Fog:
                DrawCloudIcon(img, x, y, size, new Rgba32(198, 198, 204));
                DrawFogOverlay(img, x, y, size, new Rgba32(148, 148, 156));
                break;

            case WeatherType.Drizzle:
                DrawCloudIcon(img, x, y, size, new Rgba32(210, 210, 216));
                DrawPrecipitationOverlay(img, x, y, size, DrizzleSprite, new Rgba32(120, 188, 255));
                break;

            case WeatherType.Rain:
                DrawCloudIcon(img, x, y, size, new Rgba32(210, 210, 216));
                DrawPrecipitationOverlay(img, x, y, size, RainSprite, new Rgba32(120, 188, 255));
                break;

            case WeatherType.Snow:
                DrawCloudIcon(img, x, y, size, new Rgba32(226, 226, 232));
                DrawPrecipitationOverlay(img, x, y, size, SnowSprite, new Rgba32(235, 245, 255));
                break;

            case WeatherType.Thunder:
                DrawCloudIcon(img, x, y, size, new Rgba32(210, 210, 216));
                DrawThunderOverlay(img, x, y, size, new Rgba32(255, 219, 106));
                break;

            default:
                DrawCloudIcon(img, x, y, size, new Rgba32(210, 210, 216));
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

    private static void DrawPartlyCloudyIcon(Image<Rgba32> img, int x, int y, int size, bool isDay)
    {
        var celestialSize = Math.Max(7, size - 3);
        if (isDay)
            DrawSun(img, x, y, celestialSize);
        else
            DrawMoon(img, x, y, celestialSize);

        DrawCloudIcon(img, x + size / 3, y + size / 3, size - size / 3, new Rgba32(214, 214, 220));
    }

    private static void DrawCloudIcon(Image<Rgba32> img, int x, int y, int size, Rgba32 baseColor)
    {
        var cloudWidth = size;
        var cloudHeight = Math.Max(5, (int)MathF.Round(size * 0.58f));
        var highlight = Mix(baseColor, new Rgba32(255, 255, 255), 0.16f);
        var shadow = Mix(baseColor, BackgroundColor, 0.32f);
        DrawScaledSprite(
            img,
            x,
            y,
            cloudWidth,
            cloudHeight,
            CloudSprite,
            token => token switch
            {
                '1' => highlight,
                '2' => baseColor,
                '3' => shadow,
                _ => null
            });
    }

    private static void DrawFogOverlay(Image<Rgba32> img, int x, int y, int size, Rgba32 color)
    {
        var overlayY = y + Math.Max(6, size - Math.Max(3, size / 4));
        DrawScaledSprite(
            img,
            x + 1,
            overlayY,
            Math.Max(8, size - 2),
            Math.Max(3, size / 4),
            FogSprite,
            token => token == '1' ? color : null);
    }

    private static void DrawPrecipitationOverlay(
        Image<Rgba32> img,
        int x,
        int y,
        int size,
        string[] sprite,
        Rgba32 color)
    {
        var overlayY = y + Math.Max(6, size - Math.Max(4, size / 3));
        DrawScaledSprite(
            img,
            x + 1,
            overlayY,
            Math.Max(8, size - 2),
            Math.Max(4, size / 3),
            sprite,
            token => token == '1' ? color : null);
    }

    private static void DrawThunderOverlay(Image<Rgba32> img, int x, int y, int size, Rgba32 color)
    {
        DrawScaledSprite(
            img,
            x + size / 2 - Math.Max(2, size / 7),
            y + Math.Max(5, size / 2 - 1),
            Math.Max(4, size / 3),
            Math.Max(5, size / 2),
            ThunderSprite,
            token => token == '1' ? color : null);
    }

    private static void DrawScaledSprite(
        Image<Rgba32> img,
        int x,
        int y,
        int width,
        int height,
        string[] sprite,
        Func<char, Rgba32?> resolveColor)
    {
        if (width <= 0 || height <= 0 || sprite.Length == 0 || sprite[0].Length == 0)
            return;

        var sourceHeight = sprite.Length;
        var sourceWidth = sprite[0].Length;
        for (var targetY = 0; targetY < height; targetY++)
        for (var targetX = 0; targetX < width; targetX++)
        {
            var sourceX = targetX * sourceWidth / width;
            var sourceY = targetY * sourceHeight / height;
            var token = sprite[sourceY][sourceX];
            var color = resolveColor(token);
            if (color is { } resolved)
                SetPixel(img, x + targetX, y + targetY, resolved);
        }
    }

    private static Rgba32 Mix(Rgba32 source, Rgba32 destination, float amount)
    {
        var t = Math.Clamp(amount, 0f, 1f);
        return new Rgba32(
            (byte)Math.Round(source.R + (destination.R - source.R) * t),
            (byte)Math.Round(source.G + (destination.G - source.G) * t),
            (byte)Math.Round(source.B + (destination.B - source.B) * t));
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
