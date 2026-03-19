using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Reflection;
using Xunit;

namespace advent.Tests;

public class SceneOrchestrationTests
{
    [Fact]
    public void TestMode_QueuesSingleCycleScene_WhenIdle()
    {
        var control = CreateControlService(isTestMode: true, out var playbackEngine);

        Assert.Equal(1, playbackEngine.QueueLength);

        control.Advance(TimeSpan.FromMilliseconds(10));

        Assert.True(playbackEngine.HasActiveScene);
        Assert.Equal(0, playbackEngine.QueueLength);
    }

    [Fact]
    public void NormalMode_QueuesScene_WhenRandomTimerExpires()
    {
        var control = CreateControlService(isTestMode: false, out var playbackEngine);

        ForceRandomSceneTimer(control, TimeSpan.Zero);
        control.Advance(TimeSpan.FromMilliseconds(10));

        Assert.Equal(1, playbackEngine.QueueLength);
    }

    [Fact]
    public void NormalMode_DoesNotQueueMoreThanTwoScenesWithinOneMinute()
    {
        var control = CreateControlService(isTestMode: false, out var playbackEngine);

        ForceRandomSceneTimer(control, TimeSpan.Zero);
        control.Advance(TimeSpan.FromMilliseconds(10));
        ForceRandomSceneTimer(control, TimeSpan.Zero);
        control.Advance(TimeSpan.FromMilliseconds(10));
        ForceRandomSceneTimer(control, TimeSpan.Zero);
        control.Advance(TimeSpan.FromMilliseconds(10));

        Assert.True(playbackEngine.HasActiveScene);
        Assert.Equal(1, playbackEngine.QueueLength);
    }

    [Fact]
    public void QueuedScene_IsActivatedElapsedAndDrawn_WhenStillActive()
    {
        using var renderer = new SceneRenderer();
        var playbackEngine = new ScenePlaybackEngine();
        var special = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[3, 3] = new Rgba32(255, 0, 0)
        };
        playbackEngine.Enqueue(special);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(20));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(20), playbackEngine.ActiveScene);

        Assert.Equal(1, special.ActivateCount);
        Assert.Equal(1, special.ElapsedCount);
        Assert.Equal(1, special.DrawCount);
        Assert.Equal(255, renderer.Img[3, 3].R);
    }

    [Fact]
    public void QueuedScene_DrawsClockOverlayAfterScene_WhenSceneDoesNotHideTime()
    {
        using var renderer = new SceneRenderer(img => img[4, 4] = new Rgba32(0, 255, 0));
        var playbackEngine = new ScenePlaybackEngine();
        var special = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = false,
            OnDraw = img => img[4, 4] = new Rgba32(255, 0, 0)
        };
        playbackEngine.Enqueue(special);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(20));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(20), playbackEngine.ActiveScene);

        Assert.Equal(new Rgba32(0, 255, 0), renderer.Img[4, 4]);
    }

    [Fact]
    public void QueuedScene_SuppressesClockOverlay_WhenSceneHidesTime()
    {
        using var renderer = new SceneRenderer(img => img[5, 5] = new Rgba32(0, 255, 0));
        var playbackEngine = new ScenePlaybackEngine();
        var special = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[5, 5] = new Rgba32(255, 0, 0)
        };
        playbackEngine.Enqueue(special);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(20));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(20), playbackEngine.ActiveScene);

        Assert.Equal(new Rgba32(255, 0, 0), renderer.Img[5, 5]);
    }

    [Fact]
    public void QueuedFadingScene_CrossfadesClockDuringTransition()
    {
        using var renderer = new SceneRenderer(img => img[6, 6] = new Rgba32(0, 255, 0));
        var playbackEngine = new ScenePlaybackEngine();
        var special = new FadingScene(new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[6, 6] = new Rgba32(255, 0, 0)
        });
        playbackEngine.Enqueue(special);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(500));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(500), playbackEngine.ActiveScene);

        var pixel = renderer.Img[6, 6];
        Assert.True(pixel.R > 0);
        Assert.True(pixel.G > 0);
    }

    [Fact]
    public void QueuedFadingScene_KeepsClockHiddenAtStartOfFadeOut_ThenCrossfadesItBackIn()
    {
        using var renderer = new SceneRenderer(img => img[7, 7] = new Rgba32(0, 255, 0));
        var playbackEngine = new ScenePlaybackEngine();
        var special = new FadingScene(new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[7, 7] = new Rgba32(255, 0, 0),
            OnElapsed = testScene => testScene.IsActive = false
        });
        playbackEngine.Enqueue(special);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(500));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(500), playbackEngine.ActiveScene);
        playbackEngine.Advance(TimeSpan.FromMilliseconds(600));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(600), playbackEngine.ActiveScene);

        Assert.Equal(new Rgba32(255, 0, 0), renderer.Img[7, 7]);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(500));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(500), playbackEngine.ActiveScene);

        var pixel = renderer.Img[7, 7];
        Assert.True(pixel.R > 0);
        Assert.True(pixel.G > 0);
    }

    [Fact]
    public void QueuedClockFadingScene_FadesClockOutBeforeActivatingInnerScene()
    {
        using var renderer = new SceneRenderer(img => img[8, 8] = new Rgba32(0, 255, 0));
        var playbackEngine = new ScenePlaybackEngine();
        var innerScene = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[8, 8] = new Rgba32(255, 0, 0)
        };
        playbackEngine.Enqueue(new ClockFadingScene(innerScene));

        playbackEngine.Advance(TimeSpan.FromMilliseconds(500));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(500), playbackEngine.ActiveScene);

        Assert.Equal(0, innerScene.ActivateCount);
        var fadedClockPixel = renderer.Img[8, 8];
        Assert.Equal(0, fadedClockPixel.R);
        Assert.True(fadedClockPixel.G > 0);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(600));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(600), playbackEngine.ActiveScene);

        Assert.Equal(1, innerScene.ActivateCount);
        Assert.Equal(new Rgba32(255, 0, 0), renderer.Img[8, 8]);
    }

    [Fact]
    public void QueuedClockFadingScene_FadesClockBackInAfterInnerSceneEnds()
    {
        using var renderer = new SceneRenderer(img => img[9, 9] = new Rgba32(0, 255, 0));
        var playbackEngine = new ScenePlaybackEngine();
        var innerScene = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[9, 9] = new Rgba32(255, 0, 0),
            OnElapsed = testScene => testScene.IsActive = false
        };
        playbackEngine.Enqueue(new ClockFadingScene(innerScene));

        playbackEngine.Advance(TimeSpan.FromMilliseconds(1000));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(1000), playbackEngine.ActiveScene);

        Assert.Equal(new Rgba32(255, 0, 0), renderer.Img[9, 9]);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(100));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(100), playbackEngine.ActiveScene);
        playbackEngine.Advance(TimeSpan.FromMilliseconds(500));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(500), playbackEngine.ActiveScene);

        var fadedClockPixel = renderer.Img[9, 9];
        Assert.Equal(0, fadedClockPixel.R);
        Assert.True(fadedClockPixel.G > 0);
    }

    [Fact]
    public void QueuedScene_NotDrawn_WhenItBecomesInactiveDuringElapsed()
    {
        using var renderer = new SceneRenderer();
        var playbackEngine = new ScenePlaybackEngine();
        var special = new TestSpecialScene
        {
            IsActive = true,
            OnElapsed = testScene => testScene.IsActive = false
        };
        playbackEngine.Enqueue(special);

        playbackEngine.Advance(TimeSpan.FromMilliseconds(20));
        renderer.AdvanceAndRender(TimeSpan.FromMilliseconds(20), playbackEngine.ActiveScene);

        Assert.Equal(1, special.ActivateCount);
        Assert.Equal(1, special.ElapsedCount);
        Assert.Equal(0, special.DrawCount);
    }

    private static SceneControlService CreateControlService(bool isTestMode, out ScenePlaybackEngine playbackEngine)
    {
        playbackEngine = new ScenePlaybackEngine();
        var scheduler = new SceneSelector(11, nextIndex: static _ => 0, imageSceneDirectory: CreateImageDirectory());
        return new SceneControlService(playbackEngine, scheduler, isTestMode);
    }

    private static string CreateImageDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"advent-orchestration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestSpecialScene : ISpecialScene
    {
        public int ActivateCount { get; private set; }
        public int ElapsedCount { get; private set; }
        public int DrawCount { get; private set; }

        public Action<TestSpecialScene>? OnElapsed { get; init; }
        public Action<Image<Rgba32>>? OnDraw { get; init; }

        public bool IsActive { get; set; }
        public bool HidesTime { get; set; }
        public bool RainbowSnow => false;
        public string Name => "Test Scene";

        public void Activate()
        {
            ActivateCount++;
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            ElapsedCount++;
            OnElapsed?.Invoke(this);
        }

        public void Draw(Image<Rgba32> img)
        {
            DrawCount++;
            OnDraw?.Invoke(img);
        }
    }

    private static void ForceRandomSceneTimer(SceneControlService control, TimeSpan value)
    {
        var coordinatorField = typeof(SceneControlService).GetField(
            "scheduleCoordinator",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(coordinatorField);

        var coordinator = coordinatorField!.GetValue(control);
        Assert.NotNull(coordinator);

        var timerField = coordinator.GetType().GetField(
            "timeToNextRandomScene",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(timerField);
        timerField!.SetValue(coordinator, value);
    }
}
