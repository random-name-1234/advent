using System.Globalization;
using System.Text;
using advent.Data.Rail;

namespace advent;

internal abstract record RailCard(TimeSpan Duration);
internal sealed record RailOverviewCard(string Station, string[] Lines)
    : RailCard(RailBoardScene.OverviewDuration);
internal sealed record RailDepartureCard(string Station, int Number, string Time, string Destination,
    string Platform, string Status) : RailCard(RailBoardScene.DepartureDuration)
{
    public bool IsFast { get; init; }
}
internal sealed record RailNoticeCard(string Station, IReadOnlyList<string[]> Pages)
    : RailCard(RailBoardScene.NoticeDuration);

internal static class RailCards
{
    internal const int TextWidth = 60;

    internal static IReadOnlyList<RailServiceSnapshot> Upcoming(RailStationSnapshot station, DateTimeOffset now) =>
        station.IsUnavailable ? [] : station.Departures
            .Where(service => !service.HasDeparted && service.SortTime >= now)
            .OrderBy(service => service.SortTime)
            .DistinctBy(ServiceKey)
            .ToArray();

    internal static string ServiceKey(RailServiceSnapshot service) =>
        $"{service.ScheduledAt?.ToString("O", CultureInfo.InvariantCulture) ?? service.ScheduledText}|{service.LocationCode}|{service.OperatorText}";

    internal static RailOverviewCard StationStatus(RailStationSnapshot station) => new(
        StationLabel(station.StationName, station.Crs ?? station.HeaderLabel),
        station.IsUnavailable ? ["LIVE DATA", "UNAVAILABLE", "CHECK ONLINE"] :
        ["NO MORE", "TRAINS LISTED", "CHECK ONLINE"]);

    internal static RailDepartureCard Departure(RailStationSnapshot station, RailServiceSnapshot service, int number) =>
        new(StationCode(station), number, service.ScheduledText,
            StationLabel(service.LocationText, service.LocationCode),
            Platform(service.PlatformText), Status(service.StatusText)) { IsFast = service.IsFastToCounterpart };

    internal static RailNoticeCard Notice(RailStationSnapshot station)
    {
        var alerts = station.Alerts.OrderByDescending(alert => alert.SeverityWeight).ToArray();
        var lines = Wrap(alerts.FirstOrDefault()?.Message ?? "Check National Rail for travel information.");
        var pages = lines.Chunk(3).Select(chunk => chunk.ToArray()).ToList();
        if (pages.Count > 4 || alerts.Length > 1)
        {
            pages = pages.Take(3).ToList();
            pages.Add(["MORE ONLINE", "NATIONAL RAIL", "TRAVEL INFO"]);
        }
        return new RailNoticeCard(StationCode(station), pages);
    }

    internal static string StationLabel(string name, string code)
    {
        var normalized = Normalize(name).Replace("'", "");
        var label = normalized switch
        {
            "LONDON KINGS CROSS" or "KINGS CROSS" => "KINGS CROSS",
            "LONDON LIVERPOOL STREET" or "LIVERPOOL STREET" => "LIVERPOOL ST",
            "PETERBOROUGH" => "PETERBORO",
            "BISHOPS STORTFORD" => "BISHOPS STFD",
            _ => normalized
        };
        if (RailDmiText.MeasureWidth(label) <= TextWidth && label.Length > 0)
            return label;

        var crs = Normalize(code);
        return crs.Length == 3 && crs.All(char.IsAsciiLetter) ? crs : Abbreviate(label, TextWidth);
    }

    internal static string Status(string status)
    {
        var normalized = Normalize(status);
        if (normalized.StartsWith('+') && int.TryParse(normalized.AsSpan(1), out var minutes) && minutes > 0)
        {
            var delay = $"+{minutes} MIN";
            return RailDmiText.MeasureWidth(delay) <= TextWidth ? delay : "LONG DELAY";
        }
        return normalized switch
        {
            "ON TIME" => "ON TIME",
            "CANCELLED" or "CANCELED" => "CANCELLED",
            "SEE FRONT" or "DELAYED" => "CHECK STATUS",
            _ => "CHECK STATUS"
        };
    }

    internal static string Platform(string platform)
    {
        var label = Normalize(platform);
        if (label.StartsWith('P'))
            label = label[1..];
        // Never truncate a platform into a different, apparently valid number.
        return label.Length > 0 && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') &&
            RailDmiText.MeasureWidth(label) <= 19 ? label : "--";
    }

    internal static IReadOnlyList<string> Wrap(string text)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var remaining = word;
            if (line.Length > 0 && RailDmiText.MeasureWidth($"{line} {word}") <= TextWidth)
            {
                line += $" {word}";
                continue;
            }
            if (line.Length > 0)
                lines.Add(line);
            while (RailDmiText.MeasureWidth(remaining) > TextWidth)
            {
                var part = RailDmiText.TrimToWidth(remaining, TextWidth);
                lines.Add(part);
                remaining = remaining[part.Length..];
            }
            line = remaining;
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines.Count > 0 ? lines : ["CHECK ONLINE"];
    }

    private static string StationCode(RailStationSnapshot station)
    {
        var code = Normalize(station.Crs ?? station.HeaderLabel);
        if (code == "CAM") return "CBG";
        if (code.Length == 3 && code.All(char.IsAsciiLetter)) return code;
        return Normalize(station.StationName).Replace("'", "") switch
        {
            "CAMBRIDGE" => "CBG",
            "LONDON KINGS CROSS" or "KINGS CROSS" => "KGX",
            "LONDON LIVERPOOL STREET" or "LIVERPOOL STREET" => "LST",
            _ => "RAIL"
        };
    }

    private static string Abbreviate(string text, int width) =>
        RailDmiText.TrimToWidth(text, width - RailDmiText.MeasureWidth("...") - 1) + "...";

    internal static string Normalize(string text)
    {
        var normalized = text.ToUpperInvariant().Replace("&", " AND ")
            .Replace('\u2019', '\'').Replace('\u2018', '\'')
            .Replace('\u2013', '-').Replace('\u2014', '-').Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsAsciiLetterOrDigit(c) || " !'()+,-./:?".Contains(c))
                result.Append(c);
            else if (char.IsWhiteSpace(c))
                result.Append(' ');
        }
        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
