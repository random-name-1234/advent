using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Reflection;
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
    public void Constructor_LoadsRootAndMonthImageScenes_InDecember()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "always-logo.png"), 32, 32);
            CreateGif(Path.Combine(imageDirectory, "always-gif.gif"));
            Directory.CreateDirectory(Path.Combine(imageDirectory, "12"));
            CreateGif(Path.Combine(imageDirectory, "12", "december-gif.gif"));

            var sut = new SceneSelector(12, imageSceneDirectory: imageDirectory);

            Assert.Contains("Santa", sut.AvailableSceneNames);
            Assert.Contains("always-logo", sut.AvailableSceneNames);
            Assert.Contains("always-gif", sut.AvailableSceneNames);
            Assert.Contains("december-gif", sut.AvailableSceneNames);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_LoadsRootImageScenes_OutsideDecember()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "always-logo.png"), 32, 32);
            Directory.CreateDirectory(Path.Combine(imageDirectory, "12"));
            CreateGif(Path.Combine(imageDirectory, "12", "december-gif.gif"));

            var sut = new SceneSelector(11, imageSceneDirectory: imageDirectory);

            Assert.Contains("always-logo", sut.AvailableSceneNames);
            Assert.DoesNotContain("december-gif", sut.AvailableSceneNames);
            Assert.DoesNotContain("Santa", sut.AvailableSceneNames);
            Assert.Equal(14, sut.AvailableSceneNames.Count);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_MapsWideStaticImagesToScrollingScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "wide_banner.png"), 180, 32);
            var sut = new SceneSelector(11, count => count - 1, imageSceneDirectory: imageDirectory);

            var scene = sut.GetScene();
            var mainScene = UnwrapMainScene(scene);

            Assert.IsType<ScrollingImageScene>(mainScene);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_MapsSquareStaticImagesToStaticScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "logo_square.png"), 32, 32);
            var sut = new SceneSelector(11, count => count - 1, imageSceneDirectory: imageDirectory);

            var scene = sut.GetScene();
            var mainScene = UnwrapMainScene(scene);

            Assert.IsType<StaticImageScene>(mainScene);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_MapsGifImagesToAnimatedGifScene()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreateGif(Path.Combine(imageDirectory, "animated_name.gif"));
            var sut = new SceneSelector(11, count => count - 1, imageSceneDirectory: imageDirectory);

            var scene = sut.GetScene();
            var mainScene = UnwrapMainScene(scene);

            Assert.IsType<AnimatedGifScene>(mainScene);
            Assert.Equal("animated name", mainScene.Name);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_SkipsInvalidImageFiles()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            File.WriteAllText(Path.Combine(imageDirectory, "broken.png"), "not-a-real-image");
            var sut = new SceneSelector(11, imageSceneDirectory: imageDirectory);

            Assert.DoesNotContain("broken", sut.AvailableSceneNames);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_AppliesManifestOverrides_ForTypeNameDurationAndMonth()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "logo.png"), 32, 32);
            WriteManifest(imageDirectory, """
                {
                  "images": [
                    {
                      "file": "logo.png",
                      "name": "Custom Logo",
                      "type": "scroll",
                      "months": [11],
                      "durationSeconds": 0.1
                    }
                  ]
                }
                """);

            var sut = new SceneSelector(11, count => count - 1, imageSceneDirectory: imageDirectory);
            Assert.Contains("Custom Logo", sut.AvailableSceneNames);

            var scene = sut.GetScene();
            var mainScene = UnwrapMainScene(scene);
            var scrolling = Assert.IsType<ScrollingImageScene>(mainScene);
            Assert.Equal("Custom Logo", scrolling.Name);

            scrolling.Activate();
            scrolling.Elapsed(TimeSpan.FromMilliseconds(150));
            Assert.False(scrolling.IsActive);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_RespectsManifestMonthFilter_ForRootImages()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "seasonal-logo.png"), 32, 32);
            WriteManifest(imageDirectory, """
                {
                  "images": [
                    {
                      "file": "seasonal-logo.png",
                      "months": [12]
                    }
                  ]
                }
                """);

            var sut = new SceneSelector(11, imageSceneDirectory: imageDirectory);

            Assert.DoesNotContain("seasonal-logo", sut.AvailableSceneNames);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void Constructor_LoadsManifestOnlyNestedImages_WhenConfigured()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var nestedDirectory = Path.Combine(imageDirectory, "special");
            Directory.CreateDirectory(nestedDirectory);
            CreateGif(Path.Combine(nestedDirectory, "extra.gif"));

            WriteManifest(imageDirectory, """
                {
                  "images": [
                    {
                      "file": "special/extra.gif",
                      "name": "Manifest Extra",
                      "type": "animated",
                      "months": [11]
                    }
                  ]
                }
                """);

            var sut = new SceneSelector(11, count => count - 1, imageSceneDirectory: imageDirectory);
            Assert.Contains("Manifest Extra", sut.AvailableSceneNames);

            var scene = sut.GetScene();
            var mainScene = UnwrapMainScene(scene);
            Assert.IsType<AnimatedGifScene>(mainScene);
            Assert.Equal("Manifest Extra", mainScene.Name);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void GetScene_ThrowsWhenIndexProviderReturnsOutOfRange()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var sut = new SceneSelector(11, _ => 99, imageSceneDirectory: imageDirectory);
            Assert.Throws<InvalidOperationException>(() => sut.GetScene());
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void TryGetSceneByName_ReturnsScene_ForKnownName()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "always-logo.png"), 32, 32);
            var sut = new SceneSelector(11, imageSceneDirectory: imageDirectory);

            var found = sut.TryGetSceneByName("Fireworks", out var scene);

            Assert.True(found);
            Assert.NotNull(scene);
            Assert.Equal("Fireworks", scene.Name);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void TryGetSceneByName_ReturnsFalse_ForUnknownName()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            var sut = new SceneSelector(11, imageSceneDirectory: imageDirectory);
            var found = sut.TryGetSceneByName("does-not-exist", out _);
            Assert.False(found);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void AllSceneNames_ContainsLoadedScenes()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "always-logo.png"), 32, 32);
            var sut = new SceneSelector(11, imageSceneDirectory: imageDirectory);

            Assert.Equal(sut.AvailableSceneNames, sut.AllSceneNames);
            Assert.Contains("Game of Life", sut.AllSceneNames);
            Assert.Contains("Starfield Parallax", sut.AllSceneNames);
            Assert.Contains("Plasma SDF", sut.AllSceneNames);
            Assert.Contains("Metaballs", sut.AllSceneNames);
            Assert.Contains("Matrix Rain", sut.AllSceneNames);
            Assert.Contains("Weather", sut.AllSceneNames);
            Assert.Contains("Synthwave Grid", sut.AllSceneNames);
            Assert.Contains("Orbital", sut.AllSceneNames);
            Assert.Contains("Fireworks", sut.AllSceneNames);
            Assert.Contains("always-logo", sut.AllSceneNames);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    [Fact]
    public void GetNextSceneNameInCycle_ReturnsAllScenesInOrder_ThenWraps()
    {
        var imageDirectory = CreateImageDirectory();
        try
        {
            CreatePng(Path.Combine(imageDirectory, "always-logo.png"), 32, 32);
            var sut = new SceneSelector(11, imageSceneDirectory: imageDirectory);

            var cycled = new List<string>();
            for (var i = 0; i < sut.AllSceneNames.Count + 2; i++) cycled.Add(sut.GetNextSceneNameInCycle());

            Assert.Equal(sut.AllSceneNames, cycled.Take(sut.AllSceneNames.Count));
            Assert.Equal(sut.AllSceneNames[0], cycled[^2]);
            Assert.Equal(sut.AllSceneNames[1], cycled[^1]);
        }
        finally
        {
            Directory.Delete(imageDirectory, true);
        }
    }

    private static string CreateImageDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"advent-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void CreatePng(string filePath, int width, int height)
    {
        using var image = new Image<Rgba32>(width, height);
        image.Save(filePath);
    }

    private static void CreateGif(string filePath)
    {
        using var image = new Image<Rgba32>(4, 4);
        image.SaveAsGif(filePath);
    }

    private static void WriteManifest(string imageDirectory, string content)
    {
        File.WriteAllText(Path.Combine(imageDirectory, "manifest.json"), content);
    }

    private static ISpecialScene UnwrapMainScene(ISpecialScene scene)
    {
        var fadingScene = Assert.IsType<FadingScene>(scene);
        var field = typeof(FadingScene).GetField("mainScene", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<ISpecialScene>(field!.GetValue(fadingScene));
    }
}
