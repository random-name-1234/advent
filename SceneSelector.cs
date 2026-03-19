using System;
using System.Collections.Generic;
using System.Linq;
using advent.Data.Rail;
using advent.Data.Weather;

namespace advent;

public sealed class SceneSelector : ISceneScheduler
{
    private static readonly IReadOnlyList<ISceneModule> DefaultModules =
    [
        new WeatherSceneModule(),
        new BuiltinSceneModule(),
        new RailSceneModule(),
        new SeasonalSceneModule(),
        new ImageSceneModule(),
        new ManualSceneModule()
    ];

    private readonly ISceneCatalog sceneCatalog;
    private readonly Func<int, int> nextIndex;
    private int cycleIndex;

    public SceneSelector()
        : this(DateTime.Now.Month)
    {
    }

    public SceneSelector(
        int month,
        Func<int, int>? nextIndex = null,
        string imageSceneDirectory = "advent-images",
        IEnumerable<string>? extraImageSceneDirectories = null)
        : this(
            SceneCatalog.Create(
                DefaultModules,
                new SceneModuleContext(
                    month,
                    imageSceneDirectory,
                    extraImageSceneDirectories,
                    WeatherSnapshotSource: null,
                    RailSnapshotSource: null,
                    RailConfigured: RailBoardScene.IsConfiguredFromEnvironment())),
            nextIndex)
    {
    }

    internal SceneSelector(
        int month,
        Func<int, int>? nextIndex,
        string imageSceneDirectory,
        IEnumerable<string>? extraImageSceneDirectories,
        IWeatherSnapshotSource? weatherSnapshotSource = null,
        IRailSnapshotSource? railSnapshotSource = null)
        : this(
            SceneCatalog.Create(
                DefaultModules,
                new SceneModuleContext(
                    month,
                    imageSceneDirectory,
                    extraImageSceneDirectories,
                    weatherSnapshotSource,
                    railSnapshotSource,
                    railSnapshotSource is not null || RailBoardScene.IsConfiguredFromEnvironment())),
            nextIndex)
    {
    }

    internal SceneSelector(ISceneCatalog sceneCatalog, Func<int, int>? nextIndex = null)
    {
        this.sceneCatalog = sceneCatalog ?? throw new ArgumentNullException(nameof(sceneCatalog));
        this.nextIndex = nextIndex ?? Random.Shared.Next;
        cycleIndex = 0;
    }

    public IReadOnlyList<string> AvailableSceneNames => sceneCatalog.AvailableSceneNames;
    public IReadOnlyList<string> AllSceneNames => sceneCatalog.AllSceneNames;
    public IReadOnlyList<string> KnownSceneNames => sceneCatalog.KnownSceneNames;

    public ISpecialScene GetScene()
    {
        var candidates = GetReadyCycleEntries();
        var index = nextIndex(candidates.Count);
        if ((uint)index >= (uint)candidates.Count)
            throw new InvalidOperationException(
                $"Scene index provider returned {index}, but valid range is 0..{candidates.Count - 1}.");

        return candidates[index].Create();
    }

    public bool TryGetSceneByName(string sceneName, out ISpecialScene scene)
    {
        var selection = SelectSceneByName(sceneName);
        scene = selection.Scene!;
        return selection.Status == SceneSelectionStatus.Ready;
    }

    public string GetNextSceneNameInCycle()
    {
        return GetNextCycleEntry(requireReady: false).Name;
    }

    public ISpecialScene GetNextSceneInCycle()
    {
        return GetNextCycleEntry(requireReady: true).Create();
    }

    internal SceneSelection SelectSceneByName(string? sceneName)
    {
        return sceneCatalog.SelectSceneByName(sceneName);
    }

    SceneSelection ISceneScheduler.SelectSceneByName(string? sceneName)
    {
        return SelectSceneByName(sceneName);
    }

    private SceneCatalogEntry GetNextCycleEntry(bool requireReady)
    {
        if (sceneCatalog.CycleEntries.Count == 0)
            throw new InvalidOperationException("No scenes are available for cycling.");

        if (!requireReady)
            return AdvanceCycle();

        SceneCatalogEntry? fallback = null;
        for (var i = 0; i < sceneCatalog.CycleEntries.Count; i++)
        {
            var candidate = AdvanceCycle();
            fallback ??= candidate;
            if (candidate.IsReady())
                return candidate;
        }

        return fallback!;
    }

    private IReadOnlyList<SceneCatalogEntry> GetReadyCycleEntries()
    {
        var readyEntries = sceneCatalog.CycleEntries
            .Where(static entry => entry.IsReady())
            .ToArray();

        return readyEntries.Length > 0 ? readyEntries : sceneCatalog.CycleEntries;
    }

    private SceneCatalogEntry AdvanceCycle()
    {
        var selectedEntry = sceneCatalog.CycleEntries[cycleIndex];
        cycleIndex = (cycleIndex + 1) % sceneCatalog.CycleEntries.Count;
        return selectedEntry;
    }
}
