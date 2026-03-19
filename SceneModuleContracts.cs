using System;
using System.Collections.Generic;
using advent.Data.Rail;
using advent.Data.Weather;

namespace advent;

internal interface ISceneModule
{
    IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context);
}

internal sealed record SceneModuleContext(
    int Month,
    string ImageSceneDirectory,
    IEnumerable<string>? ExtraImageSceneDirectories,
    IWeatherSnapshotSource? WeatherSnapshotSource,
    IRailSnapshotSource? RailSnapshotSource,
    bool RailConfigured);

internal sealed record SceneCatalogRegistration(
    string Name,
    Func<ISpecialScene>? CreateScene,
    bool IncludedInCycle = true,
    TimeSpan? MaxDuration = null,
    Func<bool>? IsReady = null)
{
    public bool IsKnown => true;
    public bool CanCreate => CreateScene is not null;
    public Func<bool> ReadyPredicate => IsReady ?? SceneCatalogEntry.AlwaysReady;

    public SceneCatalogEntry CreateEntry()
    {
        if (CreateScene is null)
            throw new InvalidOperationException($"Scene '{Name}' cannot be created.");

        return new SceneCatalogEntry(
            Name,
            () => new FadingScene(new TimedScene(CreateScene(), MaxDuration)),
            ReadyPredicate);
    }
}
