using System;
using System.Collections.Generic;

namespace advent;

internal sealed class SceneScheduleCoordinator
{
    private readonly Queue<TimeSpan> recentRandomSceneRequests = new();
    private readonly ISceneScheduler scheduler;

    private TimeSpan elapsedSinceStartup;
    private bool isTestMode;
    private TimeSpan timeToNextRandomScene;

    public SceneScheduleCoordinator(ISceneScheduler scheduler, bool isTestMode)
    {
        this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        this.isTestMode = isTestMode;
        timeToNextRandomScene = TimeSpan.FromMinutes(Random.Shared.NextDouble());
    }

    public bool IsTestMode => isTestMode;

    public bool SetMode(bool testMode)
    {
        if (isTestMode == testMode)
            return false;

        isTestMode = testMode;
        return true;
    }

    public void Advance(TimeSpan timeSpan, ScenePlaybackEngine playbackEngine)
    {
        elapsedSinceStartup += timeSpan;

        if (isTestMode)
        {
            EnqueueNextCycleSceneIfIdle(playbackEngine);
            return;
        }

        timeToNextRandomScene -= timeSpan;
        if (timeToNextRandomScene < TimeSpan.Zero)
            EnqueueRandomSceneIfAllowed(playbackEngine);
    }

    public void EnqueueNextScene(ScenePlaybackEngine playbackEngine)
    {
        playbackEngine.Enqueue(isTestMode ? scheduler.GetNextSceneInCycle() : scheduler.GetScene());
    }

    public void EnsureInitialScene(ScenePlaybackEngine playbackEngine)
    {
        if (isTestMode)
            EnqueueNextCycleSceneIfIdle(playbackEngine);
    }

    private void EnqueueRandomSceneIfAllowed(ScenePlaybackEngine playbackEngine)
    {
        while (recentRandomSceneRequests.Count > 0 &&
               elapsedSinceStartup - recentRandomSceneRequests.Peek() >= SceneTiming.RandomSceneWindow)
        {
            recentRandomSceneRequests.Dequeue();
        }

        if (recentRandomSceneRequests.Count >= SceneTiming.MaxRandomSceneRequestsPerWindow)
        {
            var nextAvailableAt = recentRandomSceneRequests.Peek() + SceneTiming.RandomSceneWindow;
            timeToNextRandomScene = nextAvailableAt - elapsedSinceStartup;
            if (timeToNextRandomScene < TimeSpan.FromMilliseconds(1))
                timeToNextRandomScene = TimeSpan.FromMilliseconds(1);
            return;
        }

        recentRandomSceneRequests.Enqueue(elapsedSinceStartup);
        timeToNextRandomScene = TimeSpan.FromMinutes(Random.Shared.NextDouble() * 2);
        playbackEngine.Enqueue(scheduler.GetScene());
    }

    private void EnqueueNextCycleSceneIfIdle(ScenePlaybackEngine playbackEngine)
    {
        if (playbackEngine.HasActiveScene || playbackEngine.QueueLength > 0)
            return;

        playbackEngine.Enqueue(scheduler.GetNextSceneInCycle());
    }
}
