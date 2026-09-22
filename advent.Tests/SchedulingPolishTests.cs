using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public sealed class SchedulingPolishTests
{
    [Fact]
    public void BagCoversEveryReadySceneBeforeRepeatingAndAvoidsBoundaryRepeats()
    {
        var selector = new SceneSelector(new Catalog(), static count => count - 1);
        string? previous = null;
        for (var bag = 0; bag < 5; bag++)
        {
            var names = new List<string>();
            for (var pick = 0; pick < 3; pick++)
            {
                var next = selector.GetScene().Name;
                Assert.NotEqual(previous, next);
                names.Add(next);
                previous = next;
            }
            Assert.Equal(new[] { "A", "B", "C" }, names.Order());
        }
    }

    [Fact]
    public void BagRechecksReadinessAndManualSelectionDoesNotConsumeAnEntry()
    {
        var catalog = new Catalog();
        var selector = new SceneSelector(catalog, static _ => 0);
        Assert.Equal("A", selector.GetScene().Name);
        catalog.BReady = false;
        Assert.Equal("C", selector.GetScene().Name);
        catalog.BReady = true;
        Assert.True(selector.TryGetSceneByName("B", out _));
        Assert.Equal("B", selector.GetScene().Name);
        Assert.Equal("A", selector.GetNextSceneNameInCycle());
        Assert.Equal("B", selector.GetNextSceneNameInCycle());
    }

    [Fact]
    public void ErrorIsManualAndExperimentalScenesStayOutOfRotation()
    {
        var registrations = new BuiltinSceneModule().RegisterScenes(new SceneModuleContext(9, "missing", null, null, null, false)).ToArray();
        Assert.False(registrations.Single(r => r.Name == "Error").IncludedInCycle);
        Assert.NotNull(registrations.Single(r => r.Name == "Error").CreateScene);
        Assert.DoesNotContain(registrations, r => r.Name is "WarpCore" or "Game of Life" or "Matrix Rain" or "Plasma SDF");
    }

    [Fact]
    public void AutomaticPlaybackWaitsForLongSceneAndClockGapWithoutDiscardingManualRequests()
    {
        var engine = new ScenePlaybackEngine();
        var coordinator = new SceneScheduleCoordinator(new SceneSelector(new Catalog(), static _ => 0), false);
        var rail = new TestScene("Long rail");
        var manual = new TestScene("Manual");
        engine.Enqueue(rail);
        engine.Advance(TimeSpan.Zero);
        Due(coordinator);
        coordinator.Advance(TimeSpan.FromSeconds(130), engine);
        Assert.Equal(0, engine.QueueLength);
        engine.Enqueue(manual);
        coordinator.Advance(TimeSpan.FromSeconds(30), engine);
        Assert.Equal(1, engine.QueueLength);
        rail.IsActive = false;
        engine.Advance(TimeSpan.Zero);
        coordinator.Advance(TimeSpan.Zero, engine);
        engine.Advance(TimeSpan.Zero);
        Assert.Same(manual, engine.ActiveScene);
        coordinator.Advance(TimeSpan.FromSeconds(2), engine);
        manual.IsActive = false;
        engine.Advance(TimeSpan.Zero);
        coordinator.Advance(TimeSpan.Zero, engine);
        coordinator.Advance(TimeSpan.FromSeconds(9.99), engine);
        Assert.Equal(0, engine.QueueLength);
        coordinator.Advance(TimeSpan.FromMilliseconds(10), engine);
        Assert.Equal(1, engine.QueueLength);
    }

    [Fact]
    public void ManualNextAndTestModeBypassAutomaticClockPause()
    {
        var engine = new ScenePlaybackEngine();
        var coordinator = new SceneScheduleCoordinator(new SceneSelector(new Catalog(), static _ => 0), false);
        coordinator.EnqueueNextScene(engine);
        Assert.Equal(1, engine.QueueLength);
        engine.ClearQueue();
        coordinator.SetMode(true);
        coordinator.Advance(TimeSpan.Zero, engine);
        Assert.Equal(1, engine.QueueLength);
        coordinator.Advance(TimeSpan.Zero, engine);
        Assert.Equal(1, engine.QueueLength);
    }

    [Fact]
    public void AutomaticRateLimitStillAppliesToVeryShortVisits()
    {
        var engine = new ScenePlaybackEngine();
        var coordinator = new SceneScheduleCoordinator(new SceneSelector(new Catalog(), static _ => 0), false);
        for (var i = 0; i < 2; i++)
        {
            Due(coordinator);
            coordinator.Advance(TimeSpan.FromSeconds(10), engine);
            Assert.Equal(1, engine.QueueLength);
            engine.ClearQueue();
        }
        Due(coordinator);
        coordinator.Advance(TimeSpan.FromSeconds(10), engine);
        Assert.Equal(0, engine.QueueLength);
        coordinator.Advance(TimeSpan.FromSeconds(41), engine);
        Assert.Equal(1, engine.QueueLength);
    }

    private static void Due(SceneScheduleCoordinator coordinator) => typeof(SceneScheduleCoordinator)
        .GetField("timeToNextRandomScene", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(coordinator, TimeSpan.Zero);

    private sealed class TestScene(string name) : ISpecialScene
    {
        public string Name => name;
        public bool IsActive { get; set; }
        public bool HidesTime => true;
        public bool RainbowSnow => false;
        public void Activate() => IsActive = true;
        public void Elapsed(TimeSpan timeSpan) { }
        public void Draw(Image<Rgba32> image) { }
    }

    private sealed class Catalog : ISceneCatalog
    {
        public bool BReady = true;
        public IReadOnlyList<string> AvailableSceneNames => ["A", "B", "C"];
        public IReadOnlyList<string> AllSceneNames => AvailableSceneNames;
        public IReadOnlyList<string> KnownSceneNames => AvailableSceneNames;
        public IReadOnlyList<SceneCatalogEntry> CycleEntries =>
        [new("A", () => new TestScene("A"), () => true),
         new("B", () => new TestScene("B"), () => BReady),
         new("C", () => new TestScene("C"), () => true)];
        public SceneSelection SelectSceneByName(string? name) =>
            new(SceneSelectionStatus.Ready, new TestScene(name!));
    }
}
