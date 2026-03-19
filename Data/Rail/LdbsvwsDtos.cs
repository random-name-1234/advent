using System.Text.Json.Serialization;

namespace advent.Data.Rail;

internal sealed record StationBoardDto(
    string? LocationName,
    string? Crs,
    DateTimeOffset? GeneratedAt,
    bool ServicesAreUnavailable,
    ServiceItemDto[]? TrainServices,
    NrccMessageDto[]? NrccMessages);

internal sealed record ServiceItemDto(
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

internal sealed record ServiceLocationDto(
    string? LocationName,
    string? Crs,
    string? Sta,
    string? Ata,
    string? Eta,
    string? Std,
    string? Atd,
    string? Etd,
    string? Platform,
    bool PlatformIsHidden,
    bool IsPass);

internal sealed record EndPointLocationDto(
    string? LocationName,
    string? Crs,
    string? Via);

internal sealed record NrccMessageDto(
    string? Category,
    string? Severity,
    [property: JsonPropertyName("xhtmlMessage")] string? XhtmlMessage);
