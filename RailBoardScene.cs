using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class RailBoardScene : ISpecialScene
{
    private const int Width = 64;
    private const int Height = 32;
    private const int PanelCount = 2;
    private const int RowsPerSection = 2;
    private const int StationFetchRows = 8;

    private static readonly TimeSpan SceneDuration = SceneTiming.MaxSceneDuration;
    private static readonly TimeSpan PanelDuration =
        TimeSpan.FromMilliseconds(SceneDuration.TotalMilliseconds / PanelCount);
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(45);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Lock CacheLock = new();
    private static DateTimeOffset cacheUpdatedAtUtc = DateTimeOffset.MinValue;
    private static RailBoardSnapshot? cachedSnapshot;

    private static readonly Font HeaderFont = AppFonts.Create(6.25f);
    private static readonly Font RowFont = AppFonts.Create(5.75f);

    private readonly RailBoardOptions? options;
    private readonly Func<RailBoardOptions, DateTimeOffset, CancellationToken, Task<RailBoardSnapshot>> fetchSnapshotAsync;
    private readonly Func<DateTimeOffset> nowProvider;

    private Image<Rgba32>? currentPanelBuffer;
    private TimeSpan elapsedThisScene;
    private Task<RailBoardSnapshot>? fetchTask;
    private RailBoardSnapshot? snapshot;

    public RailBoardScene()
        : this(
            RailBoardOptions.TryFromEnvironment(),
            static (options, now, cancellationToken) => FetchSnapshotAsync(options, now, cancellationToken),
            static () => DateTimeOffset.UtcNow)
    {
    }

    public RailBoardScene(
        Func<DateTimeOffset, CancellationToken, Task<RailBoardSnapshot>> fetchSnapshotAsync,
        Func<DateTimeOffset>? nowProvider = null)
        : this(
            RailBoardOptions.CreateForTesting(),
            (_, now, cancellationToken) => fetchSnapshotAsync(now, cancellationToken),
            nowProvider ?? (() => DateTimeOffset.UtcNow))
    {
    }

    private RailBoardScene(
        RailBoardOptions? options,
        Func<RailBoardOptions, DateTimeOffset, CancellationToken, Task<RailBoardSnapshot>> fetchSnapshotAsync,
        Func<DateTimeOffset> nowProvider)
    {
        this.options = options;
        this.fetchSnapshotAsync = fetchSnapshotAsync;
        this.nowProvider = nowProvider;
    }

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "UK Rail Board";

    public static bool IsConfiguredFromEnvironment()
    {
        return RailBoardOptions.TryFromEnvironment() is not null;
    }

    public void Activate()
    {
        EnsurePanelBuffers();
        elapsedThisScene = TimeSpan.Zero;
        fetchTask = null;
        IsActive = true;
        HidesTime = true;

        snapshot = TryGetCachedSnapshot();
        if (options is null)
            return;

        if (snapshot is null || IsCacheStale())
            fetchTask = fetchSnapshotAsync(options, nowProvider(), CancellationToken.None);
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
            Console.WriteLine($"Rail board fetch failed: {baseException?.Message ?? "Unknown error"}");
        }

        fetchTask = null;
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive)
            return;

        if (options is null && snapshot is null)
        {
            DrawStatePanel(img, "RAIL", "CONFIG", "Add LDB creds");
            return;
        }

        if (snapshot is null)
        {
            DrawStatePanel(img, "RAIL", "LIVE", "Loading board");
            return;
        }

        DrawPanels(img, snapshot);
    }

    private void DrawPanels(Image<Rgba32> img, RailBoardSnapshot railSnapshot)
    {
        var panelIndex = Math.Min(PanelCount - 1, (int)(elapsedThisScene.TotalMilliseconds / PanelDuration.TotalMilliseconds));

        EnsurePanelBuffers();
        DrawBoardPanel(currentPanelBuffer!, railSnapshot.Panels[panelIndex], railSnapshot.GeneratedAtLocal);
        Blit(img, currentPanelBuffer!, 0);
    }

    private static void DrawBoardPanel(Image<Rgba32> img, RailBoardPanel panel, DateTimeOffset generatedAtLocal)
    {
        Clear(img);
        DrawPanelStamp(img, generatedAtLocal);
        DrawSection(img, panel.Sections[0], panel.BoardLabel, 0);
        DrawSection(img, panel.Sections[1], panel.BoardLabel, 16);
    }

    private static void DrawStatePanel(Image<Rgba32> img, string title, string badge, string message)
    {
        Clear(img);
        img.Mutate(ctx => ctx.DrawText(title, HeaderFont, new Rgba32(242, 242, 242), new PointF(1, -1)));
        DrawRightLabel(img, badge, 0, new Rgba32(255, 196, 96));
        DrawCenteredText(img, message, HeaderFont, new Rgba32(236, 236, 236), Width / 2, 11);
    }

    private static void DrawPanelStamp(Image<Rgba32> img, DateTimeOffset generatedAtLocal)
    {
        var stamp = generatedAtLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
        img.Mutate(ctx => ctx.DrawText(stamp, HeaderFont, new Rgba32(156, 156, 156), new PointF(23, -1)));
    }

    private static void DrawSection(Image<Rgba32> img, RailStationSection section, string boardLabel, int top)
    {
        img.Mutate(ctx => ctx.DrawText(section.StationLabel, HeaderFont, new Rgba32(242, 242, 242), new PointF(1, top - 1)));
        DrawRightLabel(img, boardLabel, top, BoardAccent(boardLabel));

        if (section.Services.Count == 0)
        {
            var message = section.IsUnavailable ? "NO DATA" : "CLEAR";
            DrawCenteredText(img, message, HeaderFont, new Rgba32(160, 160, 160), Width / 2, top + 6);
            return;
        }

        for (var i = 0; i < section.Services.Count && i < RowsPerSection; i++)
            DrawServiceRow(img, top + 5 + i * 5, section.Services[i], boardLabel);
    }

    private static void DrawRightLabel(Image<Rgba32> img, string text, int y, Rgba32 color)
    {
        var labelSize = TextMeasurer.MeasureSize(text, new TextOptions(HeaderFont));
        img.Mutate(ctx => ctx.DrawText(text, HeaderFont, color, new PointF(Width - labelSize.Width - 2f, y - 1)));
    }

    private static void DrawServiceRow(Image<Rgba32> img, int y, RailServiceRow row, string boardLabel)
    {
        img.Mutate(ctx => ctx.DrawText(row.TimeText, RowFont, BoardAccent(boardLabel), new PointF(1, y - 1)));
        img.Mutate(ctx => ctx.DrawText(row.LocationCode, RowFont, new Rgba32(236, 236, 236), new PointF(25, y - 1)));

        var statusSize = TextMeasurer.MeasureSize(row.StatusText, new TextOptions(RowFont));
        var statusX = Math.Max(47f, Width - statusSize.Width - 1f);
        img.Mutate(ctx => ctx.DrawText(row.StatusText, RowFont, row.StatusColor, new PointF(statusX, y - 1)));
    }

    private static Rgba32 BoardAccent(string boardLabel)
    {
        return string.Equals(boardLabel, "ARR", StringComparison.OrdinalIgnoreCase)
            ? new Rgba32(132, 232, 255)
            : new Rgba32(255, 196, 96);
    }

    private static void Clear(Image<Rgba32> img)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
            img[x, y] = new Rgba32(0, 0, 0);
    }

    private static void DrawCenteredText(Image<Rgba32> img, string text, Font font, Rgba32 color, int centerX, int y)
    {
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var left = Math.Clamp(centerX - size.Width / 2f, 2f, Width - size.Width - 2f);
        img.Mutate(ctx => ctx.DrawText(text, font, color, new PointF(left, y)));
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

    private static async Task<RailBoardSnapshot> FetchSnapshotAsync(
        RailBoardOptions railOptions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ukNow = ConvertToUkTime(now);
        var boardTime = ukNow.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        var requests = new[]
        {
            new RailStationRequest(railOptions.CambridgeCrs, "CBG"),
            new RailStationRequest(railOptions.KingsCrossCrs, "KGX")
        };

        var tasks = requests
            .Select(request => FetchStationPanelsAsync(railOptions, request, boardTime, cancellationToken))
            .ToArray();

        var stationPanels = await Task.WhenAll(tasks).ConfigureAwait(false);
        var generatedAtLocal = stationPanels
            .Select(static x => x.GeneratedAtLocal)
            .Where(static x => x != DateTimeOffset.MinValue)
            .DefaultIfEmpty(ukNow)
            .Max();

        return new RailBoardSnapshot(
            [
                new RailBoardPanel("DEP", stationPanels.Select(static x => x.Departures).ToArray()),
                new RailBoardPanel("ARR", stationPanels.Select(static x => x.Arrivals).ToArray())
            ],
            generatedAtLocal);
    }

    private static async Task<RailStationPanels> FetchStationPanelsAsync(
        RailBoardOptions railOptions,
        RailStationRequest request,
        string boardTime,
        CancellationToken cancellationToken)
    {
        var url =
            $"{railOptions.BaseUrl}/api/20220120/GetArrDepBoardWithDetails/{request.Crs.ToUpperInvariant()}/{boardTime}?numRows={StationFetchRows}&timeWindow=90&services=P";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        ApplyAuthentication(httpRequest, railOptions);

        using var response = await HttpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Rail API returned {(int)response.StatusCode} ({response.ReasonPhrase}) for '{url}'. Body: {body}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var board = await JsonSerializer.DeserializeAsync<StationBoardDto>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (board is null)
        {
            return new RailStationPanels(
                new RailStationSection(request.StationLabel, [], true),
                new RailStationSection(request.StationLabel, [], true),
                DateTimeOffset.MinValue);
        }

        var generatedAtLocal = board.GeneratedAt.HasValue
            ? ConvertToUkTime(board.GeneratedAt.Value)
            : DateTimeOffset.MinValue;

        return new RailStationPanels(
            BuildSection(board, request.StationLabel, RailPanelKind.Departures),
            BuildSection(board, request.StationLabel, RailPanelKind.Arrivals),
            generatedAtLocal);
    }

    private static RailStationSection BuildSection(
        StationBoardDto board,
        string stationLabel,
        RailPanelKind panelKind)
    {
        var services = (board.TrainServices ?? [])
            .Select(service => BuildServiceRow(service, panelKind))
            .Where(static row => row is not null)
            .Cast<RailServiceRow>()
            .OrderBy(row => row.SortTime)
            .Take(RowsPerSection)
            .ToArray();

        return new RailStationSection(stationLabel, services, board.ServicesAreUnavailable);
    }

    private static RailServiceRow? BuildServiceRow(ServiceItemDto service, RailPanelKind panelKind)
    {
        var location = panelKind == RailPanelKind.Departures
            ? PickLocation(service.Destination)
            : PickLocation(service.Origin);
        if (location is null)
            return null;

        var planned = ParseBoardTimestamp(panelKind == RailPanelKind.Departures ? service.Std : service.Sta);
        var estimated = ParseBoardTimestamp(panelKind == RailPanelKind.Departures ? service.Etd : service.Eta);
        if (!planned.HasValue && !estimated.HasValue)
            return null;

        var timeText = planned?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--";
        var locationCode = string.IsNullOrWhiteSpace(location.Crs)
            ? BuildLocationCode(location.LocationName)
            : location.Crs!.Trim().ToUpperInvariant();

        var (statusText, statusColor) = BuildStatus(service, planned, estimated);
        return new RailServiceRow(
            timeText,
            locationCode,
            statusText,
            statusColor,
            estimated ?? planned ?? DateTimeOffset.MaxValue);
    }

    private static EndPointLocationDto? PickLocation(EndPointLocationDto[]? endpoints)
    {
        if (endpoints is null || endpoints.Length == 0)
            return null;

        foreach (var endpoint in endpoints)
        {
            if (endpoint is not null && !string.IsNullOrWhiteSpace(endpoint.Crs))
                return endpoint;
        }

        return endpoints.FirstOrDefault(static endpoint => endpoint is not null);
    }

    private static (string Text, Rgba32 Color) BuildStatus(
        ServiceItemDto service,
        DateTimeOffset? planned,
        DateTimeOffset? estimated)
    {
        if (service.IsCancelled)
            return ("CAN", new Rgba32(255, 132, 126));

        if (service.ServiceIsSuppressed)
            return ("OFF", new Rgba32(255, 166, 108));

        if (planned.HasValue && estimated.HasValue)
        {
            var delayMinutes = (int)Math.Round((estimated.Value - planned.Value).TotalMinutes);
            if (delayMinutes > 0)
                return ($"+{Math.Min(delayMinutes, 99):0}", new Rgba32(255, 206, 118));
        }

        if (!service.PlatformIsHidden && !string.IsNullOrWhiteSpace(service.Platform))
            return ($"P{service.Platform!.Trim()}", new Rgba32(164, 238, 178));

        return ("ON", new Rgba32(186, 232, 164));
    }

    private static DateTimeOffset? ParseBoardTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return parsed;

        return null;
    }

    private static DateTimeOffset ConvertToUkTime(DateTimeOffset value)
    {
        try
        {
            var ukTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
            return TimeZoneInfo.ConvertTime(value, ukTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return value.ToLocalTime();
        }
        catch (InvalidTimeZoneException)
        {
            return value.ToLocalTime();
        }
    }

    private static string BuildLocationCode(string? locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            return "---";

        var cleaned = new string(locationName
            .Where(static c => char.IsLetter(c) || c == ' ')
            .ToArray());
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            var combined = string.Concat(words.Take(2).Select(static word => char.ToUpperInvariant(word[0])));
            if (combined.Length >= 2)
                return combined;
        }

        var letters = new string(cleaned.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return letters.Length == 0 ? "---" : letters;
    }

    private static void ApplyAuthentication(HttpRequestMessage request, RailBoardOptions railOptions)
    {
        if (!string.IsNullOrWhiteSpace(railOptions.AuthHeaderName) &&
            !string.IsNullOrWhiteSpace(railOptions.AuthHeaderValue))
        {
            request.Headers.TryAddWithoutValidation(railOptions.AuthHeaderName, railOptions.AuthHeaderValue);
            return;
        }

        if (!string.IsNullOrWhiteSpace(railOptions.Username) &&
            !string.IsNullOrWhiteSpace(railOptions.Password))
        {
            var bytes = Encoding.UTF8.GetBytes($"{railOptions.Username}:{railOptions.Password}");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(bytes));
            return;
        }

        throw new InvalidOperationException("Rail board credentials are not configured.");
    }

    private static RailBoardSnapshot? TryGetCachedSnapshot()
    {
        lock (CacheLock)
        {
            if (cachedSnapshot is null)
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
            return cachedSnapshot is null || DateTimeOffset.UtcNow - cacheUpdatedAtUtc > CacheLifetime;
        }
    }

    private static void UpdateCache(RailBoardSnapshot railSnapshot)
    {
        lock (CacheLock)
        {
            cachedSnapshot = railSnapshot;
            cacheUpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private void EnsurePanelBuffers()
    {
        currentPanelBuffer ??= new Image<Rgba32>(Width, Height);
    }

    private void DisposePanelBuffers()
    {
        currentPanelBuffer?.Dispose();
        currentPanelBuffer = null;
    }

    private static void FillRect(Image<Rgba32> img, int x, int y, int width, int height, Rgba32 color)
    {
        for (var py = y; py < y + height; py++)
        for (var px = x; px < x + width; px++)
            if ((uint)px < Width && (uint)py < Height)
                img[px, py] = color;
    }

    public sealed record RailBoardSnapshot(IReadOnlyList<RailBoardPanel> Panels, DateTimeOffset GeneratedAtLocal);

    public sealed record RailBoardPanel(
        string BoardLabel,
        IReadOnlyList<RailStationSection> Sections);

    public sealed record RailStationSection(
        string StationLabel,
        IReadOnlyList<RailServiceRow> Services,
        bool IsUnavailable);

    public sealed record RailServiceRow(
        string TimeText,
        string LocationCode,
        string StatusText,
        Rgba32 StatusColor,
        DateTimeOffset SortTime);

    private sealed record RailStationPanels(
        RailStationSection Departures,
        RailStationSection Arrivals,
        DateTimeOffset GeneratedAtLocal);

    private sealed record RailStationRequest(string Crs, string StationLabel);

    private sealed record RailBoardOptions(
        string BaseUrl,
        string CambridgeCrs,
        string KingsCrossCrs,
        string? AuthHeaderName,
        string? AuthHeaderValue,
        string? Username,
        string? Password)
    {
        public static RailBoardOptions? TryFromEnvironment()
        {
            var enabled = ReadBool("ADVENT_RAIL_ENABLED", true);
            if (!enabled)
                return null;

            var consumerKey = ReadString("ADVENT_RAIL_LDB_CONSUMER_KEY", string.Empty);
            var authHeaderName = ReadString("ADVENT_RAIL_LDB_AUTH_HEADER_NAME", string.Empty);
            var authHeaderValue = ReadString("ADVENT_RAIL_LDB_AUTH_HEADER_VALUE", string.Empty);
            if (string.IsNullOrWhiteSpace(authHeaderName) &&
                string.IsNullOrWhiteSpace(authHeaderValue) &&
                !string.IsNullOrWhiteSpace(consumerKey))
            {
                authHeaderName = "x-apikey";
                authHeaderValue = consumerKey;
            }

            var username = ReadString("ADVENT_RAIL_LDB_USERNAME", string.Empty);
            var password = ReadString("ADVENT_RAIL_LDB_PASSWORD", string.Empty);

            var hasHeaderAuth = !string.IsNullOrWhiteSpace(authHeaderName) &&
                                !string.IsNullOrWhiteSpace(authHeaderValue);
            var hasBasicAuth = !string.IsNullOrWhiteSpace(username) &&
                               !string.IsNullOrWhiteSpace(password);
            if (!hasHeaderAuth && !hasBasicAuth)
                return null;

            return new RailBoardOptions(
                ReadString(
                    "ADVENT_RAIL_LDB_BASE_URL",
                    "https://api1.raildata.org.uk/1010-live-arrival-and-departure-boards---staff-version1_0/LDBSVWS")
                    .TrimEnd('/'),
                NormalizeCrs(ReadString("ADVENT_RAIL_CAMBRIDGE_CRS", "CBG")),
                NormalizeCrs(ReadString("ADVENT_RAIL_KINGS_CROSS_CRS", "KGX")),
                authHeaderName,
                authHeaderValue,
                username,
                password);
        }

        public static RailBoardOptions CreateForTesting()
        {
            return new RailBoardOptions(
                "https://example.invalid",
                "CBG",
                "KGX",
                "X-Test",
                "dummy",
                null,
                null);
        }

        private static string NormalizeCrs(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "CBG" : value.Trim().ToUpperInvariant();
        }

        private static string ReadString(string environmentVariable, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static bool ReadBool(string environmentVariable, bool fallback)
        {
            var value = Environment.GetEnvironmentVariable(environmentVariable);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            if (bool.TryParse(value, out var parsed))
                return parsed;

            return value.Trim() switch
            {
                "1" => true,
                "0" => false,
                _ => fallback
            };
        }
    }

    private enum RailPanelKind
    {
        Departures,
        Arrivals
    }
}

public sealed record StationBoardDto(
    string? LocationName,
    string? Crs,
    DateTimeOffset? GeneratedAt,
    bool ServicesAreUnavailable,
    ServiceItemDto[]? TrainServices);

public sealed record ServiceItemDto(
    EndPointLocationDto[]? Origin,
    EndPointLocationDto[]? Destination,
    string? Sta,
    string? Eta,
    string? Std,
    string? Etd,
    string? Platform,
    bool PlatformIsHidden,
    [property: JsonPropertyName("serviceIsSupressed")] bool ServiceIsSuppressed,
    bool IsCancelled);

public sealed record EndPointLocationDto(
    string? LocationName,
    string? Crs,
    string? Via);
