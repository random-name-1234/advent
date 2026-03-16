using System;
using System.Collections.Generic;

namespace advent;

public sealed class SceneControlService
{
    private const int MaxMessageLength = 120;
    private readonly object gate = new();
    private readonly Scene scene;
    private readonly SceneSelector sceneSelector;

    private bool isTestMode;

    public SceneControlService(Scene scene, SceneSelector sceneSelector, bool isTestMode)
    {
        this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
        this.sceneSelector = sceneSelector ?? throw new ArgumentNullException(nameof(sceneSelector));
        this.isTestMode = isTestMode;
        this.scene.ContinuousSceneRequests = isTestMode;
    }

    public IReadOnlyList<string> AvailableSceneNames => sceneSelector.AvailableSceneNames;
    public IReadOnlyList<string> AllSceneNames => sceneSelector.AllSceneNames;

    public void EnqueueNextScene()
    {
        ISpecialScene specialScene;
        lock (gate)
        {
            specialScene = isTestMode
                ? sceneSelector.GetNextSceneInCycle()
                : sceneSelector.GetScene();
        }

        scene.SpecialScenes.Enqueue(specialScene);
        Console.WriteLine($"Enqueued scene: {specialScene.Name}");
    }

    public bool EnqueueSceneByName(string sceneName, out string error)
    {
        ISpecialScene requestedScene;
        lock (gate)
        {
            if (!sceneSelector.TryGetSceneByName(sceneName, out requestedScene))
            {
                error = $"Unknown scene: '{sceneName}'.";
                return false;
            }
        }

        scene.SpecialScenes.Enqueue(requestedScene);
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

        if (normalizedMessage.Length > MaxMessageLength)
        {
            error = $"Message too long. Maximum is {MaxMessageLength} characters.";
            return false;
        }

        if (sceneDuration is { } duration && (duration <= TimeSpan.Zero || duration > SceneTiming.MaxSceneDuration))
        {
            error = $"Duration must be between 0 and {SceneTiming.MaxSceneDuration.TotalSeconds:0} seconds.";
            return false;
        }

        var messageScene = new TimedScene(new MessageScene(normalizedMessage, sceneDuration));
        scene.SpecialScenes.Enqueue(messageScene);
        Console.WriteLine($"Enqueued message: {normalizedMessage}");
        error = string.Empty;
        return true;
    }

    public bool SetMode(bool testMode)
    {
        var changed = false;
        lock (gate)
        {
            if (isTestMode != testMode)
            {
                isTestMode = testMode;
                scene.ContinuousSceneRequests = testMode;
                changed = true;
            }
        }

        if (changed && testMode) EnqueueNextScene();
        return changed;
    }

    public void ClearQueue()
    {
        while (scene.SpecialScenes.TryDequeue(out _))
        {
        }
    }

    public SceneControlStatus GetStatus()
    {
        bool testMode;
        lock (gate)
        {
            testMode = isTestMode;
        }

        return new SceneControlStatus(
            testMode ? "test" : "normal",
            scene.ContinuousSceneRequests,
            scene.SpecialScenes.Count);
    }
}

public sealed record SceneControlStatus(string Mode, bool ContinuousSceneRequests, int QueueLength);
