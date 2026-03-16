using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public sealed class RailBoardScene : ISpecialScene
{
    public static readonly TimeSpan MaxSceneDuration = TimeSpan.FromSeconds(60);

    private const int WideMinWidth = 100;
    private const int WideMinHeight = 48;
    private const int StationFetchRows = 10;
    private const int WideBoardRows = 3;
    private const int CompactBoardRows = 2;

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan BoardPageDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DetailPageDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan JourneyPageDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan AlertPageDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan LoadingPageDuration = TimeSpan.FromSeconds(6);

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex HtmlTagRegex = new("<.*?>", RegexOptions.Compiled);
    private static readonly Lock CacheLock = new();
    private static DateTimeOffset cacheUpdatedAtUtc = DateTimeOffset.MinValue;
    private static RailSceneSnapshot? cachedSnapshot;

    private static readonly Font WideHeaderFont = AppFonts.Create(10f);
    private static readonly Font WideRowFont = AppFonts.Create(8f);
    private static readonly Font WideSmallFont = AppFonts.Create(6f);
    private static readonly DrawingOptions CrispDrawingOptions = new()
    {
        GraphicsOptions = new GraphicsOptions
        {
            Antialias = false
        }
    };

    private static readonly Rgba32 BackgroundColor = new(0, 0, 0);
    private static readonly Rgba32 HeaderColor = new(255, 176, 64);
    private static readonly Rgba32 PrimaryTextColor = new(255, 214, 128);
    private static readonly Rgba32 SecondaryTextColor = new(122, 90, 44);
    private static readonly Rgba32 DividerColor = new(32, 24, 12);
    private static readonly Rgba32 OnTimeColor = new(230, 192, 112);
    private static readonly Rgba32 DelayedColor = new(255, 152, 40);
    private static readonly Rgba32 CancelledColor = new(255, 96, 84);
    private static readonly Rgba32 WarningColor = new(255, 214, 128);
    private static readonly Rgba32 AlertColor = new(255, 84, 72);

    private readonly RailBoardOptions? options;
    private readonly Func<RailBoardOptions, DateTimeOffset, CancellationToken, Task<RailSceneSnapshot>> fetchSnapshotAsync;
    private readonly Func<DateTimeOffset> nowProvider;

    private RailSceneSnapshot? snapshot;
    private Task<RailSceneSnapshot>? fetchTask;
    private IReadOnlyList<RailPage> pages = [];
    private int currentPageIndex;
    private TimeSpan elapsedOnPage;
    private TimeSpan elapsedThisScene;

    public RailBoardScene()
        : this(
            RailBoardOptions.TryFromEnvironment(),
            static (options, now, cancellationToken) => FetchSnapshotAsync(options, now, cancellationToken),
            static () => DateTimeOffset.UtcNow)
    {
    }

    public RailBoardScene(
        Func<DateTimeOffset, CancellationToken, Task<RailSceneSnapshot>> fetchSnapshotAsync,
        Func<DateTimeOffset>? nowProvider = null)
        : this(
            RailBoardOptions.CreateForTesting(),
            (_, now, cancellationToken) => fetchSnapshotAsync(now, cancellationToken),
            nowProvider ?? (() => DateTimeOffset.UtcNow))
    {
    }

    private RailBoardScene(
        RailBoardOptions? options,
        Func<RailBoardOptions, DateTimeOffset, CancellationToken, Task<RailSceneSnapshot>> fetchSnapshotAsync,
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
        elapsedThisScene = TimeSpan.Zero;
        elapsedOnPage = TimeSpan.Zero;
        currentPageIndex = 0;
        fetchTask = null;
        IsActive = true;
        HidesTime = true;

        snapshot = TryGetCachedSnapshot();
        pages = snapshot is not null ? BuildPages(snapshot) : [];

        if (options is not null && (snapshot is null || IsCacheStale()))
            fetchTask = fetchSnapshotAsync(options, nowProvider(), CancellationToken.None);
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        elapsedThisScene += timeSpan;

        if (fetchTask is not null && fetchTask.IsCompleted)
        {
            if (fetchTask.IsCompletedSuccessfully)
            {
                snapshot = fetchTask.Result;
                UpdateCache(snapshot);
                pages = BuildPages(snapshot);
                currentPageIndex = 0;
                elapsedOnPage = TimeSpan.Zero;
            }
            else
            {
                var baseException = fetchTask.Exception?.GetBaseException();
                Console.WriteLine($"Rail board fetch failed: {baseException?.Message ?? "Unknown error"}");
            }

            fetchTask = null;
        }

        if (pages.Count == 0)
        {
            if (fetchTask is null || elapsedThisScene >= LoadingPageDuration)
            {
                IsActive = false;
                HidesTime = false;
            }

            return;
        }

        elapsedOnPage += timeSpan;
        while (currentPageIndex < pages.Count && elapsedOnPage >= pages[currentPageIndex].Duration)
        {
            elapsedOnPage -= pages[currentPageIndex].Duration;
            currentPageIndex++;

            if (currentPageIndex >= pages.Count)
            {
                IsActive = false;
                HidesTime = false;
                return;
            }
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive)
            return;

        Clear(img);

        if (options is null && snapshot is null)
        {
            DrawFallbackPage(img, "NATIONAL RAIL", "NO LIVE DATA", "CONFIGURE API ACCESS");
            return;
        }

        if (pages.Count == 0)
        {
            DrawFallbackPage(img, "NATIONAL RAIL", "LOADING", "UPDATING BOARD DATA");
            return;
        }

        var page = pages[Math.Clamp(currentPageIndex, 0, pages.Count - 1)];
        if (IsWide(img))
            DrawWidePage(img, page, elapsedOnPage);
        else
            DrawCompactPage(img, page, elapsedOnPage);
    }

    private static bool IsWide(Image<Rgba32> img)
    {
        return img.Width >= WideMinWidth && img.Height >= WideMinHeight;
    }

    private static IReadOnlyList<RailPage> BuildPages(RailSceneSnapshot railSnapshot)
    {
        var pages = new List<RailPage>
        {
            BuildBoardPage(railSnapshot.Cambridge, BoardType.Departures, railSnapshot.UpdatedAt),
            BuildBoardPage(railSnapshot.KingsCross, BoardType.Arrivals, railSnapshot.UpdatedAt)
        };

        var journeyService = railSnapshot.Cambridge.Departures
            .FirstOrDefault(static service => string.Equals(service.LocationCode, "KGX", StringComparison.OrdinalIgnoreCase) ||
                                             service.LocationText.Contains("KX", StringComparison.OrdinalIgnoreCase) ||
                                             service.LocationText.Contains("Kings Cross", StringComparison.OrdinalIgnoreCase));
        if (journeyService is not null)
            pages.Add(BuildJourneyPage(railSnapshot.Cambridge, journeyService));

        foreach (var alert in railSnapshot.Cambridge.Alerts
                     .Select(alert => (Station: railSnapshot.Cambridge.StationCode, alert))
                     .Concat(railSnapshot.KingsCross.Alerts.Select(alert => (Station: railSnapshot.KingsCross.StationCode, alert)))
                     .OrderByDescending(static item => item.alert.SeverityWeight)
                     .Take(2))
        {
            pages.Add(BuildAlertPage(alert.Station, alert.alert));
        }

        var detailService = SelectDetailPageService(railSnapshot);
        if (detailService is not null)
            pages.Add(BuildDetailPage(detailService.Value.Service, detailService.Value.Station));

        return pages;
    }

    private static (RailServiceSnapshot Service, RailStationSnapshot Station)? SelectDetailPageService(
        RailSceneSnapshot railSnapshot)
    {
        var candidates = railSnapshot.Cambridge.Departures
            .Select(static service => (Service: service, StationCode: "CAM"))
            .Concat(railSnapshot.KingsCross.Arrivals.Select(static service => (Service: service, StationCode: "KGX")))
            .ToArray();
        if (candidates.Length == 0)
            return null;

        foreach (var candidate in candidates)
        {
            if (candidate.Service.StatusText is not "ON TIME")
            {
                return (candidate.Service,
                    candidate.StationCode == railSnapshot.Cambridge.StationCode ? railSnapshot.Cambridge : railSnapshot.KingsCross);
            }
        }

        var first = candidates[0];
        return (first.Service, first.StationCode == railSnapshot.Cambridge.StationCode ? railSnapshot.Cambridge : railSnapshot.KingsCross);
    }

    private static BoardPage BuildBoardPage(
        RailStationSnapshot station,
        BoardType boardType,
        DateTimeOffset updatedAt)
    {
        var services = boardType == BoardType.Departures ? station.Departures : station.Arrivals;
        var boardRows = BuildBoardRows(station, boardType, services);
        var tickerText = BuildBoardTicker(station, boardType, boardRows);
        return new BoardPage(
            $"{station.HeaderLabel} {BoardTitle(boardType)}",
            station.StationName,
            boardType,
            updatedAt,
            boardRows,
            tickerText,
            DurationForText(BoardPageDuration, tickerText));
    }

    private static IReadOnlyList<RailServiceSnapshot> BuildBoardRows(
        RailStationSnapshot station,
        BoardType boardType,
        IReadOnlyList<RailServiceSnapshot> services)
    {
        if (services.Count > 0)
            return services.Take(WideBoardRows).ToArray();

        var statusText = station.IsUnavailable ? "NO DATA" : "CHECK";
        var detailTicker = station.IsUnavailable
            ? $"{station.StationName.ToUpperInvariant()} BOARD DATA UNAVAILABLE"
            : $"{station.StationName.ToUpperInvariant()} HAS NO SERVICES IN THE CURRENT WINDOW";

        return
        [
            new RailServiceSnapshot(
                "--:--",
                station.IsUnavailable ? "NO LIVE DATA" : "NO SERVICES",
                boardType == BoardType.Departures ? "DEP" : "ARR",
                "--",
                statusText,
                station.IsUnavailable ? AlertColor : SecondaryTextColor,
                string.Empty,
                string.Empty,
                detailTicker,
                DateTimeOffset.MaxValue)
        ];
    }

    private static ServiceDetailPage BuildDetailPage(RailServiceSnapshot service, RailStationSnapshot station)
    {
        var title = $"{station.HeaderLabel} SERVICE DETAIL";
        return new ServiceDetailPage(
            title,
            service.ScheduledText,
            service.LocationText,
            service.PlatformText,
            service.StatusText,
            service.StatusColor,
            service.OperatorText,
            service.DetailTicker,
            DurationForText(DetailPageDuration, service.DetailTicker));
    }

    private static JourneyPage BuildJourneyPage(RailStationSnapshot station, RailServiceSnapshot service)
    {
        var ticker = string.IsNullOrWhiteSpace(service.CallingText)
            ? service.DetailTicker
            : $"CALLS {service.CallingText}";
        return new JourneyPage(
            $"{station.HeaderLabel} -> KGX",
            "NEXT SERVICE",
            service.ScheduledText,
            service.PlatformText,
            service.LocationText,
            service.StatusText,
            service.StatusColor,
            ticker,
            DurationForText(JourneyPageDuration, ticker));
    }

    private static AlertPage BuildAlertPage(string stationLabel, RailAlertSnapshot alert)
    {
        var lines = WrapText(alert.Message, 28, 3);
        return new AlertPage(
            "RAIL ALERT",
            stationLabel,
            lines,
            alert.Message,
            AlertPageDuration);
    }

    private static string BuildBoardTicker(
        RailStationSnapshot station,
        BoardType boardType,
        IReadOnlyList<RailServiceSnapshot> services)
    {
        if (station.IsUnavailable)
            return $"{station.StationName.ToUpperInvariant()} BOARD DATA UNAVAILABLE";

        if (services.Count == 0)
        {
            return station.Alerts.Count > 0
                ? station.Alerts[0].Message
                : $"{station.StationName.ToUpperInvariant()} {BoardTitle(boardType)}";
        }

        var focus = services[0];
        return focus.DetailTicker;
    }

    private static TimeSpan DurationForText(TimeSpan baseDuration, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return baseDuration;

        var extraSeconds = Math.Min(5, Math.Max(0, text.Length - 42) / 18.0);
        return baseDuration + TimeSpan.FromSeconds(extraSeconds);
    }

    private static void DrawWidePage(Image<Rgba32> img, RailPage page, TimeSpan elapsedOnPage)
    {
        switch (page)
        {
            case BoardPage boardPage:
                DrawWideBoardPage(img, boardPage, elapsedOnPage);
                break;
            case ServiceDetailPage serviceDetailPage:
                DrawWideDetailPage(img, serviceDetailPage, elapsedOnPage);
                break;
            case JourneyPage journeyPage:
                DrawWideJourneyPage(img, journeyPage, elapsedOnPage);
                break;
            case AlertPage alertPage:
                DrawWideAlertPage(img, alertPage, elapsedOnPage);
                break;
        }
    }

    private static void DrawCompactPage(Image<Rgba32> img, RailPage page, TimeSpan elapsedOnPage)
    {
        switch (page)
        {
            case BoardPage boardPage:
                DrawCompactBoardPage(img, boardPage, elapsedOnPage);
                break;
            case ServiceDetailPage serviceDetailPage:
                DrawCompactDetailPage(img, serviceDetailPage, elapsedOnPage);
                break;
            case JourneyPage journeyPage:
                DrawCompactJourneyPage(img, journeyPage, elapsedOnPage);
                break;
            case AlertPage alertPage:
                DrawCompactAlertPage(img, alertPage, elapsedOnPage);
                break;
        }
    }

    private static void DrawWideBoardPage(Image<Rgba32> img, BoardPage page, TimeSpan elapsedOnPage)
    {
        FillRect(img, 0, 0, img.Width, 1, DividerColor);
        DrawText(img, page.Header, WideHeaderFont, HeaderColor, 3, 2);
        DrawText(img, page.UpdatedAt.ToString("HH:mm", CultureInfo.InvariantCulture), WideSmallFont, SecondaryTextColor,
            img.Width - 28, 4);

        var columnsY = 13;
        DrawText(img, "TIME", WideSmallFont, SecondaryTextColor, 3, columnsY);
        DrawText(img, page.BoardType == BoardType.Departures ? "DESTINATION" : "ORIGIN", WideSmallFont, SecondaryTextColor, 30,
            columnsY);
        DrawText(img, "P", WideSmallFont, SecondaryTextColor, img.Width - 42, columnsY);
        DrawText(img, "STATUS", WideSmallFont, SecondaryTextColor, img.Width - 26, columnsY);
        FillRect(img, 0, 21, img.Width, 1, DividerColor);

        var rowTop = 24;
        var rowHeight = 11;
        for (var i = 0; i < page.Rows.Count && i < WideBoardRows; i++)
        {
            var row = page.Rows[i];
            var y = rowTop + i * rowHeight;
            DrawText(img, row.ScheduledText, WideRowFont, HeaderColor, 3, y);
            DrawScrollingField(img, row.LocationText, WideRowFont, PrimaryTextColor, 30, y, img.Width - 62, 10, elapsedOnPage, i);
            DrawText(img, row.PlatformText, WideRowFont, PrimaryTextColor, img.Width - 42, y);
            DrawText(img, row.StatusText, WideSmallFont, row.StatusColor, img.Width - 26, y + 1);

            if (i < page.Rows.Count - 1)
                FillRect(img, 0, y + rowHeight - 1, img.Width, 1, DividerColor);
        }

        DrawTicker(img, page.TickerText, elapsedOnPage);
    }

    private static void DrawWideDetailPage(Image<Rgba32> img, ServiceDetailPage page, TimeSpan elapsedOnPage)
    {
        DrawText(img, page.Header, WideHeaderFont, HeaderColor, 3, 2);
        DrawText(img, page.TimeText, WideHeaderFont, PrimaryTextColor, 4, 16);
        DrawText(img, page.PlatformText, WideHeaderFont, WarningColor, img.Width - 26, 16);
        DrawScrollingField(img, page.LocationText, WideHeaderFont, PrimaryTextColor, 4, 30, img.Width - 8, 12, elapsedOnPage, 0);
        DrawText(img, page.StatusText, WideHeaderFont, page.StatusColor, 4, 44);
        DrawText(img, page.OperatorText, WideSmallFont, SecondaryTextColor, img.Width - 48, 46);
        DrawTicker(img, page.TickerText, elapsedOnPage);
    }

    private static void DrawWideJourneyPage(Image<Rgba32> img, JourneyPage page, TimeSpan elapsedOnPage)
    {
        DrawText(img, page.Header, WideHeaderFont, HeaderColor, 3, 2);
        DrawText(img, page.Subtitle, WideSmallFont, SecondaryTextColor, 4, 16);
        DrawText(img, page.TimeText, WideHeaderFont, PrimaryTextColor, 4, 26);
        DrawText(img, page.PlatformText, WideHeaderFont, WarningColor, img.Width - 26, 26);
        DrawText(img, page.StatusText, WideHeaderFont, page.StatusColor, 4, 42);
        DrawScrollingField(img, page.LocationText, WideRowFont, PrimaryTextColor, 56, 27, img.Width - 60, 10, elapsedOnPage, 0);
        DrawTicker(img, page.TickerText, elapsedOnPage);
    }

    private static void DrawWideAlertPage(Image<Rgba32> img, AlertPage page, TimeSpan elapsedOnPage)
    {
        DrawText(img, page.Header, WideHeaderFont, AlertColor, 3, 2);
        DrawText(img, page.StationText, WideSmallFont, WarningColor, 4, 16);
        for (var i = 0; i < page.Lines.Count; i++)
            DrawText(img, page.Lines[i], WideHeaderFont, PrimaryTextColor, 4, 24 + i * 10);

        DrawTicker(img, page.TickerText, elapsedOnPage);
    }

    private static void DrawCompactBoardPage(Image<Rgba32> img, BoardPage page, TimeSpan elapsedOnPage)
    {
        DrawCompactHeader(img, CompactBoardTitle(page), page.UpdatedAt.ToString("HH:mm", CultureInfo.InvariantCulture), HeaderColor);
        var rowTop = 9;
        var rowHeight = 11;
        var rows = page.Rows.Take(CompactBoardRows).ToArray();
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var y = rowTop + i * rowHeight;
            DrawPixelText(img, row.ScheduledText, HeaderColor, 1, y);
            DrawPixelText(img, FitCompactBoardLocation(row.LocationText), PrimaryTextColor, 25, y);
            DrawPixelRightAlignedText(img, CompactPlatform(row.PlatformText), PrimaryTextColor, img.Width - 2, y);
            DrawPixelText(img, CompactBoardStatus(row.StatusText), row.StatusColor, 25, y + 5);

            if (i < rows.Length - 1)
                FillRect(img, 1, y + rowHeight - 1, img.Width - 2, 1, DividerColor);
        }
    }

    private static void DrawCompactDetailPage(Image<Rgba32> img, ServiceDetailPage page, TimeSpan elapsedOnPage)
    {
        DrawCompactHeader(img, CompactPageTitle(page.Header, "DETAIL"), page.TimeText, HeaderColor);
        DrawPixelScrollingField(img, page.LocationText, PrimaryTextColor, 1, 10, img.Width - 2, elapsedOnPage, 0);
        DrawPixelText(img, CompactStatus(page.StatusText), page.StatusColor, 1, 18);
        DrawPixelRightAlignedText(img, CompactPlatform(page.PlatformText), PrimaryTextColor, img.Width - 2, 18);
        DrawCompactFooter(img, BuildCompactDetailFooter(page), elapsedOnPage, 1);
    }

    private static void DrawCompactJourneyPage(Image<Rgba32> img, JourneyPage page, TimeSpan elapsedOnPage)
    {
        DrawCompactHeader(img, CompactPageTitle(page.Header, "TRIP"), page.TimeText, HeaderColor);
        DrawPixelText(img, RailDmiText.TrimToWidth(page.Subtitle, img.Width - 2), SecondaryTextColor, 1, 10);
        DrawPixelText(img, CompactStatus(page.StatusText), page.StatusColor, 1, 18);
        DrawPixelRightAlignedText(img, CompactPlatform(page.PlatformText), PrimaryTextColor, img.Width - 2, 18);
        DrawCompactFooter(img, page.TickerText, elapsedOnPage, 0);
    }

    private static void DrawCompactAlertPage(Image<Rgba32> img, AlertPage page, TimeSpan elapsedOnPage)
    {
        DrawCompactHeader(img, "ALERT", CompactStationLabel(page.StationText), AlertColor);
        var lines = WrapText(page.TickerText, 16, 2);
        if (lines.Count > 0)
            DrawPixelText(img, RailDmiText.TrimToWidth(lines[0], img.Width - 2), PrimaryTextColor, 1, 10);
        if (lines.Count > 1)
            DrawPixelText(img, RailDmiText.TrimToWidth(lines[1], img.Width - 2), PrimaryTextColor, 1, 18);

        if (NeedsCompactFooter(page.TickerText, 16, 2))
            DrawCompactFooter(img, page.TickerText, elapsedOnPage, 0);
    }

    private static void DrawFallbackPage(Image<Rgba32> img, string title, string line1, string line2)
    {
        if (IsWide(img))
        {
            DrawText(img, title, WideHeaderFont, HeaderColor, 3, 4);
            DrawText(img, line1, WideHeaderFont, PrimaryTextColor, 3, 22);
            DrawText(img, line2, WideSmallFont, SecondaryTextColor, 3, 40);
            return;
        }

        DrawPixelText(img, title, HeaderColor, 1, 1);
        DrawPixelText(img, line1, PrimaryTextColor, 1, 11);
        DrawPixelText(img, line2, SecondaryTextColor, 1, 22);
    }

    private static void DrawTicker(Image<Rgba32> img, string text, TimeSpan elapsedOnPage)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var y = img.Height - 8;
        FillRect(img, 0, y - 2, img.Width, 1, DividerColor);
        DrawScrollingField(img, text, WideSmallFont, SecondaryTextColor, 3, y, img.Width - 6, 8, elapsedOnPage, 0);
    }

    private static void DrawCompactHeader(Image<Rgba32> img, string leftText, string rightText, Rgba32 leftColor)
    {
        FillRect(img, 0, 7, img.Width, 1, DividerColor);
        DrawPixelText(img, leftText, leftColor, 1, 1);
        if (!string.IsNullOrWhiteSpace(rightText))
            DrawPixelRightAlignedText(img, rightText, SecondaryTextColor, img.Width - 2, 1);
    }

    private static void DrawCompactFooter(Image<Rgba32> img, string text, TimeSpan elapsedOnPage, int lane)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        var y = img.Height - RailDmiText.Height - 1;
        FillRect(img, 0, y - 1, img.Width, 1, DividerColor);
        DrawPixelScrollingField(img, text, SecondaryTextColor, 1, y, img.Width - 2, elapsedOnPage, lane);
    }

    private static string CompactBoardTitle(BoardPage page)
    {
        return $"{CompactStationLabel(page.StationName)} {(page.BoardType == BoardType.Departures ? "DEP" : "ARR")}";
    }

    private static string CompactPageTitle(string header, string fallbackSuffix)
    {
        var token = header.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var station = string.IsNullOrWhiteSpace(token) ? string.Empty : CompactStationLabel(token);
        return string.IsNullOrWhiteSpace(station) ? fallbackSuffix : $"{station} {fallbackSuffix}";
    }

    private static string BuildCompactBoardFooter(BoardPage page)
    {
        var updated = $"UPD {page.UpdatedAt:HH:mm}";
        return string.IsNullOrWhiteSpace(page.TickerText) ? updated : $"{updated}  •  {page.TickerText}";
    }

    private static string BuildCompactDetailFooter(ServiceDetailPage page)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(page.OperatorText))
            parts.Add(CompactOperatorText(page.OperatorText));

        var callsText = ExtractCallsText(page.TickerText);
        if (!string.IsNullOrWhiteSpace(callsText))
            parts.Add(callsText);

        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(page.TickerText))
            parts.Add(page.TickerText);

        return string.Join("  •  ", parts);
    }

    private static string CompactStationLabel(string stationName)
    {
        return stationName.Trim().ToUpperInvariant() switch
        {
            "CAMBRIDGE" => "CAM",
            "LONDON KINGS CROSS" => "KGX",
            "KINGS CROSS" => "KGX",
            "LONDON KING'S CROSS" => "KGX",
            _ => BuildLocationCode(stationName)
        };
    }

    private static string CompactPlatform(string platformText)
    {
        if (string.IsNullOrWhiteSpace(platformText) || platformText == "--")
            return string.Empty;

        return platformText.Trim().ToUpperInvariant();
    }

    private static string CompactBoardStatus(string status)
    {
        return status switch
        {
            "ON TIME" => "ON TIME",
            "SEE FRONT" => "SEE FRNT",
            _ when status.Length > 8 => status[..8],
            _ => status
        };
    }

    private static string FitCompactBoardLocation(string locationText)
    {
        var normalized = CompactBoardLocation(locationText);
        if (RailDmiText.MeasureWidth(normalized) <= 28)
            return normalized;

        var code = BuildLocationCode(locationText);
        if (RailDmiText.MeasureWidth(code) <= 28)
            return code;

        return RailDmiText.TrimToWidth(normalized, 28);
    }

    private static string CompactBoardLocation(string locationText)
    {
        return locationText.Trim().ToUpperInvariant() switch
        {
            "CAMBRIDGE" => "CAM",
            "LONDON KX" => "KGX",
            "LONDON KINGS CROSS" => "KGX",
            "KINGS CROSS" => "KGX",
            "LIV ST" => "LST",
            "LONDON LIVERPOOL STREET" => "LST",
            "PETERBORO" => "PBO",
            "PETERBOROUGH" => "PBO",
            "FINSBURY PK" => "FPK",
            "LONDON LIVERPOOL ST" => "LST",
            "STEVENAGE" => "SVG",
            _ => locationText.Trim().ToUpperInvariant()
        };
    }

    private static string CompactOperatorText(string operatorText)
    {
        return operatorText.Trim().ToUpperInvariant() switch
        {
            "GREAT NORTHERN" => "GN",
            "GREATER ANGLIA" => "GA",
            "THAMESLINK" => "TL",
            "LNER" => "LNER",
            _ => operatorText.Trim().ToUpperInvariant()
        };
    }

    private static string ExtractCallsText(string tickerText)
    {
        if (string.IsNullOrWhiteSpace(tickerText))
            return string.Empty;

        var callsIndex = tickerText.IndexOf("CALLS ", StringComparison.OrdinalIgnoreCase);
        return callsIndex >= 0 ? tickerText[callsIndex..].ToUpperInvariant() : string.Empty;
    }

    private static bool NeedsCompactFooter(string text, int maxCharactersPerLine, int maxLines)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var wrapped = WrapText(text, maxCharactersPerLine, maxLines + 1);
        return wrapped.Count > maxLines;
    }

    private static void DrawText(Image<Rgba32> img, string text, Font font, Rgba32 color, int x, int y)
    {
        img.Mutate(ctx => ctx.DrawText(CrispDrawingOptions, text, font, color, new PointF(x, y)));
    }

    private static void DrawPixelText(Image<Rgba32> img, string text, Rgba32 color, int x, int y)
    {
        RailDmiText.Draw(img, text, x, y, color);
    }

    private static void DrawPixelRightAlignedText(Image<Rgba32> img, string text, Rgba32 color, int rightX, int y)
    {
        RailDmiText.DrawRightAligned(img, text, rightX, y, color);
    }

    private static void DrawScrollingField(
        Image<Rgba32> img,
        string text,
        Font font,
        Rgba32 color,
        int x,
        int y,
        int width,
        int height,
        TimeSpan elapsedOnPage,
        int lane)
    {
        if (string.IsNullOrWhiteSpace(text) || width <= 0 || height <= 0)
            return;

        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        if (size.Width <= width - 2)
        {
            DrawText(img, text, font, color, x, y);
            return;
        }

        using var field = new Image<Rgba32>(width, height);
        var gap = 18f;
        var cycleWidth = size.Width + gap;
        var speed = 12f;
        var phase = ((float)elapsedOnPage.TotalSeconds * speed + lane * 12f) % cycleWidth;
        var drawX = 2f - phase;

        field.Mutate(ctx =>
        {
            ctx.DrawText(CrispDrawingOptions, text, font, color, new PointF(drawX, -1));
            ctx.DrawText(CrispDrawingOptions, text, font, color, new PointF(drawX + cycleWidth, -1));
        });

        BlitOpaque(img, field, x, y);
    }

    private static void DrawPixelScrollingField(
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

        var size = RailDmiText.MeasureWidth(text);
        if (size <= width)
        {
            DrawPixelText(img, text, color, x, y);
            return;
        }

        using var field = new Image<Rgba32>(width, RailDmiText.Height);
        var gap = 8f;
        var cycleWidth = size + gap;
        var speed = 10f;
        var phase = ((float)elapsedOnPage.TotalSeconds * speed + lane * 10f) % cycleWidth;
        var drawX = (int)MathF.Round(-phase);

        DrawPixelText(field, text, color, drawX, 0);
        DrawPixelText(field, text, color, drawX + (int)MathF.Round(cycleWidth), 0);

        BlitOpaque(img, field, x, y);
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

    private static void Clear(Image<Rgba32> img)
    {
        for (var y = 0; y < img.Height; y++)
        for (var x = 0; x < img.Width; x++)
            img[x, y] = BackgroundColor;
    }

    private static void FillRect(Image<Rgba32> img, int x, int y, int width, int height, Rgba32 color)
    {
        for (var py = y; py < y + height; py++)
        for (var px = x; px < x + width; px++)
            if ((uint)px < img.Width && (uint)py < img.Height)
                img[px, py] = color;
    }

    private static async Task<RailSceneSnapshot> FetchSnapshotAsync(
        RailBoardOptions railOptions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var ukNow = ConvertToUkTime(now);
        var boardTime = ukNow.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

        var requests = new[]
        {
            new RailStationRequest(railOptions.CambridgeCrs, "CAM"),
            new RailStationRequest(railOptions.KingsCrossCrs, "KGX")
        };

        var tasks = requests
            .Select(request => FetchStationSnapshotAsync(railOptions, request, boardTime, cancellationToken))
            .ToArray();

        var stations = await Task.WhenAll(tasks).ConfigureAwait(false);
        var updatedAt = stations
            .Select(static station => station.UpdatedAt)
            .Where(static value => value != DateTimeOffset.MinValue)
            .DefaultIfEmpty(ukNow)
            .Max();

        return new RailSceneSnapshot(stations[0], stations[1], updatedAt);
    }

    private static async Task<RailStationSnapshot> FetchStationSnapshotAsync(
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
            return new RailStationSnapshot(
                request.StationLabel,
                request.StationLabel,
                [],
                [],
                [],
                DateTimeOffset.MinValue,
                true);
        }

        var updatedAt = board.GeneratedAt.HasValue
            ? ConvertToUkTime(board.GeneratedAt.Value)
            : DateTimeOffset.MinValue;

        return new RailStationSnapshot(
            request.StationLabel,
            board.LocationName ?? request.StationLabel,
            BuildServices(board.TrainServices, BoardType.Departures),
            BuildServices(board.TrainServices, BoardType.Arrivals),
            BuildAlerts(board.NrccMessages),
            updatedAt,
            board.ServicesAreUnavailable);
    }

    private static IReadOnlyList<RailServiceSnapshot> BuildServices(ServiceItemDto[]? services, BoardType boardType)
    {
        if (services is null || services.Length == 0)
            return [];

        return services
            .Select(service => BuildServiceSnapshot(service, boardType))
            .Where(static service => service is not null)
            .Cast<RailServiceSnapshot>()
            .OrderBy(static service => service.SortTime)
            .ToArray();
    }

    private static RailServiceSnapshot? BuildServiceSnapshot(ServiceItemDto service, BoardType boardType)
    {
        var location = boardType == BoardType.Departures
            ? PickEndPoint(service.Destination)
            : PickEndPoint(service.Origin);
        if (location is null)
            return null;

        var planned = ParseBoardTimestamp(boardType == BoardType.Departures ? service.Std : service.Sta);
        var estimated = ParseBoardTimestamp(boardType == BoardType.Departures
            ? service.Etd ?? service.Atd
            : service.Eta ?? service.Ata);
        if (!planned.HasValue && !estimated.HasValue)
            return null;

        var platformText = !service.PlatformIsHidden && !string.IsNullOrWhiteSpace(service.Platform)
            ? $"P{service.Platform!.Trim()}"
            : "--";
        var locationText = FormatStationDisplayName(location.Crs, location.LocationName);
        var locationCode = !string.IsNullOrWhiteSpace(location.Crs)
            ? location.Crs!.Trim().ToUpperInvariant()
            : BuildLocationCode(location.LocationName);
        var callingPoints = BuildCallingPoints(boardType == BoardType.Departures
            ? service.SubsequentLocations
            : service.PreviousLocations);

        var (statusText, statusColor) = BuildStatus(service, planned, estimated);
        var detailTicker = BuildDetailTicker(locationText, service.Operator, callingPoints, statusText);

        return new RailServiceSnapshot(
            planned?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "--:--",
            locationText,
            locationCode,
            platformText,
            statusText,
            statusColor,
            service.Operator ?? string.Empty,
            callingPoints,
            detailTicker,
            estimated ?? planned ?? DateTimeOffset.MaxValue);
    }

    private static string BuildDetailTicker(string locationText, string? operatorName, string callingPoints, string statusText)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(locationText))
            parts.Add(locationText.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(operatorName))
            parts.Add(operatorName.ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(callingPoints))
            parts.Add($"CALLS {callingPoints}");
        if (!string.IsNullOrWhiteSpace(statusText))
            parts.Add(statusText);
        return string.Join("  •  ", parts);
    }

    private static IReadOnlyList<RailAlertSnapshot> BuildAlerts(NrccMessageDto[]? messages)
    {
        if (messages is null || messages.Length == 0)
            return [];

        return messages
            .Select(BuildAlertSnapshot)
            .Where(static alert => alert is not null)
            .Cast<RailAlertSnapshot>()
            .DistinctBy(static alert => alert.Message)
            .OrderByDescending(static alert => alert.SeverityWeight)
            .ToArray();
    }

    private static RailAlertSnapshot? BuildAlertSnapshot(NrccMessageDto message)
    {
        var stripped = StripHtml(message.XhtmlMessage);
        if (string.IsNullOrWhiteSpace(stripped))
            return null;

        return new RailAlertSnapshot(
            stripped,
            GetSeverityWeight(message.Severity));
    }

    private static int GetSeverityWeight(string? severity)
    {
        return severity?.Trim().ToUpperInvariant() switch
        {
            "SEVERE" => 3,
            "MAJOR" => 2,
            "MINOR" => 1,
            _ => 0
        };
    }

    private static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decoded = WebUtility.HtmlDecode(value);
        var stripped = HtmlTagRegex.Replace(decoded, " ");
        return Regex.Replace(stripped, "\\s+", " ").Trim();
    }

    private static EndPointLocationDto? PickEndPoint(EndPointLocationDto[]? endpoints)
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

    private static string BuildCallingPoints(ServiceLocationDto[]? locations)
    {
        if (locations is null || locations.Length == 0)
            return string.Empty;

        return string.Join(", ",
            locations
                .Select(static location => FormatStationDisplayName(location.Crs, location.LocationName))
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .Take(8));
    }

    private static (string Text, Rgba32 Color) BuildStatus(
        ServiceItemDto service,
        DateTimeOffset? planned,
        DateTimeOffset? estimated)
    {
        if (service.IsCancelled)
            return ("CANCEL", CancelledColor);

        if (service.ServiceIsSuppressed)
            return ("SEE FRONT", WarningColor);

        if (planned.HasValue && estimated.HasValue)
        {
            var delayMinutes = (int)Math.Round((estimated.Value - planned.Value).TotalMinutes);
            if (delayMinutes > 0)
                return ($"+{Math.Min(delayMinutes, 99):0}", DelayedColor);
        }

        return ("ON TIME", OnTimeColor);
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

    private static string BoardTitle(BoardType boardType)
    {
        return boardType == BoardType.Departures ? "DEPARTURES" : "ARRIVALS";
    }

    private static string CompactStatus(string status)
    {
        return status switch
        {
            "ON TIME" => "ON TIME",
            "CANCEL" => "CANCEL",
            "SEE FRONT" => "SEE FRNT",
            _ when status.Length > 8 => status[..8],
            _ => status
        };
    }

    private static string FormatStationDisplayName(string? crs, string? locationName)
    {
        if (!string.IsNullOrWhiteSpace(crs))
        {
            return crs.Trim().ToUpperInvariant() switch
            {
                "CBG" => "CAMBRIDGE",
                "KGX" => "LONDON KX",
                "LST" => "LIV ST",
                "PBO" => "PETERBORO",
                "SVG" => "STEVENAGE",
                "FPK" => "FINSBURY PK",
                _ => crs.Trim().ToUpperInvariant()
            };
        }

        if (string.IsNullOrWhiteSpace(locationName))
            return "---";

        return locationName.Trim().ToUpperInvariant() switch
        {
            "LONDON KINGS CROSS" => "LONDON KX",
            "LONDON LIVERPOOL STREET" => "LIV ST",
            _ => locationName.Trim().ToUpperInvariant()
        };
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

    private static IReadOnlyList<string> WrapText(string text, int maxCharactersPerLine, int maxLines)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
            if (candidate.Length <= maxCharactersPerLine)
            {
                current = candidate;
                continue;
            }

            if (!string.IsNullOrEmpty(current))
                lines.Add(current);

            current = word;
            if (lines.Count >= maxLines)
                break;
        }

        if (!string.IsNullOrEmpty(current) && lines.Count < maxLines)
            lines.Add(current);

        return lines;
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

    private static RailSceneSnapshot? TryGetCachedSnapshot()
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

    private static void UpdateCache(RailSceneSnapshot railSnapshot)
    {
        lock (CacheLock)
        {
            cachedSnapshot = railSnapshot;
            cacheUpdatedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    public sealed record RailSceneSnapshot(
        RailStationSnapshot Cambridge,
        RailStationSnapshot KingsCross,
        DateTimeOffset UpdatedAt);

    public sealed record RailStationSnapshot(
        string HeaderLabel,
        string StationName,
        IReadOnlyList<RailServiceSnapshot> Departures,
        IReadOnlyList<RailServiceSnapshot> Arrivals,
        IReadOnlyList<RailAlertSnapshot> Alerts,
        DateTimeOffset UpdatedAt,
        bool IsUnavailable)
    {
        public string StationCode => HeaderLabel;
    }

    public sealed record RailServiceSnapshot(
        string ScheduledText,
        string LocationText,
        string LocationCode,
        string PlatformText,
        string StatusText,
        Rgba32 StatusColor,
        string OperatorText,
        string CallingText,
        string DetailTicker,
        DateTimeOffset SortTime);

    public sealed record RailAlertSnapshot(string Message, int SeverityWeight);

    private abstract record RailPage(TimeSpan Duration);

    private sealed record BoardPage(
        string Header,
        string StationName,
        BoardType BoardType,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<RailServiceSnapshot> Rows,
        string TickerText,
        TimeSpan Duration)
        : RailPage(Duration);

    private sealed record ServiceDetailPage(
        string Header,
        string TimeText,
        string LocationText,
        string PlatformText,
        string StatusText,
        Rgba32 StatusColor,
        string OperatorText,
        string TickerText,
        TimeSpan Duration)
        : RailPage(Duration);

    private sealed record JourneyPage(
        string Header,
        string Subtitle,
        string TimeText,
        string PlatformText,
        string LocationText,
        string StatusText,
        Rgba32 StatusColor,
        string TickerText,
        TimeSpan Duration)
        : RailPage(Duration);

    private sealed record AlertPage(
        string Header,
        string StationText,
        IReadOnlyList<string> Lines,
        string TickerText,
        TimeSpan Duration)
        : RailPage(Duration);

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

    private enum BoardType
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
    ServiceItemDto[]? TrainServices,
    NrccMessageDto[]? NrccMessages);

public sealed record ServiceItemDto(
    EndPointLocationDto[]? Origin,
    EndPointLocationDto[]? Destination,
    ServiceLocationDto[]? PreviousLocations,
    ServiceLocationDto[]? SubsequentLocations,
    string? Sta,
    string? Ata,
    string? Eta,
    string? Std,
    string? Atd,
    string? Etd,
    string? Platform,
    string? Operator,
    bool PlatformIsHidden,
    [property: JsonPropertyName("serviceIsSupressed")] bool ServiceIsSuppressed,
    bool IsCancelled);

public sealed record ServiceLocationDto(
    string? LocationName,
    string? Crs);

public sealed record EndPointLocationDto(
    string? LocationName,
    string? Crs,
    string? Via);

public sealed record NrccMessageDto(
    string? Category,
    string? Severity,
    [property: JsonPropertyName("xhtmlMessage")] string? XhtmlMessage);
