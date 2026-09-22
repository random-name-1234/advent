using advent.Data.Home;

namespace advent;

internal sealed class NewSceneModule : ISceneModule
{
    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        var rotate = context.NewScenesInRotation;
        return
        [
            new("Night Train", () => new NightTrainScene(), IncludedInCycle: rotate),
            new("Aquarium", () => new AquariumScene(), IncludedInCycle: rotate),
            new("Pixel City", () => new PixelCityScene(latitude: context.Latitude, longitude: context.Longitude), IncludedInCycle: rotate),
            new("Breakout", () => new BreakoutScene(), IncludedInCycle: rotate),
            new("Weather Window", () => new WeatherWindowScene(context.WeatherSnapshotSource!), IncludedInCycle: rotate,
                IsReady: () => context.WeatherSnapshotSource?.TryGetSnapshot(out _) == true),
            new("Moonlit Landscape", () => new MoonlitLandscapeScene(), IncludedInCycle: rotate),
            new("Two Cats", () => new TwoCatsScene(context.HomeSnapshotSource!), IncludedInCycle: rotate,
                IsReady: () => Ready(context.HomeSnapshotSource, pets: true)),
            new("Agile Power", () => new AgilePowerScene(context.HomeSnapshotSource!), IncludedInCycle: rotate,
                IsReady: () => Ready(context.HomeSnapshotSource, pets: false))
        ];
    }

    private static bool Ready(IHomeSnapshotSource? source, bool pets) =>
        source is not null && source.TryGetSnapshot(out var snapshot) &&
        (pets ? snapshot.PetsReady(DateTimeOffset.UtcNow) : snapshot.AgileReady(DateTimeOffset.UtcNow));
}
