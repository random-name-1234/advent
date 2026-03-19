namespace advent.Data.Rail;

internal sealed record RailBoardOptions(
    string BaseUrl,
    string OriginCrs,
    string DestinationCrs,
    string OriginLabel,
    string DestinationLabel,
    string? AuthHeaderName,
    string? AuthHeaderValue,
    string? Username,
    string? Password)
{
    public static RailBoardOptions? TryFromEnvironment()
    {
        if (!ReadBool("ADVENT_RAIL_ENABLED", true))
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

        var originCrs = ReadStationCrs("ADVENT_RAIL_ORIGIN_CRS", "ADVENT_RAIL_CAMBRIDGE_CRS", "CBG");
        var destinationCrs = ReadStationCrs(
            "ADVENT_RAIL_DESTINATION_CRS",
            "ADVENT_RAIL_LONDON_CRS",
            "KGX",
            "ADVENT_RAIL_KINGS_CROSS_CRS");

        return new RailBoardOptions(
            ReadString(
                    "ADVENT_RAIL_LDB_BASE_URL",
                    "https://api1.raildata.org.uk/1010-live-departure-board---staff-version1_0/LDBSVWS")
                .TrimEnd('/'),
            originCrs,
            destinationCrs,
            ReadStationLabel("ADVENT_RAIL_ORIGIN_LABEL", "ADVENT_RAIL_CAMBRIDGE_LABEL", originCrs),
            ReadStationLabel(
                "ADVENT_RAIL_DESTINATION_LABEL",
                "ADVENT_RAIL_LONDON_LABEL",
                destinationCrs,
                "ADVENT_RAIL_KINGS_CROSS_LABEL"),
            authHeaderName,
            authHeaderValue,
            username,
            password);
    }

    public static RailBoardOptions CreateForTesting() => new(
        "https://example.invalid",
        "CBG",
        "KGX",
        "Cambridge",
        "London Kings Cross",
        "X-Test",
        "dummy",
        null,
        null);

    private static string ReadStationCrs(
        string primaryEnvironmentVariable,
        string legacyEnvironmentVariable,
        string fallback,
        string? secondaryLegacyEnvironmentVariable = null)
    {
        var value = ReadOptionalString(primaryEnvironmentVariable)
                    ?? ReadOptionalString(legacyEnvironmentVariable)
                    ?? ReadOptionalString(secondaryLegacyEnvironmentVariable);

        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim().ToUpperInvariant();
    }

    private static string ReadStationLabel(
        string primaryEnvironmentVariable,
        string legacyEnvironmentVariable,
        string fallbackCrs,
        string? secondaryLegacyEnvironmentVariable = null)
    {
        var value = ReadOptionalString(primaryEnvironmentVariable)
                    ?? ReadOptionalString(legacyEnvironmentVariable)
                    ?? ReadOptionalString(secondaryLegacyEnvironmentVariable);

        return string.IsNullOrWhiteSpace(value)
            ? RailStationNames.DisplayName(fallbackCrs, null)
            : RailStationNames.FullLabel(value);
    }

    private static string ReadString(string environmentVariable, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? ReadOptionalString(string? environmentVariable)
    {
        if (string.IsNullOrWhiteSpace(environmentVariable))
            return null;

        var value = Environment.GetEnvironmentVariable(environmentVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
