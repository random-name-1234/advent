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
    bool RailConfigured,
    double Latitude = 52.2053,
    double Longitude = 0.1218);

internal enum SceneTransitionStyle
{
    Cut,
    CutWithClockFade,
    Crossfade
}

internal sealed record SceneCatalogRegistration(
    string Name,
    Func<ISpecialScene>? CreateScene,
    bool IncludedInCycle = true,
    TimeSpan? MaxDuration = null,
    Func<bool>? IsReady = null,
    SceneTransitionStyle TransitionStyle = SceneTransitionStyle.Crossfade)
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
            () => CreateSceneInstance(),
            ReadyPredicate);
    }

    private ISpecialScene CreateSceneInstance()
    {
        var timedScene = new TimedScene(CreateScene!(), MaxDuration);
        return TransitionStyle switch
        {
            SceneTransitionStyle.Cut => timedScene,
            SceneTransitionStyle.CutWithClockFade => new ClockFadingScene(timedScene),
            SceneTransitionStyle.Crossfade => new FadingScene(timedScene),
            _ => throw new ArgumentOutOfRangeException(nameof(TransitionStyle), TransitionStyle, "Unknown transition style.")
        };
    }
}
