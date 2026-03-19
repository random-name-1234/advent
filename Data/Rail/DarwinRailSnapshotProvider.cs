using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using SixLabors.ImageSharp.PixelFormats;

namespace advent.Data.Rail;

internal static partial class DarwinRailSnapshotProvider
{
    private static readonly Regex HtmlTagRegex = HtmlRegex();

    private static readonly Rgba32 OnTimeColor = new(230, 192, 112);
    private static readonly Rgba32 DelayedColor = new(255, 152, 40);
    private static readonly Rgba32 CancelledColor = new(255, 96, 84);
    private static readonly Rgba32 WarningColor = new(255, 214, 128);
    private static readonly Rgba32 AlertColor = new(255, 84, 72);

    public static async Task<RailSceneSnapshot> FetchSnapshotAsync(
        RailBoardOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var ukNow = ConvertToUkTime(now);
        var boardTime = ukNow.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

        var forwardRequest = new RailDirectionRequest(
            options.OriginCrs,
            options.OriginLabel,
            options.DestinationCrs,
            options.DestinationLabel);
        var reverseRequest = new RailDirectionRequest(
            options.DestinationCrs,
            options.DestinationLabel,
            options.OriginCrs,
            options.OriginLabel);

        var boards = await Task.WhenAll(
                DarwinDepartureBoardClient.FetchDirectionalDepartureBoardAsync(
                    options,
                    forwardRequest.StationCrs,
                    forwardRequest.StationLabel,
                    forwardRequest.CounterpartCrs,
                    boardTime,
                    cancellationToken),
                DarwinDepartureBoardClient.FetchDirectionalDepartureBoardAsync(
                    options,
                    reverseRequest.StationCrs,
                    reverseRequest.StationLabel,
                    reverseRequest.CounterpartCrs,
                    boardTime,
                    cancellationToken))
            .ConfigureAwait(false);

        var updatedAt = boards
            .Select(static board => board.GeneratedAt is { } value ? ConvertToUkTime(value) : DateTimeOffset.MinValue)
            .Where(static value => value != DateTimeOffset.MinValue)
            .DefaultIfEmpty(ukNow)
            .Max();

        return new RailSceneSnapshot(
            BuildCorridorStationSnapshot(boards[0], boards[1], forwardRequest),
            BuildCorridorStationSnapshot(boards[1], boards[0], reverseRequest),
            updatedAt);
    }

    private static RailStationSnapshot BuildCorridorStationSnapshot(
        StationBoardDto departuresBoard,
        StationBoardDto oppositeDirectionBoard,
        RailDirectionRequest request)
    {
        var updatedAt = departuresBoard.GeneratedAt is { } generatedAt
            ? ConvertToUkTime(generatedAt)
            : DateTimeOffset.MinValue;

        return new RailStationSnapshot(
            request.StationLabel,
            departuresBoard.LocationName ?? request.StationLabel,
            BuildDepartureServices(departuresBoard.TrainServices),
            BuildArrivalsFromOppositeDirection(oppositeDirectionBoard.TrainServices, request),
            BuildAlerts(departuresBoard.NrccMessages),
            updatedAt,
            departuresBoard.ServicesAreUnavailable);
    }

    private static IReadOnlyList<RailServiceSnapshot> BuildDepartureServices(ServiceItemDto[]? services)
        => services is null or { Length: 0 }
            ? []
            : [.. services
                .Select(BuildDepartureSnapshot)
                .Where(static service => service is not null)
                .Cast<RailServiceSnapshot>()
                .OrderBy(static service => service.SortTime)];

    private static IReadOnlyList<RailServiceSnapshot> BuildArrivalsFromOppositeDirection(
        ServiceItemDto[]? services,
        RailDirectionRequest request)
        => services is null or { Length: 0 }
            ? []
            : [.. services
                .Select(service => BuildArrivalSnapshotFromOppositeDirection(service, request))
                .Where(static service => service is not null)
                .Cast<RailServiceSnapshot>()
                .OrderBy(static service => service.SortTime)];

    private static RailServiceSnapshot? BuildDepartureSnapshot(ServiceItemDto service)
    {
        var location = PickEndPoint(service.Destination);
        if (location is null)
            return null;

        var planned = ParseBoardTimestamp(service.Std);
        var estimated = ParseBoardTimestamp(service.Etd ?? service.Atd);
        if (!planned.HasValue && !estimated.HasValue)
            return null;

        var platformText = !service.PlatformIsHidden && !string.IsNullOrWhiteSpace(service.Platform)
            ? $"P{service.Platform!.Trim()}"
            : "--";
        var locationText = RailStationNames.DisplayName(location.Crs, location.LocationName);
        var locationCode = !string.IsNullOrWhiteSpace(location.Crs)
            ? location.Crs!.Trim().ToUpperInvariant()
            : RailStationNames.BuildLocationCode(location.LocationName);
        var callingPoints = BuildCallingPoints(service.SubsequentLocations);
        var (statusText, statusColor) = BuildStatus(service, planned, estimated);
        var detailTicker = BuildDetailTicker(locationText, locationCode, service.Operator, callingPoints, statusText);

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

    private static RailServiceSnapshot? BuildArrivalSnapshotFromOppositeDirection(
        ServiceItemDto service,
        RailDirectionRequest request)
    {
        var arrivalLocation = FindArrivalLocation(service.SubsequentLocations, request.StationCrs);
        if (arrivalLocation is null)
            return null;

        var planned = ParseBoardTimestamp(arrivalLocation.Sta ?? arrivalLocation.Std);
        var estimated = ParseBoardTimestamp(arrivalLocation.Eta ?? arrivalLocation.Ata ?? arrivalLocation.Etd ??
                                            arrivalLocation.Atd ?? arrivalLocation.Sta ?? arrivalLocation.Std);
        if (!planned.HasValue && !estimated.HasValue)
            return null;

        var platformText = !arrivalLocation.PlatformIsHidden && !string.IsNullOrWhiteSpace(arrivalLocation.Platform)
            ? $"P{arrivalLocation.Platform!.Trim()}"
            : "--";
        var locationText = RailStationNames.DisplayName(request.CounterpartCrs, request.CounterpartLabel);
        var locationCode = request.CounterpartCrs.ToUpperInvariant();
        var priorCallingPoints = TakeLocationsBefore(service.SubsequentLocations, arrivalLocation);
        var callingPoints = BuildCallingPoints(priorCallingPoints, takeFromEnd: true);
        var (statusText, statusColor) = BuildStatus(service.IsCancelled, service.ServiceIsSuppressed, planned, estimated);
        var detailTicker = BuildDetailTicker(locationText, locationCode, service.Operator, callingPoints, statusText);

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

    private static string BuildDetailTicker(
        string locationText,
        string locationCode,
        string? operatorName,
        string callingPoints,
        string statusText)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(locationText))
            parts.Add(locationText);
        if (!string.IsNullOrWhiteSpace(operatorName))
            parts.Add(operatorName);

        var servicePattern = BuildServicePatternText(locationText, locationCode, callingPoints);
        if (!string.IsNullOrWhiteSpace(servicePattern))
            parts.Add(servicePattern);
        if (!string.IsNullOrWhiteSpace(statusText))
            parts.Add(statusText);

        return string.Join("  •  ", parts);
    }

    private static string BuildServicePatternText(string locationText, string locationCode, string callingPoints)
    {
        var normalizedCallingPoints = NormalizeCallingPoints(callingPoints);
        if (normalizedCallingPoints.Count is 0)
            return string.Empty;

        var prefix = IsFastService(locationText, locationCode, normalizedCallingPoints)
            ? "Fast via"
            : "Via";

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

        return [.. trimmed
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(RailStationNames.FullLabel)
            .Where(static point => !string.IsNullOrWhiteSpace(point))];
    }

    private static bool IsFastService(
        string locationText,
        string locationCode,
        IReadOnlyList<string> normalizedCallingPoints)
    {
        if (normalizedCallingPoints.Count is 0)
            return false;

        var locationKey = RailStationNames.NormalizeLocationKey(
            !string.IsNullOrWhiteSpace(locationCode) ? locationCode : locationText);
        if (locationKey is not ("CAM" or "CBG" or "KGX" or "CAMBRIDGE" or "KINGS CROSS" or "LONDON KINGS CROSS"))
            return false;

        var coreStops = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ROYSTON",
            "STEVENAGE",
            "FINSBURY PARK"
        };

        var calledCoreStops = normalizedCallingPoints
            .Select(RailStationNames.NormalizeLocationKey)
            .Count(coreStops.Contains);

        return normalizedCallingPoints.Count <= 2 || calledCoreStops < coreStops.Count;
    }

    private static IReadOnlyList<RailAlertSnapshot> BuildAlerts(NrccMessageDto[]? messages)
        => messages is null or { Length: 0 }
            ? []
            : [.. messages
                .Select(BuildAlertSnapshot)
                .Where(static alert => alert is not null)
                .Cast<RailAlertSnapshot>()
                .DistinctBy(static alert => alert.Message)
                .OrderByDescending(static alert => alert.SeverityWeight)];

    private static RailAlertSnapshot? BuildAlertSnapshot(NrccMessageDto message)
    {
        var stripped = StripHtml(message.XhtmlMessage);
        return string.IsNullOrWhiteSpace(stripped)
            ? null
            : new RailAlertSnapshot(stripped, GetSeverityWeight(message.Severity));
    }

    private static int GetSeverityWeight(string? severity) => severity?.Trim().ToUpperInvariant() switch
    {
        "SEVERE" => 3,
        "MAJOR" => 2,
        "MINOR" => 1,
        _ => 0
    };

    private static string StripHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var decoded = WebUtility.HtmlDecode(value);
        var stripped = HtmlTagRegex.Replace(decoded, " ");
        return WhitespaceRegex().Replace(stripped, " ").Trim();
    }

    private static EndPointLocationDto? PickEndPoint(EndPointLocationDto[]? endpoints)
    {
        if (endpoints is null or { Length: 0 })
            return null;

        foreach (var endpoint in endpoints)
        {
            if (endpoint is not null && !string.IsNullOrWhiteSpace(endpoint.Crs))
                return endpoint;
        }

        return endpoints.FirstOrDefault(static endpoint => endpoint is not null);
    }

    private static string BuildCallingPoints(IEnumerable<ServiceLocationDto>? locations, bool takeFromEnd = false)
    {
        if (locations is null)
            return string.Empty;

        var displayPoints = locations
            .Where(static location => !location.IsPass)
            .Select(static location => RailStationNames.DisplayName(location.Crs, location.LocationName))
            .Where(static text => !string.IsNullOrWhiteSpace(text) && !LooksLikeTimingPointCode(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (displayPoints.Count is 0)
            return string.Empty;

        var selectedPoints = takeFromEnd ? displayPoints.TakeLast(8) : displayPoints.Take(8);
        return string.Join(", ", selectedPoints);
    }

    private static (string Text, Rgba32 Color) BuildStatus(
        ServiceItemDto service,
        DateTimeOffset? planned,
        DateTimeOffset? estimated)
        => BuildStatus(service.IsCancelled, service.ServiceIsSuppressed, planned, estimated);

    private static (string Text, Rgba32 Color) BuildStatus(
        bool isCancelled,
        bool isSuppressed,
        DateTimeOffset? planned,
        DateTimeOffset? estimated)
    {
        if (isCancelled)
            return ("Cancelled", CancelledColor);

        if (isSuppressed)
            return ("See front", WarningColor);

        if (planned.HasValue && estimated.HasValue)
        {
            var delayMinutes = (int)Math.Round((estimated.Value - planned.Value).TotalMinutes);
            if (delayMinutes > 0)
                return ($"+{Math.Min(delayMinutes, 99):0}", DelayedColor);
        }

        return ("On time", OnTimeColor);
    }

    private static DateTimeOffset? ParseBoardTimestamp(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
                ? parsed
                : null;

    private static ServiceLocationDto? FindArrivalLocation(ServiceLocationDto[]? locations, string arrivalStationCrs)
    {
        if (locations is null or { Length: 0 })
            return null;

        foreach (var location in locations)
        {
            if (string.Equals(location.Crs, arrivalStationCrs, StringComparison.OrdinalIgnoreCase))
                return location;
        }

        return null;
    }

    private static IEnumerable<ServiceLocationDto> TakeLocationsBefore(
        ServiceLocationDto[]? locations,
        ServiceLocationDto targetLocation)
    {
        if (locations is null or { Length: 0 })
            yield break;

        foreach (var location in locations)
        {
            if (ReferenceEquals(location, targetLocation))
                yield break;

            yield return location;
        }
    }

    private static bool LooksLikeTimingPointCode(string locationName)
        => !string.IsNullOrWhiteSpace(locationName) &&
           locationName.Length >= 5 &&
           locationName.All(static ch => char.IsUpper(ch) || char.IsDigit(ch));

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

    private sealed record RailDirectionRequest(
        string StationCrs,
        string StationLabel,
        string CounterpartCrs,
        string CounterpartLabel);

    [GeneratedRegex("<.*?>", RegexOptions.Compiled)]
    private static partial Regex HtmlRegex();

    [GeneratedRegex("\\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
