using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using advent.Data.Home;
using Xunit;

namespace advent.Tests;

public sealed class HomeSnapshotTests
{
    private static readonly DateTimeOffset Now = NewSceneCapture.FixtureTime;

    private static JsonObject Payload() => JsonNode.Parse("""
        {"schema_version":1,"source":"live","generated_at":"2026-09-21T19:10:00Z",
         "pets":{"status":"ok","observed_at":"2026-09-21T19:09:00Z","items":[
             {"id":"barney","name":"Barney","location":"indoors","changed_at":"2026-09-21T17:00:00Z"},
             {"id":"beaker","name":"Beaker","location":"outdoors","changed_at":"2026-09-21T18:00:00Z"}]},
         "agile":{"status":"ok","observed_at":"2026-09-21T19:08:00Z","currency":"GBP","unit":"p/kWh",
            "current":{"starts_at":"2026-09-21T19:00:00Z","ends_at":"2026-09-21T19:30:00Z","price_p_per_kwh":-1.25,"band":"cheap"},
            "slots":[{"starts_at":"2026-09-21T19:00:00Z","ends_at":"2026-09-21T19:30:00Z","price_p_per_kwh":-1.25,"band":"cheap"}],
            "next_cheap_window":{"starts_at":"2026-09-21T19:00:00Z","ends_at":"2026-09-21T20:00:00Z"}}}
        """)!.AsObject();

    private static HomeSnapshot? Parse(JsonNode payload)
    {
        using var document = JsonDocument.Parse(payload.ToJsonString());
        return HomeSnapshot.Parse(document.RootElement);
    }

    [Fact]
    public void ParsesVersionedContractWithoutLosingNegativePrices()
    {
        var snapshot = Assert.IsType<HomeSnapshot>(Parse(Payload()));
        Assert.True(snapshot.PetsReady(Now));
        Assert.True(snapshot.AgileReady(Now));
        Assert.Equal(-1.25, snapshot.Current!.Price);
        Assert.Equal(CatLocation.Indoors, snapshot.Cats[0].Location);
        Assert.Equal(CatLocation.Outdoors, snapshot.Cats[1].Location);
        Assert.All(snapshot.Cats, cat => Assert.Equal(CatLocation.Unknown, cat.PreviousLocation));
    }

    [Theory]
    [InlineData("source", "demo")]
    [InlineData("source", "hybrid")]
    [InlineData("schema_version", "1")]
    [InlineData("generated_at", "2026-09-21T19:10:00")]
    public void RejectsDemoWrongVersionTypesAndNaiveDates(string field, string value)
    {
        var json = Payload(); json[field] = value;
        Assert.Null(Parse(json));
    }

    [Fact]
    public void RejectsUnsupportedVersion()
    {
        var json = Payload(); json["schema_version"] = 2;
        Assert.Null(Parse(json));
    }

    [Theory]
    [InlineData("stale")]
    [InlineData("unavailable")]
    [InlineData("broken")]
    public void PetStatusIsIndependentOfAgile(string status)
    {
        var json = Payload(); json["pets"]!["status"] = status;
        var snapshot = Parse(json)!;
        Assert.False(snapshot.PetsReady(Now));
        Assert.True(snapshot.AgileReady(Now));
    }

    [Theory]
    [InlineData("currency", "USD")]
    [InlineData("unit", "GBP/kWh")]
    [InlineData("status", "stale")]
    [InlineData("observed_at", "2026-09-21T17:00:00Z")]
    public void AgileRejectsWrongUnitsOrStaleDataWithoutHidingCats(string field, string value)
    {
        var json = Payload(); json["agile"]![field] = value;
        var snapshot = Parse(json)!;
        Assert.True(snapshot.PetsReady(Now));
        Assert.False(snapshot.AgileReady(Now));
    }

    [Fact]
    public void UnknownCatIsNotAssumedToBeOutdoors()
    {
        var json = Payload(); json["pets"]!["items"]![1]!["location"] = "unavailable";
        var snapshot = Parse(json)!;
        Assert.True(snapshot.PetsReady(Now));
        Assert.Equal(CatLocation.Unknown, snapshot.Cats[1].Location);
        json["pets"]!["items"]![0]!["location"] = null;
        Assert.False(Parse(json)!.PetsReady(Now));
    }

    [Fact]
    public void SourceAgeGenerationAgeAndRateRolloverAreAllChecked()
    {
        var snapshot = Parse(Payload())!;
        Assert.False(snapshot.PetsReady(Now.AddMinutes(3)));
        Assert.False((snapshot with { GeneratedAt = Now, PetsObservedAt = Now.AddMinutes(-6) }).PetsReady(Now));
        Assert.False((snapshot with { GeneratedAt = Now.AddMinutes(5) }).IsFresh(Now));
        Assert.False((snapshot with { GeneratedAt = Now.AddMinutes(20) }).AgileReady(Now.AddMinutes(20)));
        Assert.False((snapshot with { Current = snapshot.Current! with { StartsAt = Now.AddMinutes(1) } }).AgileReady(Now));
    }

    [Fact]
    public void MalformedOptionalDomainsDoNotThrowOrCreateValues()
    {
        var json = Payload(); json["pets"] = "broken";
        Assert.Empty(Parse(json)!.Cats);
        json["agile"] = new JsonArray();
        Assert.Null(Parse(json)!.Current);
        Assert.Null(Parse(json)!.NextCheapWindow);
        Assert.Empty(Parse(json)!.Slots);
    }

    [Theory]
    [InlineData("2026-10-25T01:00:00+01:00", "2026-10-25T01:30:00+01:00")]
    [InlineData("2026-10-25T01:00:00Z", "2026-10-25T01:30:00Z")]
    [InlineData("2026-03-29T00:30:00Z", "2026-03-29T02:00:00+01:00")]
    public void ExplicitIntervalsSurviveBritishClockChanges(string start, string end)
    {
        var json = Payload(); json["agile"]!["current"]!["starts_at"] = start; json["agile"]!["current"]!["ends_at"] = end;
        var slot = Parse(json)!.Current!;
        Assert.Equal(TimeSpan.FromMinutes(30), slot.EndsAt - slot.StartsAt);
    }

    [Fact]
    public void InvalidIntervalsAndPriceStringsAreNotRendered()
    {
        var json = Payload(); json["agile"]!["current"]!["price_p_per_kwh"] = "0";
        Assert.Null(Parse(json)!.Current);
        json = Payload(); json["agile"]!["current"]!["ends_at"] = "2026-09-21T18:00:00Z";
        Assert.Null(Parse(json)!.Current);
    }

    [Fact]
    public void OnlyObservedKnownTransitionsBecomeMovement()
    {
        var before = new HomeCat("barney", CatLocation.Outdoors, Now.AddMinutes(-40));
        var after = new HomeCat("barney", CatLocation.Indoors, Now);
        Assert.Equal(CatLocation.Outdoors, HomeSnapshotStore.TrackMovement(after, [before], Now).PreviousLocation);
        Assert.Equal(CatLocation.Unknown, HomeSnapshotStore.TrackMovement(after, [], Now).PreviousLocation);
        Assert.Equal(CatLocation.Unknown, HomeSnapshotStore.TrackMovement(after, [before with { Location = CatLocation.Unknown }], Now).PreviousLocation);
        Assert.Equal(CatLocation.Unknown, HomeSnapshotStore.TrackMovement(after with { ChangedAt = Now.AddHours(-1) }, [before], Now).PreviousLocation);
    }

    [Fact]
    public async Task StoreHandlesErrorsAndInvalidatesExplicitUnavailableData()
    {
        var clock = new MutableClock(Now);
        var handler = new StubHandler { Json = Payload().ToJsonString() };
        using var store = new HomeSnapshotStore(new Uri("http://dashboard.test/api/output/advent/v1"), "read-only-test", clock, handler);
        await store.RefreshOnceAsync(CancellationToken.None);
        Assert.True(store.TryGetSnapshot(out _));
        Assert.Equal("Bearer read-only-test", handler.Authorization);
        handler.Status = HttpStatusCode.ServiceUnavailable;
        await store.RefreshOnceAsync(CancellationToken.None);
        Assert.True(store.TryGetSnapshot(out _));
        clock.Now = Now.AddMinutes(3);
        Assert.False(store.TryGetSnapshot(out _));
        clock.Now = Now;
        handler.Status = HttpStatusCode.OK;
        var demo = Payload(); demo["source"] = "demo"; handler.Json = demo.ToJsonString();
        await store.RefreshOnceAsync(CancellationToken.None);
        Assert.False(store.TryGetSnapshot(out _));
    }

    private sealed class MutableClock(DateTimeOffset now) : TimeProvider
    {
        internal DateTimeOffset Now = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        internal string Json = "{}";
        internal HttpStatusCode Status = HttpStatusCode.OK;
        internal string? Authorization;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Json) });
        }
    }
}
