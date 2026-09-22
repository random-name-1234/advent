using SixLabors.ImageSharp.PixelFormats;

namespace advent.Data.Rail;

internal sealed record RailSceneSnapshot(
    RailStationSnapshot Cambridge,
    RailStationSnapshot KingsCross,
    DateTimeOffset UpdatedAt);

internal sealed record RailStationSnapshot(
    string HeaderLabel,
    string StationName,
    IReadOnlyList<RailServiceSnapshot> Departures,
    IReadOnlyList<RailServiceSnapshot> Arrivals,
    IReadOnlyList<RailAlertSnapshot> Alerts,
    DateTimeOffset UpdatedAt,
    bool IsUnavailable)
{
    public string StationCode => HeaderLabel;
    public string? Crs { get; init; }
}

internal sealed record RailServiceSnapshot(
    string ScheduledText,
    string LocationText,
    string LocationCode,
    string PlatformText,
    string StatusText,
    Rgba32 StatusColor,
    string OperatorText,
    string CallingText,
    string DetailTicker,
    DateTimeOffset SortTime)
{
    public DateTimeOffset? ScheduledAt { get; init; }
    public bool HasDeparted { get; init; }
    public bool IsFastToCounterpart { get; init; }
}

internal sealed record RailAlertSnapshot(string Message, int SeverityWeight);
