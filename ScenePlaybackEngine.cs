using System;
using System.Collections.Concurrent;

namespace advent;

internal sealed class ScenePlaybackEngine
{
    private readonly ConcurrentQueue<ISpecialScene> queuedScenes = new();
    private ISpecialScene? activeScene;

    public ISpecialScene? ActiveScene => activeScene;
    public bool HasActiveScene => activeScene is not null;
    public int QueueLength => queuedScenes.Count;

    public void Enqueue(ISpecialScene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        queuedScenes.Enqueue(scene);
    }

    public void ClearQueue()
    {
        while (queuedScenes.TryDequeue(out _))
        {
        }
    }

    public bool TryPeekQueuedScene(out ISpecialScene scene)
    {
        return queuedScenes.TryPeek(out scene!);
    }

    public void Advance(TimeSpan timeSpan)
    {
        if (activeScene is null && queuedScenes.TryDequeue(out var queuedScene))
        {
            activeScene = queuedScene;
            activeScene.Activate();
        }

        if (activeScene is null)
            return;

        activeScene.Elapsed(timeSpan);
        if (!activeScene.IsActive)
            activeScene = null;
    }
}
