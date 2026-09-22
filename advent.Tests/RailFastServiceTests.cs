using System.Text.Json;
using advent.Data.Rail;
using Xunit;

namespace advent.Tests;

public class RailFastServiceTests
{
    private static readonly DateTimeOffset Departure = DateTimeOffset.Parse("2026-09-21T23:40:00+01:00");

    [Theory]
    [InlineData("CBG", "KGX", 0, 48, true)]
    [InlineData("CBG", "KGX", 1, 50, true)]
    [InlineData("KGX", "CBG", 1, 50, true)]
    [InlineData("KGX", "CBG", 2, 60, true)]
    [InlineData("CBG", "KGX", 2, 60, true)]
    [InlineData("KGX", "CBG", 3, 59, true)]
    [InlineData("CBG", "KGX", 3, 55, true)]
    [InlineData("KGX", "CBG", 5, 58, true)]
    [InlineData("KGX", "CBG", 6, 61, true)]
    [InlineData("KGX", "CBG", 7, 63, true)]
    [InlineData("KGX", "CBG", 7, 65, true)]
    [InlineData("CBG", "KGX", 7, 65, true)]
    [InlineData("KGX", "CBG", 8, 65, false)]
    [InlineData("CBG", "KGX", 8, 65, false)]
    [InlineData("KGX", "CBG", 7, 66, false)]
    [InlineData("CBG", "KGX", 12, 90, false)]
    [InlineData("KGX", "CBG", 1, 66, false)]
    [InlineData("CBG", "KGX", 0, 0, false)]
    [InlineData("KGX", "CBG", 0, -1, false)]
    [InlineData("CBG", "LST", 0, 50, false)]
    [InlineData("LST", "CBG", 0, 50, false)]
    [InlineData(" cbg ", " kgx ", 1, 50, true)]
    public void FastBadge_UsesScheduledCorridorLeg_InBothDirections_AcrossMidnight(
        string origin, string target, int stops, int minutes, bool expected)
    {
        var calls = Enumerable.Range(0, stops).Select(_ => Call("CMS")).Append(Call(target.Trim(), minutes)).ToArray();
        Assert.Equal(expected, Snapshot(Service(calls), origin, target).IsFastToCounterpart);
    }

    [Theory]
    [InlineData("2026-09-21", "CBG", "KGX", "05:42", "06:37", "CMS,RYS,LET", true)]
    [InlineData("2026-09-21", "CBG", "KGX", "07:42", "08:39", "CMS,RYS,LET", true)]
    [InlineData("2026-09-21", "CBG", "KGX", "06:55", "08:03", "CMS,FXN,STH,MEL,RYS,AWM,BDK,LET,HIT,WLW", false)]
    [InlineData("2026-09-21", "CBG", "KGX", "22:51", "23:59", "CMS,RYS,AWM,BDK,LET,HIT,SVG,FPK", false)]
    [InlineData("2026-09-21", "KGX", "CBG", "16:18", "17:13", "LET,RYS,CMS", true)]
    [InlineData("2026-09-21", "KGX", "CBG", "22:18", "23:13", "LET,RYS,CMS", true)]
    [InlineData("2026-09-21", "KGX", "CBG", "23:18", "00:16", "LET,BDK,AWM,RYS,CMS", true)]
    [InlineData("2026-09-21", "KGX", "CBG", "18:54", "20:08", "WGC,WLW,KBW,SVG,HIT,LET,BDK,AWM,RYS,MEL,STH,FXN,CMS", false)]
    [InlineData("2026-09-26", "KGX", "CBG", "23:18", "00:15", "LET,BDK,AWM,RYS,CMS", true)]
    [InlineData("2026-09-26", "CBG", "KGX", "22:51", "23:58", "CMS,RYS,AWM,BDK,LET,HIT,SVG,FPK", false)]
    [InlineData("2026-09-27", "CBG", "KGX", "18:05", "19:05", "CMS,RYS,LET", true)]
    [InlineData("2026-09-27", "KGX", "CBG", "23:24", "00:25", "HIT,LET,BDK,AWM,RYS,CMS", true)]
    [InlineData("2026-09-27", "KGX", "CBG", "08:12", "09:15", "SVG,HIT,LET,BDK,AWM,RYS,CMS", true)]
    [InlineData("2026-09-27", "KGX", "CBG", "08:41", "09:50", "FPK,SVG,HIT,LET,BDK,AWM,RYS,CMS", false)]
    public void PublishedTableA_Journeys_AreClassifiedByTheirCorridorPattern(
        string date, string origin, string target, string departureTime, string arrivalTime, string stops, bool expected)
    {
        // Standard Table A, valid May-December 2026; endpoint times/calls verified 2026-09-21.
        var departure = DateTimeOffset.Parse($"{date}T{departureTime}:00+01:00");
        var arrival = DateTimeOffset.Parse($"{date}T{arrivalTime}:00+01:00");
        if (arrival < departure) arrival = arrival.AddDays(1);
        var calls = stops.Split(',').Select(code => Call(code) with { Sta = null })
            .Append(Call(target) with { Sta = arrival.ToString("O") }).ToArray();
        var service = Service(calls) with
        {
            Std = departure.ToString("O"), Destination = [new(target, target, null)]
        };
        Assert.Equal(expected, Snapshot(service, origin, target).IsFastToCounterpart);
    }

    [Fact]
    public void PassThroughPoints_AndStopsBeyondCambridge_DoNotCount()
    {
        var calls = Enumerable.Repeat(Call("SVG") with { IsPass = true }, 20)
            .Concat(new[] { "SVG", "HIT", "LET", "BDK", "AWM", "RYS", "CMS" }.Select(code => Call(code)))
            .Append(Call("CBG", 63))
            .Concat(Enumerable.Repeat(Call("ELY", 70), 10)).Append(Call("KLN", 105)).ToArray();
        var dto = Service(calls) with { Destination = [new("Kings Lynn", "KLN", null)] };
        var snapshot = Snapshot(dto, "KGX", "CBG");
        Assert.True(snapshot.IsFastToCounterpart);
        Assert.Equal("Kings Lynn", snapshot.LocationText);
        var station = RailBoardSceneTests.Snapshot().KingsCross;
        var card = RailCards.Departure(station, snapshot, 2);
        Assert.True(card.IsFast);
        Assert.Equal("KINGS LYNN", card.Destination);
        Assert.Contains(RailCardRenderer.TextRuns(card, TimeSpan.Zero), run => run.Text == "FAST");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    [InlineData(125)]
    public void Delays_DoNotChangeFastClassification_OrHideStatus(int delay)
    {
        var dto = Service([Call("LET"), Call("BDK"), Call("AWM"), Call("RYS"), Call("CMS"), Call("CBG", 58)])
            with { Etd = Departure.AddMinutes(delay).ToString("O") };
        var snapshot = Snapshot(dto, "KGX", "CBG");
        var card = RailCards.Departure(RailBoardSceneTests.Snapshot().KingsCross, snapshot, 1);
        Assert.True(card.IsFast);
        Assert.Equal(delay == 0 ? "ON TIME" : $"+{delay} MIN", card.Status);
    }

    [Fact]
    public void MissingOrInvalidData_DoesNotGuessFast_FromDestinationOrEstimatedTimes()
    {
        var good = Service([Call("KGX", 50)]);
        var cases = new[]
        {
            good with { SubsequentLocations = null },
            good with { SubsequentLocations = [] },
            good with { SubsequentLocations = [Call("CMS")] },
            good with { SubsequentLocations = [Call("KGX", 50) with { IsPass = true }] },
            good with { SubsequentLocations = [Call("KGX", 50) with { Sta = null, Eta = Call("KGX", 50).Sta }] },
            good with { SubsequentLocations = [Call("KGX", 50) with { Sta = "invalid" }] },
            good with { SubsequentLocations = [Call("") , Call("KGX", 50)] },
            good with { Std = null, Etd = Departure.ToString("O") },
            good with { Std = "invalid", Etd = Departure.ToString("O") }
        };
        Assert.All(cases, dto => Assert.False(Snapshot(dto).IsFastToCounterpart));
        Assert.False(DarwinRailSnapshotProvider.BuildDepartureSnapshot(good)!.IsFastToCounterpart);
    }

    [Fact]
    public void CancelledCalls_AndSuppressedServices_AreHandledConservatively()
    {
        var good = Service([Call("CMS"), Call("KGX", 50)]);
        Assert.False(Snapshot(good with { IsCancelled = true }).IsFastToCounterpart);
        Assert.False(Snapshot(good with { ServiceIsSuppressed = true }).IsFastToCounterpart);
        Assert.False(Snapshot(good with { SubsequentLocations = [Call("KGX", 50) with { IsCancelled = true }] }).IsFastToCounterpart);
        var stoppingCalls = new[] { "CMS", "RYS", "AWM", "BDK", "LET", "HIT", "SVG", "FPK" }
            .Select(code => Call(code) with { IsCancelled = code == "SVG" }).Append(Call("KGX", 65)).ToArray();
        Assert.False(Snapshot(Service(stoppingCalls)).IsFastToCounterpart);
        var parsed = JsonSerializer.Deserialize<ServiceLocationDto>("""{"crs":"KGX","isCancelled":true}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(parsed!.IsCancelled);
    }

    [Fact]
    public void NoFastBadge_KeepsSequenceNumber_AndLegacyTickerDoesNotInventFast()
    {
        var snapshot = Snapshot(Service([Call("CMS"), Call("KGX", 75)]));
        var card = RailCards.Departure(RailBoardSceneTests.Snapshot().Cambridge, snapshot, 2);
        Assert.False(card.IsFast);
        Assert.DoesNotContain("Fast", snapshot.DetailTicker);
        Assert.Contains(RailCardRenderer.TextRuns(card, TimeSpan.Zero), run => run.Text == "2ND");
        Assert.DoesNotContain(RailCardRenderer.TextRuns(card, TimeSpan.Zero), run => run.Text == "FAST");
    }

    private static RailServiceSnapshot Snapshot(ServiceItemDto dto, string origin = "CBG", string target = "KGX") =>
        DarwinRailSnapshotProvider.BuildDepartureSnapshot(dto, origin, target)!;

    private static ServiceItemDto Service(ServiceLocationDto[] calls) => new(
        null, [new("London Kings Cross", "KGX", null)], null, calls,
        null, null, null, Departure.ToString("O"), null, null, "5", "Great Northern", false, false, false);

    private static ServiceLocationDto Call(string code, int minutes = 10) => new(
        code, code, Departure.AddMinutes(minutes).ToString("O"), null, null, null, null, null, null, false, false);
}
