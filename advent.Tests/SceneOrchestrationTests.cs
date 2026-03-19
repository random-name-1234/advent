using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Reflection;
using Xunit;

namespace advent.Tests;

public class SceneOrchestrationTests
{
    [Fact]
    public void ContinuousMode_RaisesSingleRequest_UntilPendingClears()
    {
        var scene = new Scene
        {
            ContinuousSceneRequests = true
        };

        var requestCount = 0;
        scene.NewSceneWanted += (_, _) => requestCount++;

        scene.Elapsed(TimeSpan.FromMilliseconds(10));
        scene.Elapsed(TimeSpan.FromMilliseconds(10));

        Assert.Equal(1, requestCount);
    }

    [Fact]
    public void ContinuousMode_QueuedSceneClearsPending_AndRequestsAgain()
    {
        var scene = new Scene
        {
            ContinuousSceneRequests = true
        };

        var requestCount = 0;
        scene.NewSceneWanted += (_, _) => requestCount++;

        scene.Elapsed(TimeSpan.FromMilliseconds(10));

        var special = new TestSpecialScene
        {
            IsActive = true,
            OnElapsed = testScene => testScene.IsActive = false
        };
        scene.SpecialScenes.Enqueue(special);

        scene.Elapsed(TimeSpan.FromMilliseconds(10));

        Assert.Equal(1, special.ActivateCount);
        Assert.Equal(1, special.ElapsedCount);
        Assert.Equal(2, requestCount);
    }

    [Fact]
    public void NormalMode_RaisesRequest_WhenRandomTimerExpires()
    {
        var scene = new Scene();
        var requestCount = 0;
        scene.NewSceneWanted += (_, _) => requestCount++;

        scene.Elapsed(TimeSpan.FromMinutes(2));

        Assert.Equal(1, requestCount);
    }

    [Fact]
    public void NormalMode_DoesNotRaiseMoreThanTwoRequestsWithinOneMinute()
    {
        var scene = new Scene();
        var requestCount = 0;
        scene.NewSceneWanted += (_, _) => requestCount++;

        ForceRandomSceneTimer(scene, TimeSpan.Zero);
        scene.Elapsed(TimeSpan.FromMilliseconds(10));
        ForceRandomSceneTimer(scene, TimeSpan.Zero);
        scene.Elapsed(TimeSpan.FromMilliseconds(10));
        ForceRandomSceneTimer(scene, TimeSpan.Zero);
        scene.Elapsed(TimeSpan.FromMilliseconds(10));

        Assert.Equal(2, requestCount);
    }

    [Fact]
    public void QueuedScene_IsActivatedElapsedAndDrawn_WhenStillActive()
    {
        var scene = new Scene();
        var special = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[3, 3] = new Rgba32(255, 0, 0)
        };
        scene.SpecialScenes.Enqueue(special);

        scene.Elapsed(TimeSpan.FromMilliseconds(20));

        Assert.Equal(1, special.ActivateCount);
        Assert.Equal(1, special.ElapsedCount);
        Assert.Equal(1, special.DrawCount);
        Assert.Equal(255, scene.Img[3, 3].R);
    }

    [Fact]
    public void QueuedScene_DrawsClockOverlayAfterScene_WhenSceneDoesNotHideTime()
    {
        var scene = new Scene(img => img[4, 4] = new Rgba32(0, 255, 0));
        var special = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = false,
            OnDraw = img => img[4, 4] = new Rgba32(255, 0, 0)
        };
        scene.SpecialScenes.Enqueue(special);

        scene.Elapsed(TimeSpan.FromMilliseconds(20));

        Assert.Equal(new Rgba32(0, 255, 0), scene.Img[4, 4]);
    }

    [Fact]
    public void QueuedScene_SuppressesClockOverlay_WhenSceneHidesTime()
    {
        var scene = new Scene(img => img[5, 5] = new Rgba32(0, 255, 0));
        var special = new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[5, 5] = new Rgba32(255, 0, 0)
        };
        scene.SpecialScenes.Enqueue(special);

        scene.Elapsed(TimeSpan.FromMilliseconds(20));

        Assert.Equal(new Rgba32(255, 0, 0), scene.Img[5, 5]);
    }

    [Fact]
    public void QueuedFadingScene_CrossfadesClockDuringTransition()
    {
        var scene = new Scene(img => img[6, 6] = new Rgba32(0, 255, 0));
        var special = new FadingScene(new TestSpecialScene
        {
            IsActive = true,
            HidesTime = true,
            OnDraw = img => img[6, 6] = new Rgba32(255, 0, 0)
        });
        scene.SpecialScenes.Enqueue(special);

        scene.Elapsed(TimeSpan.FromMilliseconds(500));

        var pixel = scene.Img[6, 6];
        Assert.True(pixel.R > 0);
        Assert.True(pixel.G > 0);
    }

    [Fact]
    public void QueuedScene_NotDrawn_WhenItBecomesInactiveDuringElapsed()
    {
        var scene = new Scene();
        var special = new TestSpecialScene
        {
            IsActive = true,
            OnElapsed = testScene => testScene.IsActive = false
        };
        scene.SpecialScenes.Enqueue(special);

        scene.Elapsed(TimeSpan.FromMilliseconds(20));

        Assert.Equal(1, special.ActivateCount);
        Assert.Equal(1, special.ElapsedCount);
        Assert.Equal(0, special.DrawCount);
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

    private static void ForceRandomSceneTimer(Scene scene, TimeSpan value)
    {
        var field = typeof(Scene).GetField("timeToNextRandomScene", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(scene, value);
    }
}
