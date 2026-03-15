using Xunit;

namespace advent.Tests;

public class SceneSelectorTests
{
    [Fact]
    public void Constructor_ThrowsForMonthOutsideCalendarRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneSelector(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SceneSelector(13));
    }

    [Fact]
    public void Constructor_UsesChristmasScenes_InDecember()
    {
        var sut = new SceneSelector(12);

        Assert.Equal(8, sut.AvailableSceneNames.Count);
        Assert.Equal("Santa", sut.AvailableSceneNames[0]);
        Assert.All(sut.AvailableSceneNames.Skip(1), name => Assert.Equal("Animated GIF", name));
    }

    [Fact]
    public void Constructor_UsesDefaultScenes_OutsideDecember()
    {
        var sut = new SceneSelector(11);

        var expected = new[]
        {
            "Weather",
            "Cat",
            "Rainbow",
            "Game of Life",
            "Starfield Parallax",
            "Plasma SDF",
            "Matrix Rain",
            "Synthwave Grid",
            "Orbital",
            "Error",
            "Space Invaders",
            "CTM Logo",
            "CTM Banner"
        };

        Assert.Equal(expected, sut.AvailableSceneNames);
    }

    [Fact]
    public void GetScene_ThrowsWhenIndexProviderReturnsOutOfRange()
    {
        var sut = new SceneSelector(11, _ => 99);

        Assert.Throws<InvalidOperationException>(() => sut.GetScene());
    }

    [Fact]
    public void AllSceneNames_ContainsDefaultAndChristmasScenes()
    {
        var sut = new SceneSelector(11);

        Assert.Equal(21, sut.AllSceneNames.Count);
        Assert.Contains("Game of Life", sut.AllSceneNames);
        Assert.Contains("Starfield Parallax", sut.AllSceneNames);
        Assert.Contains("Plasma SDF", sut.AllSceneNames);
        Assert.Contains("Matrix Rain", sut.AllSceneNames);
        Assert.Contains("Weather", sut.AllSceneNames);
        Assert.Contains("Synthwave Grid", sut.AllSceneNames);
        Assert.Contains("Orbital", sut.AllSceneNames);
        Assert.Contains("Santa", sut.AllSceneNames);
        Assert.Equal(7, sut.AllSceneNames.Count(name => name == "Animated GIF"));
    }

    [Fact]
    public void GetNextSceneNameInCycle_ReturnsAllScenesInOrder_ThenWraps()
    {
        var sut = new SceneSelector(11);

        var cycled = new List<string>();
        for (var i = 0; i < sut.AllSceneNames.Count + 2; i++) cycled.Add(sut.GetNextSceneNameInCycle());

        Assert.Equal(sut.AllSceneNames, cycled.Take(sut.AllSceneNames.Count));
        Assert.Equal(sut.AllSceneNames[0], cycled[^2]);
        Assert.Equal(sut.AllSceneNames[1], cycled[^1]);
    }
}