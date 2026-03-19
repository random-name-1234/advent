using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public sealed class Scene : IDisposable
{
    private readonly ScenePlaybackEngine playbackEngine = new();
    private readonly SceneRenderer renderer;

    public Scene(Action<Image<Rgba32>>? clockOverlayRenderer = null)
    {
        renderer = new SceneRenderer(clockOverlayRenderer);
    }

    public Image<Rgba32> Img => renderer.Img;

    internal ScenePlaybackEngine PlaybackEngine => playbackEngine;

    public void Enqueue(ISpecialScene scene)
    {
        playbackEngine.Enqueue(scene);
    }

    public void ClearQueue()
    {
        playbackEngine.ClearQueue();
    }

    public bool TryPeekQueuedScene(out ISpecialScene scene)
    {
        return playbackEngine.TryPeekQueuedScene(out scene);
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        playbackEngine.Advance(timeSpan);
        renderer.AdvanceAndRender(timeSpan, playbackEngine.ActiveScene);
    }

    public void Dispose()
    {
        renderer.Dispose();
    }
}
