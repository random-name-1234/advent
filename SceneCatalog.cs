using System;
using System.Collections.Generic;
using System.Linq;

namespace advent;

internal interface ISceneCatalog
{
    IReadOnlyList<string> AvailableSceneNames { get; }
    IReadOnlyList<string> AllSceneNames { get; }
    IReadOnlyList<string> KnownSceneNames { get; }
    IReadOnlyList<SceneCatalogEntry> CycleEntries { get; }
    SceneSelection SelectSceneByName(string? sceneName);
}

internal sealed class SceneCatalog : ISceneCatalog
{
    private readonly IReadOnlyList<SceneCatalogEntry> cycleEntries;
    private readonly IReadOnlyDictionary<string, SceneCatalogRegistration> knownRegistrationsByName;
    private readonly IReadOnlyList<SceneCatalogEntry> selectableEntries;

    private SceneCatalog(
        IReadOnlyList<SceneCatalogEntry> cycleEntries,
        IReadOnlyList<SceneCatalogEntry> selectableEntries,
        IReadOnlyDictionary<string, SceneCatalogRegistration> knownRegistrationsByName,
        IReadOnlyList<string> knownSceneNames)
    {
        this.cycleEntries = cycleEntries;
        this.selectableEntries = selectableEntries;
        this.knownRegistrationsByName = knownRegistrationsByName;
        AllSceneNames = cycleEntries.Select(static entry => entry.Name).ToArray();
        KnownSceneNames = knownSceneNames;
    }

    public IReadOnlyList<string> AvailableSceneNames => selectableEntries
        .Where(static entry => entry.IsReady())
        .Select(static entry => entry.Name)
        .ToArray();

    public IReadOnlyList<string> AllSceneNames { get; }
    public IReadOnlyList<string> KnownSceneNames { get; }
    public IReadOnlyList<SceneCatalogEntry> CycleEntries => cycleEntries;

    public SceneSelection SelectSceneByName(string? sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return SceneSelection.NotFound;

        if (!knownRegistrationsByName.TryGetValue(sceneName.Trim(), out var registration))
            return SceneSelection.NotFound;

        if (!registration.CanCreate)
            return SceneSelection.Unavailable;

        return registration.ReadyPredicate()
            ? new SceneSelection(SceneSelectionStatus.Ready, registration.CreateEntry().Create())
            : SceneSelection.Unavailable;
    }

    public static SceneCatalog Create(IEnumerable<ISceneModule> modules, SceneModuleContext context)
    {
        ArgumentNullException.ThrowIfNull(modules);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(context), context.Month, "Month must be in the range 1-12.");

        var registrations = modules
            .SelectMany(module => module.RegisterScenes(context))
            .ToArray();

        var cycleEntries = registrations
            .Where(static registration => registration.IncludedInCycle && registration.CanCreate)
            .Select(static registration => registration.CreateEntry())
            .ToArray();

        var selectableEntries = registrations
            .Where(static registration => registration.CanCreate)
            .Select(static registration => registration.CreateEntry())
            .ToArray();

        var knownRegistrationsByName = BuildKnownRegistrationsByName(registrations);
        var knownSceneNames = BuildKnownSceneNames(registrations);
        return new SceneCatalog(cycleEntries, selectableEntries, knownRegistrationsByName, knownSceneNames);
    }

    private static IReadOnlyDictionary<string, SceneCatalogRegistration> BuildKnownRegistrationsByName(
        IReadOnlyList<SceneCatalogRegistration> registrations)
    {
        var byName = new Dictionary<string, SceneCatalogRegistration>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in registrations)
        {
            if (!byName.TryAdd(registration.Name, registration))
                Console.WriteLine($"Duplicate scene name detected: '{registration.Name}'. First definition is used.");
        }

        return byName;
    }

    private static IReadOnlyList<string> BuildKnownSceneNames(IReadOnlyList<SceneCatalogRegistration> registrations)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var registration in registrations)
        {
            if (seen.Add(registration.Name))
                names.Add(registration.Name);
        }

        return names;
    }
}

internal sealed record SceneCatalogEntry(string Name, Func<ISpecialScene> Create, Func<bool> IsReady)
{
    public static readonly Func<bool> AlwaysReady = static () => true;
}

internal enum SceneSelectionStatus
{
    NotFound,
    Unavailable,
    Ready
}

internal readonly record struct SceneSelection(SceneSelectionStatus Status, ISpecialScene? Scene)
{
    public static SceneSelection NotFound => new(SceneSelectionStatus.NotFound, null);
    public static SceneSelection Unavailable => new(SceneSelectionStatus.Unavailable, null);
}
