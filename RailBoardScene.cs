using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using advent.Data.Rail;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public sealed class RailBoardScene : ISpecialScene, IDeferredActivationScene
{
    public static readonly TimeSpan MaxSceneDuration = TimeSpan.FromSeconds(30);

    private const int WideMinWidth = 100;
    private const int WideMinHeight = 48;
    private const int WideBoardRows = 3;
    private const int CompactBoardRows = 3;

    private static readonly TimeSpan BoardPageDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DetailPageDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan JourneyPageDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan AlertPageDuration = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PageSwipeDuration = TimeSpan.FromMilliseconds(700);

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
    private static readonly Rgba32 FastServiceColor = new(128, 255, 160);
    private static readonly Rgba32 AlertColor = new(255, 84, 72);

    private readonly IRailSnapshotSource snapshotSource;

    private RailSceneSnapshot? snapshot;
    private IReadOnlyList<RailPage> pages = [];
    private int currentPageIndex;
    private Image<Rgba32>? currentPageBuffer;
    private TimeSpan elapsedOnPage;
    private Image<Rgba32>? nextPageBuffer;
    private bool shouldSkipActivation;

    public RailBoardScene()
        : this(EmptyRailSnapshotSource.Instance)
    {
    }

    internal RailBoardScene(IRailSnapshotSource snapshotSource)
    {
        this.snapshotSource = snapshotSource ?? throw new ArgumentNullException(nameof(snapshotSource));
    }

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "UK Rail Board";
    public bool IsReadyToActivate => pages.Count > 0;
    public bool ShouldSkipActivation => shouldSkipActivation;

    public static bool IsConfiguredFromEnvironment()
    {
        return RailBoardOptions.TryFromEnvironment() is not null;
    }

    public void Prepare()
    {
        shouldSkipActivation = !snapshotSource.TryGetSnapshot(out var railSnapshot);
        snapshot = railSnapshot;
        pages = snapshot is not null ? BuildPages(snapshot) : [];
    }

    public void AdvancePreparation(TimeSpan timeSpan)
    {
        if (pages.Count > 0)
            return;

        shouldSkipActivation = !snapshotSource.TryGetSnapshot(out var railSnapshot);
        snapshot = railSnapshot;
        pages = snapshot is not null ? BuildPages(snapshot) : [];
    }

    public void Activate()
    {
        if (pages.Count == 0)
            Prepare();

        if (pages.Count == 0)
        {
            IsActive = false;
            HidesTime = false;
            return;
        }

        elapsedOnPage = TimeSpan.Zero;
        currentPageIndex = 0;
        IsActive = true;
        HidesTime = true;
        shouldSkipActivation = false;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

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
        if (!IsActive || pages.Count == 0)
            return;

        Clear(img);

        var page = pages[Math.Clamp(currentPageIndex, 0, pages.Count - 1)];
        if (ShouldSwipeToNextPage(page))
        {
            EnsurePageBuffers(img.Width, img.Height);

            RenderPage(currentPageBuffer!, page, elapsedOnPage);
            RenderPage(nextPageBuffer!, pages[currentPageIndex + 1], TimeSpan.Zero);

            var transitionStart = page.Duration - PageSwipeDuration;
            var progress = (float)((elapsedOnPage - transitionStart).TotalMilliseconds /
                                   PageSwipeDuration.TotalMilliseconds);
            progress = Math.Clamp(progress, 0f, 1f);

            var currentOffset = -(int)MathF.Round(progress * img.Width);
            var nextOffset = img.Width + currentOffset;
            BlitPage(img, currentPageBuffer!, currentOffset);
            BlitPage(img, nextPageBuffer!, nextOffset);
            return;
        }

        RenderPage(img, page, elapsedOnPage);
    }

    private static bool IsWide(Image<Rgba32> img)
    {
        return img.Width >= WideMinWidth && img.Height >= WideMinHeight;
    }

    private static bool ShouldSwipeToNextPage(RailPage page, int currentPageIndex, int pageCount, TimeSpan elapsedOnPage)
    {
        if (currentPageIndex >= pageCount - 1 || page.Duration <= PageSwipeDuration)
            return false;

        return elapsedOnPage >= page.Duration - PageSwipeDuration;
    }

    private bool ShouldSwipeToNextPage(RailPage page)
    {
        return ShouldSwipeToNextPage(page, currentPageIndex, pages.Count, elapsedOnPage);
    }

    private static void RenderPage(Image<Rgba32> img, RailPage page, TimeSpan elapsedOnPage)
    {
        Clear(img);

        if (IsWide(img))
            DrawWidePage(img, page, elapsedOnPage);
        else
            DrawCompactPage(img, page, elapsedOnPage);
    }

    private void EnsurePageBuffers(int width, int height)
    {
        if (currentPageBuffer is null || currentPageBuffer.Width != width || currentPageBuffer.Height != height)
        {
            currentPageBuffer?.Dispose();
            currentPageBuffer = new Image<Rgba32>(width, height);
        }

        if (nextPageBuffer is null || nextPageBuffer.Width != width || nextPageBuffer.Height != height)
        {
            nextPageBuffer?.Dispose();
            nextPageBuffer = new Image<Rgba32>(width, height);
        }
    }

    private static void BlitPage(Image<Rgba32> destination, Image<Rgba32> source, int offsetX)
    {
        for (var y = 0; y < destination.Height; y++)
        for (var x = 0; x < source.Width; x++)
        {
            var destX = x + offsetX;
            if ((uint)destX >= (uint)destination.Width)
                continue;

            destination[destX, y] = source[x, y];
        }
    }

    private static IReadOnlyList<RailPage> BuildPages(RailSceneSnapshot railSnapshot)
    {
        return
        [
            BuildBoardPage(railSnapshot.Cambridge, BoardType.Departures, railSnapshot.KingsCross, railSnapshot.UpdatedAt),
            BuildBoardPage(railSnapshot.KingsCross, BoardType.Departures, railSnapshot.Cambridge, railSnapshot.UpdatedAt)
        ];
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
            if (candidate.Service.StatusText is not "On time")
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
        RailStationSnapshot counterpart,
        DateTimeOffset updatedAt)
    {
        var services = boardType == BoardType.Departures ? station.Departures : station.Arrivals;
        var boardRows = BuildBoardRows(station, boardType, services);
        var fastIndices = ClassifyFastServices(boardRows);
        var tickerText = BuildBoardTicker(station, boardType, boardRows);
        return new BoardPage(
            $"{FullStationLabel(station.StationName)} {BoardTitle(boardType)}",
            station.StationName,
            boardType,
            updatedAt,
            boardRows,
            fastIndices,
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
            ? $"{FullStationLabel(station.StationName)} board data unavailable"
            : $"{FullStationLabel(station.StationName)} has no services in the current window";

        return
        [
            new RailServiceSnapshot(
                "--:--",
                station.IsUnavailable ? "No live data" : "No services",
                boardType == BoardType.Departures ? "DEP" : "ARR",
                "--",
                station.IsUnavailable ? "No data" : "Check",
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
            : BuildServicePatternText(service.LocationText, service.LocationCode, service.CallingText);
        return new JourneyPage(
            $"{FullStationLabel(station.StationName)} trip",
            "Next service",
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
            "Rail alert",
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
            return $"{FullStationLabel(station.StationName)} board data unavailable";

        if (services.Count == 0)
        {
            return station.Alerts.Count > 0
                ? station.Alerts[0].Message
                : $"{FullStationLabel(station.StationName)} {BoardTitle(boardType).ToLowerInvariant()}";
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
        DrawCompactBoardHeader(img, FullStationLabel(page.StationName), page.BoardType, elapsedOnPage);
        var timeColumnWidth = RailDmiText.MeasureWidth("00:00") + 2;
        var dotWidth = 3; // status dot + gap
        var rows = page.Rows.Take(CompactBoardRows).ToArray();

        // Layout: header 0-5, divider 6, row0 8-12, row1 15-19, row2 22-26, footer 28-31
        var rowTopPositions = new[] { 8, 15, 22 };

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var y = rowTopPositions[i];
            var platformText = CompactPlatform(row.PlatformText);
            var platformReserve = string.IsNullOrWhiteSpace(platformText) ? 0 : RailDmiText.MeasureWidth(platformText) + 1;
            var locationWidth = Math.Max(0, img.Width - timeColumnWidth - dotWidth - platformReserve - 1);

            // Highlight first row (typically most relevant departure)
            var isHighlight = i == 0;
            var timeColor = isHighlight ? HeaderColor : SecondaryTextColor;
            var isFast = page.FastRowIndices.Contains(i);
            var destColor = isFast ? FastServiceColor : isHighlight ? PrimaryTextColor : SecondaryTextColor;

            // Build scrolling text: destination + calling pattern
            var callingText = CompactBoardCallingText(row, isFast);
            var scrollText = string.IsNullOrWhiteSpace(callingText)
                ? row.LocationText
                : $"{row.LocationText}  \u00b7  {callingText}";

            // Status dot: colored circle left of the time
            DrawStatusDot(img, row.StatusColor, timeColumnWidth + 1, y);

            DrawPixelText(img, row.ScheduledText, timeColor, 0, y);
            DrawPixelScrollingField(
                img,
                scrollText,
                destColor,
                timeColumnWidth + dotWidth,
                y,
                locationWidth,
                elapsedOnPage,
                i);
            DrawPixelRightAlignedText(img, platformText, destColor, img.Width - 1, y);

            if (i < rows.Length - 1)
                FillRect(img, 0, y + 6, img.Width, 1, DividerColor);
        }
    }

    private static void DrawStatusDot(Image<Rgba32> img, Rgba32 color, int x, int y)
    {
        // 1x3 vertical pip centered on the text row (5px tall text, dot at rows 1-3)
        var dotY = y + 1;
        if ((uint)x < img.Width && (uint)dotY < img.Height) img[x, dotY] = color;
        if ((uint)x < img.Width && (uint)(dotY + 1) < img.Height) img[x, dotY + 1] = color;
        if ((uint)x < img.Width && (uint)(dotY + 2) < img.Height) img[x, dotY + 2] = color;
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

    private static void DrawCompactBoardHeader(
        Image<Rgba32> img,
        string stationName,
        BoardType boardType,
        TimeSpan elapsedOnPage)
    {
        FillRect(img, 0, 5, img.Width, 1, DividerColor);

        var rightText = boardType == BoardType.Departures ? "DEP" : "ARR";
        DrawPixelRightAlignedText(img, rightText, SecondaryTextColor, img.Width - 1, 0);

        var rightWidth = RailDmiText.MeasureWidth(rightText);
        var leftWidth = Math.Max(0, img.Width - rightWidth - 1);
        DrawPixelScrollingField(img, stationName, HeaderColor, 0, 0, leftWidth, elapsedOnPage, 0);
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
        => RailStationNames.CompactLabel(stationName);

    private static string FullStationLabel(string stationName)
        => RailStationNames.FullLabel(stationName);

    private static string CompactPlatform(string platformText)
    {
        if (string.IsNullOrWhiteSpace(platformText) || platformText == "--")
            return string.Empty;

        return platformText.Trim().ToUpperInvariant();
    }

    private static string CompactBoardIndicator(string status, int maxWidth)
    {
        var compact = status switch
        {
            "On time" => "On tm",
            "See front" => "See",
            "Cancelled" => "Can",
            _ when status.Length > 8 => status[..8],
            _ => status
        };

        return RailDmiText.TrimToWidth(compact, maxWidth);
    }

    private static string CompactBoardCallingText(RailServiceSnapshot service, bool isFast = false)
    {
        return BuildServicePatternText(service.LocationText, service.LocationCode, service.CallingText, isFast);
    }

    private static string CompactOperatorText(string operatorText)
    {
        return operatorText.Trim();
    }

    private static string ExtractCallsText(string tickerText)
    {
        if (string.IsNullOrWhiteSpace(tickerText))
            return string.Empty;

        var fastViaIndex = tickerText.IndexOf("Fast via ", StringComparison.OrdinalIgnoreCase);
        if (fastViaIndex >= 0)
            return tickerText[fastViaIndex..];

        var viaIndex = tickerText.IndexOf("Via ", StringComparison.OrdinalIgnoreCase);
        if (viaIndex >= 0)
            return tickerText[viaIndex..];

        var callsIndex = tickerText.IndexOf("Calls ", StringComparison.OrdinalIgnoreCase);
        return callsIndex >= 0 ? tickerText[callsIndex..] : string.Empty;
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

    private static string BuildServicePatternText(string locationText, string locationCode, string callingPoints, bool isFast = false)
    {
        var normalizedCallingPoints = NormalizeCallingPoints(callingPoints);
        if (normalizedCallingPoints.Count == 0)
            return string.Empty;

        var prefix = isFast ? "Fast via" : "Via";

        return $"{prefix} {string.Join(", ", normalizedCallingPoints)}";
    }

    private static IReadOnlyList<string> NormalizeCallingPoints(string callingPoints)
    {
        if (string.IsNullOrWhiteSpace(callingPoints))
            return [];

        var trimmed = callingPoints.Trim();
        if (trimmed.StartsWith("Calls ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[6..].Trim();
        if (trimmed.StartsWith("Via ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[4..].Trim();
        if (trimmed.StartsWith("Fast via ", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[9..].Trim();

        return trimmed
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(FullStationLabel)
            .Where(static point => !string.IsNullOrWhiteSpace(point))
            .ToArray();
    }

    /// <summary>
    /// Analyzes all services on a board page and classifies which are "fast" relative to the others.
    /// If there's a mix of stop counts, services with fewer stops than the median are fast.
    /// If all services have the same number of stops, none are highlighted (no contrast to show).
    /// </summary>
    private static IReadOnlySet<int> ClassifyFastServices(IReadOnlyList<RailServiceSnapshot> rows)
    {
        var result = new HashSet<int>();

        // Compute stop counts for each row
        var stopCounts = new int[rows.Count];
        var hasCallingPoints = false;
        for (var i = 0; i < rows.Count; i++)
        {
            var points = NormalizeCallingPoints(rows[i].CallingText);
            stopCounts[i] = points.Count;
            if (points.Count > 0) hasCallingPoints = true;
        }

        if (!hasCallingPoints)
            return result;

        // Find the range of stop counts (only for services that have calling points)
        var countsWithPoints = stopCounts.Where(c => c > 0).ToArray();
        if (countsWithPoints.Length < 2)
        {
            // Only one service has calling points — can't determine relative speed
            // Fall back: mark it as fast if it has very few stops
            for (var i = 0; i < rows.Count; i++)
                if (stopCounts[i] > 0 && stopCounts[i] <= 3)
                    result.Add(i);
            return result;
        }

        var min = countsWithPoints.Min();
        var max = countsWithPoints.Max();

        if (min == max)
        {
            // All services have the same number of stops — no contrast to show
            return result;
        }

        // Find the natural threshold: the largest gap between sorted unique stop counts
        var uniqueSorted = countsWithPoints.Distinct().OrderBy(c => c).ToArray();
        var bestGapIndex = 0;
        var bestGap = 0;
        for (var i = 0; i < uniqueSorted.Length - 1; i++)
        {
            var gap = uniqueSorted[i + 1] - uniqueSorted[i];
            if (gap > bestGap)
            {
                bestGap = gap;
                bestGapIndex = i;
            }
        }

        // The threshold is the value at the low side of the largest gap
        var threshold = uniqueSorted[bestGapIndex];

        // Mark services at or below the threshold as fast
        for (var i = 0; i < rows.Count; i++)
            if (stopCounts[i] > 0 && stopCounts[i] <= threshold)
                result.Add(i);

        return result;
    }

    private static bool IsFastService(RailServiceSnapshot service)
    {
        var callingPoints = NormalizeCallingPoints(service.CallingText);
        return callingPoints.Count > 0 && callingPoints.Count <= 3;
    }

    private static string BoardTitle(BoardType boardType)
    {
        return boardType == BoardType.Departures ? "Departures" : "Arrivals";
    }

    private static string CompactStatus(string status)
    {
        return status switch
        {
            "On time" => "On time",
            "Cancelled" => "Cancel",
            "See front" => "See frnt",
            _ when status.Length > 8 => status[..8],
            _ => status
        };
    }

    private static string NormalizeLocationKey(string locationName)
        => RailStationNames.NormalizeLocationKey(locationName);

    private static string FormatLocationDisplayName(string locationName)
        => RailStationNames.HumanizedLabel(locationName);

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

    private abstract record RailPage(TimeSpan Duration);

    private sealed record BoardPage(
        string Header,
        string StationName,
        BoardType BoardType,
        DateTimeOffset UpdatedAt,
        IReadOnlyList<RailServiceSnapshot> Rows,
        IReadOnlySet<int> FastRowIndices,
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

    private sealed class EmptyRailSnapshotSource : IRailSnapshotSource
    {
        public static readonly EmptyRailSnapshotSource Instance = new();

        public bool TryGetSnapshot(out RailSceneSnapshot snapshot)
        {
            snapshot = default!;
            return false;
        }
    }

    private enum BoardType
    {
        Departures,
        Arrivals
    }
}
