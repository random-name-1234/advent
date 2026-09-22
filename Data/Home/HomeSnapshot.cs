using System.Text.Json;

namespace advent.Data.Home;

internal enum CatLocation { Unknown, Indoors, Outdoors }
internal sealed record HomeCat(string Id, CatLocation Location, DateTimeOffset? ChangedAt,
    CatLocation PreviousLocation = CatLocation.Unknown);
internal sealed record AgileSlot(DateTimeOffset StartsAt, DateTimeOffset EndsAt, double Price, string Band);
internal sealed record CheapWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
internal sealed record HomeSnapshot(
    DateTimeOffset GeneratedAt,
    DateTimeOffset? PetsObservedAt,
    IReadOnlyList<HomeCat> Cats,
    DateTimeOffset? AgileObservedAt,
    AgileSlot? Current,
    IReadOnlyList<AgileSlot> Slots,
    CheapWindow? NextCheapWindow)
{
    internal bool IsFresh(DateTimeOffset now) => Fresh(GeneratedAt, now, TimeSpan.FromMinutes(2));
    internal bool PetsReady(DateTimeOffset now) => IsFresh(now) &&
        Fresh(PetsObservedAt, now, TimeSpan.FromMinutes(5)) && Cats.Any(c => c.Location != CatLocation.Unknown);
    internal bool AgileReady(DateTimeOffset now) => IsFresh(now) &&
        Fresh(AgileObservedAt, now, TimeSpan.FromMinutes(30)) && Current is not null &&
        Current.StartsAt <= now && now < Current.EndsAt;

    private static bool Fresh(DateTimeOffset? timestamp, DateTimeOffset now, TimeSpan age) =>
        timestamp is { } date && date <= now.AddSeconds(30) && now - date <= age;

    internal static HomeSnapshot? Parse(JsonElement root)
    {
        if (Field(root, "schema_version").ValueKind != JsonValueKind.Number ||
            !Field(root, "schema_version").TryGetInt32(out var version) || version != 1 ||
            String(root, "source") != "live" || Date(root, "generated_at") is not { } generated) return null;

        var pets = Field(root, "pets");
        var cats = new List<HomeCat>();
        var petsAt = String(pets, "status") == "ok" ? Date(pets, "observed_at") : null;
        if (Field(pets, "items") is { ValueKind: JsonValueKind.Array } items)
        {
            foreach (var item in items.EnumerateArray())
            {
                var id = String(item, "id");
                if (id is not ("barney" or "beaker") || cats.Any(c => c.Id == id)) continue;
                var location = String(item, "location") switch
                {
                    "indoors" => CatLocation.Indoors,
                    "outdoors" => CatLocation.Outdoors,
                    _ => CatLocation.Unknown
                };
                cats.Add(new HomeCat(id, location, Date(item, "changed_at")));
            }
        }

        var agile = Field(root, "agile");
        var agileAt = String(agile, "status") == "ok" && String(agile, "currency") == "GBP" &&
                      String(agile, "unit") == "p/kWh" ? Date(agile, "observed_at") : null;
        var current = ReadSlot(Field(agile, "current"));
        var slots = new List<AgileSlot>();
        if (Field(agile, "slots") is { ValueKind: JsonValueKind.Array } rates)
            foreach (var item in rates.EnumerateArray().Take(48))
                if (ReadSlot(item) is { } slot && (slots.Count == 0 || slots[^1].EndsAt <= slot.StartsAt)) slots.Add(slot);

        var cheap = Field(agile, "next_cheap_window");
        CheapWindow? window = Date(cheap, "starts_at") is { } start && Date(cheap, "ends_at") is { } end && end > start
            ? new CheapWindow(start, end) : null;
        return new HomeSnapshot(generated, petsAt, cats.AsReadOnly(), agileAt, current, slots.AsReadOnly(), window);
    }

    private static AgileSlot? ReadSlot(JsonElement value)
    {
        if (Date(value, "starts_at") is not { } start || Date(value, "ends_at") is not { } end ||
            end - start != TimeSpan.FromMinutes(30) || Field(value, "price_p_per_kwh") is not { ValueKind: JsonValueKind.Number } price ||
            !price.TryGetDouble(out var number) || !double.IsFinite(number)) return null;
        var band = String(value, "band");
        return new AgileSlot(start, end, number, band is "cheap" or "normal" or "expensive" ? band : "unknown");
    }

    private static JsonElement Field(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) ? value : default;
    private static string? String(JsonElement parent, string name) =>
        Field(parent, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    private static DateTimeOffset? Date(JsonElement parent, string name)
    {
        var value = String(parent, name);
        // Require an explicit offset, rather than letting the Pi's timezone guess.
        if (value is null || !(value.EndsWith('Z') || value.Length >= 6 && value[^3] == ':' && value[^6] is '+' or '-')) return null;
        return DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var result) ? result : null;
    }
}

internal interface IHomeSnapshotSource
{
    bool TryGetSnapshot(out HomeSnapshot snapshot);
}
