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
            var scene = new Scene();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(scene, selector, isTestMode: false);

            var queued = control.EnqueueSceneByName("Fireworks", out var error);

            Assert.True(queued);
            Assert.True(string.IsNullOrEmpty(error));
            Assert.Single(scene.SpecialScenes);
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
            var scene = new Scene();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(scene, selector, isTestMode: false);

            var changed = control.SetMode(testMode: true);
            var status = control.GetStatus();

            Assert.True(changed);
            Assert.True(scene.ContinuousSceneRequests);
            Assert.Equal("test", status.Mode);
            Assert.True(status.ContinuousSceneRequests);
            Assert.Single(scene.SpecialScenes);
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
            var scene = new Scene();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(scene, selector, isTestMode: false);

            var queued = control.EnqueueSceneByName("nope", out var error);

            Assert.False(queued);
            Assert.Contains("Unknown scene", error);
            Assert.Empty(scene.SpecialScenes);
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
            var scene = new Scene();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(scene, selector, isTestMode: false);

            var queued = control.EnqueueMessage("Hello house!", TimeSpan.FromSeconds(8), out var error);

            Assert.True(queued);
            Assert.True(string.IsNullOrEmpty(error));
            Assert.True(scene.SpecialScenes.TryPeek(out var queuedScene));
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
            var scene = new Scene();
            var selector = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var control = new SceneControlService(scene, selector, isTestMode: false);

            Assert.False(control.EnqueueMessage("", null, out _));
            Assert.False(control.EnqueueMessage("ok", TimeSpan.FromSeconds(0), out _));
            Assert.False(control.EnqueueMessage("ok", TimeSpan.FromSeconds(21), out _));
            Assert.Empty(scene.SpecialScenes);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    private static ISpecialScene UnwrapTimedScene(ISpecialScene scene)
    {
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
