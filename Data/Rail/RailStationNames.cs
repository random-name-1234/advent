using System.Globalization;

namespace advent.Data.Rail;

internal static class RailStationNames
{
    public static string CompactLabel(string stationName) => NormalizeLocationKey(stationName) switch
    {
        "CAM" or "CBG" => "Cambridge",
        "KGX" or "LONDON KINGS CROSS" or "KINGS CROSS" => "Kings Cross",
        _ => HumanizedLabel(stationName)
    };

    public static string FullLabel(string stationName)
    {
        if (string.IsNullOrWhiteSpace(stationName))
            return "---";

        return NormalizeLocationKey(stationName) switch
        {
            "CAM" or "CBG" => "Cambridge",
            "KGX" or "KINGS CROSS" or "LONDON KINGS CROSS" => "London Kings Cross",
            "LST" or "LIVERPOOL STREET" or "LONDON LIVERPOOL STREET" => "London Liverpool Street",
            "ELY" => "Ely",
            "KLN" => "Kings Lynn",
            "PBO" => "Peterborough",
            "SVG" => "Stevenage",
            "FPK" => "Finsbury Park",
            "FXN" => "Foxton",
            "RYS" => "Royston",
            "ASM" => "Ashwell & Morden",
            _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(NormalizeLocationKey(stationName).ToLowerInvariant())
        };
    }

    public static string DisplayName(string? crs, string? locationName)
    {
        if (!string.IsNullOrWhiteSpace(crs))
        {
            var mappedName = FullLabel(crs);
            if (!string.Equals(mappedName, crs.Trim().ToUpperInvariant(), StringComparison.Ordinal))
                return mappedName;
        }

        if (!string.IsNullOrWhiteSpace(locationName))
            return FullLabel(locationName);

        if (!string.IsNullOrWhiteSpace(crs))
            return crs.Trim().ToUpperInvariant();

        return "---";
    }

    public static string NormalizeLocationKey(string locationName) => locationName.Trim().ToUpperInvariant() switch
    {
        "LONDON KING'S CROSS" => "LONDON KINGS CROSS",
        "KING'S CROSS" => "KINGS CROSS",
        "LONDON KX" => "LONDON KINGS CROSS",
        "LIV ST" => "LONDON LIVERPOOL STREET",
        "LIVERPOOL STREET" => "LONDON LIVERPOOL STREET",
        "LONDON LIVERPOOL ST" => "LONDON LIVERPOOL STREET",
        "PETERBORO" => "PETERBOROUGH",
        "FINSBURY PK" => "FINSBURY PARK",
        _ => locationName.Trim().ToUpperInvariant()
    };

    public static string HumanizedLabel(string locationName) => NormalizeLocationKey(locationName) switch
    {
        "CAM" or "CBG" => "Cambridge",
        "KGX" => "Kings Cross",
        _ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(NormalizeLocationKey(locationName).ToLowerInvariant())
    };

    public static string BuildLocationCode(string? locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
            return "---";

        var cleaned = new string(locationName
            .Where(static c => char.IsLetter(c) || c is ' ')
            .ToArray());
        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            var initials = string.Concat(words.Take(2).Select(static word => char.ToUpperInvariant(word[0])));
            if (initials.Length >= 2)
                return initials;
        }

        var letters = new string(cleaned.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return letters.Length is 0 ? "---" : letters;
    }
}
