using System;
using System.Linq;

namespace advent;

public sealed class SceneControlService
{
    private readonly object gate = new();
    private readonly ScenePlaybackEngine playbackEngine;
    private readonly SceneScheduleCoordinator scheduleCoordinator;
    private readonly ISceneScheduler scheduler;

    internal SceneControlService(ScenePlaybackEngine playbackEngine, ISceneScheduler scheduler, bool isTestMode)
    {
        this.playbackEngine = playbackEngine ?? throw new ArgumentNullException(nameof(playbackEngine));
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        scheduleCoordinator = new SceneScheduleCoordinator(scheduler, isTestMode);
        scheduleCoordinator.EnsureInitialScene(playbackEngine);
    }

    public IReadOnlyList<string> AvailableSceneNames => scheduler.AvailableSceneNames;
    public IReadOnlyList<string> AllSceneNames => scheduler.AllSceneNames;
    public IReadOnlyList<string> KnownSceneNames => scheduler.KnownSceneNames;
    public ISpecialScene? ActiveScene => playbackEngine.ActiveScene;

    public SceneCatalogStatus GetSceneCatalog()
    {
        var availableNames = scheduler.AvailableSceneNames
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cycleNames = scheduler.AllSceneNames
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scenes = scheduler.KnownSceneNames
            .Select(name => new SceneCatalogItemStatus(
                name,
                availableNames.Contains(name),
                cycleNames.Contains(name)))
            .ToArray();

        return new SceneCatalogStatus(scenes);
    }

    public void Advance(TimeSpan timeSpan)
    {
        lock (gate)
        {
            playbackEngine.Advance(timeSpan);
            scheduleCoordinator.Advance(timeSpan, playbackEngine);
        }
    }

    public void EnqueueNextScene()
    {
        lock (gate)
        {
            scheduleCoordinator.EnqueueNextScene(playbackEngine);
        }

        Console.WriteLine("Enqueued scene: next-random-or-cycle");
    }

    public bool EnqueueSceneByName(string sceneName, out string error)
    {
        ISpecialScene requestedScene;
        lock (gate)
        {
            var selection = scheduler.SelectSceneByName(sceneName);
            if (selection.Status != SceneSelectionStatus.Ready)
            {
                error = selection.Status switch
                {
                    SceneSelectionStatus.Unavailable => $"Scene '{sceneName}' is not ready yet.",
                    _ => $"Unknown scene: '{sceneName}'."
                };
                return false;
            }

            requestedScene = selection.Scene!;
            playbackEngine.Enqueue(requestedScene);
        }

        Console.WriteLine($"Enqueued scene: {requestedScene.Name}");
        error = string.Empty;
        return true;
    }

    public bool EnqueueMessage(string message, TimeSpan? sceneDuration, out string error)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            error = "Message text is required.";
            return false;
        }

        var normalizedMessage = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (normalizedMessage.Length == 0)
        {
            error = "Message text is required.";
            return false;
        }

        if (normalizedMessage.Length > 120)
        {
            error = "Message too long. Maximum is 120 characters.";
            return false;
        }

        if (sceneDuration is { } duration && (duration <= TimeSpan.Zero || duration > SceneTiming.MaxSceneDuration))
        {
            error = $"Duration must be between 1 and {SceneTiming.MaxSceneDuration.TotalSeconds:0} seconds.";
            return false;
        }

        var messageScene = new FadingScene(new MessageScene(normalizedMessage, sceneDuration));
        lock (gate)
        {
            playbackEngine.Enqueue(messageScene);
        }

        Console.WriteLine($"Enqueued message: {normalizedMessage}");
        error = string.Empty;
        return true;
    }

    public bool SetMode(bool testMode)
    {
        bool changed;
        lock (gate)
        {
            changed = scheduleCoordinator.SetMode(testMode);
            if (changed)
                scheduleCoordinator.EnsureInitialScene(playbackEngine);
        }

        return changed;
    }

    public void ClearQueue()
    {
        lock (gate)
        {
            playbackEngine.ClearQueue();
        }
    }

    public SceneControlStatus GetStatus()
    {
        lock (gate)
        {
            return new SceneControlStatus(
                scheduleCoordinator.IsTestMode ? "test" : "normal",
                scheduleCoordinator.IsTestMode,
                playbackEngine.QueueLength,
                playbackEngine.ActiveScene?.Name,
                playbackEngine.HasActiveScene);
        }
    }
}

public sealed record SceneControlStatus(
    string Mode,
    bool ContinuousSceneRequests,
    int QueueLength,
    string? ActiveSceneName,
    bool HasActiveScene);

public sealed record SceneCatalogStatus(IReadOnlyList<SceneCatalogItemStatus> Items);

public sealed record SceneCatalogItemStatus(string Name, bool Available, bool IncludedInCycle);
