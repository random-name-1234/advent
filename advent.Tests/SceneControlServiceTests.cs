using Xunit;

namespace advent.Tests;

public class SceneControlServiceTests
{
    [Fact]
    public void EnqueueSceneByName_QueuesRequestedScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            var queued = control.EnqueueSceneByName("Fireworks", out var error);

            Assert.True(queued);
            Assert.True(string.IsNullOrEmpty(error));
            Assert.Equal(1, playbackEngine.QueueLength);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void SetMode_ToTest_EnablesContinuousRequests_AndQueuesScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            var changed = control.SetMode(testMode: true);
            var status = control.GetStatus();

            Assert.True(changed);
            Assert.Equal("test", status.Mode);
            Assert.True(status.ContinuousSceneRequests);
            Assert.Equal(1, playbackEngine.QueueLength);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void EnqueueSceneByName_ReturnsFalse_ForUnknownScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            var queued = control.EnqueueSceneByName("nope", out var error);

            Assert.False(queued);
            Assert.Contains("Unknown scene", error);
            Assert.Equal(0, playbackEngine.QueueLength);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void EnqueueSceneByName_ReturnsFalse_ForUnavailableScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            var queued = control.EnqueueSceneByName("Weather", out var error);

            Assert.False(queued);
            Assert.Contains("not ready yet", error);
            Assert.Equal(0, playbackEngine.QueueLength);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void EnqueueMessage_QueuesMessageScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            var queued = control.EnqueueMessage("Hello house!", TimeSpan.FromSeconds(8), out var error);

            Assert.True(queued);
            Assert.True(string.IsNullOrEmpty(error));
            Assert.True(playbackEngine.TryPeekQueuedScene(out var queuedScene));
            var innerScene = UnwrapTimedScene(queuedScene);
            Assert.IsType<MessageScene>(innerScene);
            Assert.Equal("Message: Hello house!", queuedScene.Name);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void EnqueueMessage_ReturnsFalse_ForInvalidInputs()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            Assert.False(control.EnqueueMessage("", null, out _));
            Assert.False(control.EnqueueMessage("ok", TimeSpan.FromSeconds(0), out _));
            Assert.False(control.EnqueueMessage("ok", TimeSpan.FromSeconds(21), out _));
            Assert.Equal(0, playbackEngine.QueueLength);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void GetStatus_ReportsActiveSceneAfterAdvance()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            Assert.True(control.EnqueueSceneByName("Fireworks", out _));

            control.Advance(TimeSpan.FromMilliseconds(10));
            var status = control.GetStatus();

            Assert.True(status.HasActiveScene);
            Assert.Equal("Fireworks", status.ActiveSceneName);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void GetSceneCatalog_ReportsSceneAvailabilityAndCycleMembership()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var playbackEngine = new ScenePlaybackEngine();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(playbackEngine, selector, isTestMode: false);

            var catalog = control.GetSceneCatalog();
            var weather = Assert.Single(catalog.Items, item => item.Name == "Weather");
            var lab = Assert.Single(catalog.Items, item => item.Name == "Legibility Lab");

            Assert.False(weather.Available);
            Assert.True(weather.IncludedInCycle);
            Assert.True(lab.Available);
            Assert.False(lab.IncludedInCycle);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    private static ISpecialScene UnwrapTimedScene(ISpecialScene scene)
    {
        if (scene is FadingScene)
        {
            var fadingField = typeof(FadingScene).GetField("mainScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(fadingField);
            scene = Assert.IsAssignableFrom<ISpecialScene>(fadingField!.GetValue(scene));
        }
        else if (scene is ClockFadingScene)
        {
            var clockField = typeof(ClockFadingScene).GetField("mainScene",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(clockField);
            scene = Assert.IsAssignableFrom<ISpecialScene>(clockField!.GetValue(scene));
        }

        if (scene.GetType().Name != "TimedScene")
            return scene;

        var field = scene.GetType().GetField("innerScene",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<ISpecialScene>(field!.GetValue(scene));
    }

    private static string CreateImageDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"advent-scene-control-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
