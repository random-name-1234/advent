using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class ImageScenesTests
{
    [Fact]
    public void StaticImageScene_Activates_Draws_AndExpires()
    {
        var tempDirectory = CreateTempDirectory();
        var imagePath = Path.Combine(tempDirectory, "logo.png");

        try
        {
            using (var image = new Image<Rgba32>(32, 32))
                image.Save(imagePath);

            var scene = new StaticImageScene(imagePath, "Logo");
            scene.Activate();

            Assert.True(scene.IsActive);
            Assert.Equal("Logo", scene.Name);

            using var canvas = new Image<Rgba32>(64, 32);
            scene.Elapsed(TimeSpan.FromMilliseconds(200));
            scene.Draw(canvas);

            scene.Elapsed(TimeSpan.FromSeconds(30));
            Assert.False(scene.IsActive);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void ScrollingImageScene_Activates_Draws_AndExpires()
    {
        var tempDirectory = CreateTempDirectory();
        var imagePath = Path.Combine(tempDirectory, "banner.png");

        try
        {
            using (var image = new Image<Rgba32>(180, 32))
                image.Save(imagePath);

            var scene = new ScrollingImageScene(imagePath, "Banner");
            scene.Activate();

            Assert.True(scene.IsActive);
            Assert.Equal("Banner", scene.Name);

            using var canvas = new Image<Rgba32>(64, 32);
            scene.Elapsed(TimeSpan.FromMilliseconds(200));
            scene.Draw(canvas);

            scene.Elapsed(TimeSpan.FromSeconds(30));
            Assert.False(scene.IsActive);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void AnimatedGifScene_UsesProvidedName()
    {
        var tempDirectory = CreateTempDirectory();
        var imagePath = Path.Combine(tempDirectory, "anim.gif");

        try
        {
            using (var image = new Image<Rgba32>(4, 4))
                image.SaveAsGif(imagePath);

            var scene = new AnimatedGifScene(imagePath, "Holiday");
            Assert.Equal("Holiday", scene.Name);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void StaticImageScene_UsesCustomDurationOverride()
    {
        var tempDirectory = CreateTempDirectory();
        var imagePath = Path.Combine(tempDirectory, "logo.png");

        try
        {
            using (var image = new Image<Rgba32>(32, 32))
                image.Save(imagePath);

            var scene = new StaticImageScene(imagePath, "Logo", TimeSpan.FromMilliseconds(100));
            scene.Activate();
            scene.Elapsed(TimeSpan.FromMilliseconds(150));

            Assert.False(scene.IsActive);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void AnimatedGifScene_UsesCustomDurationOverride()
    {
        var tempDirectory = CreateTempDirectory();
        var imagePath = Path.Combine(tempDirectory, "anim.gif");

        try
        {
            using (var image = new Image<Rgba32>(4, 4))
                image.SaveAsGif(imagePath);

            var scene = new AnimatedGifScene(imagePath, "Holiday", TimeSpan.FromMilliseconds(100));
            scene.Activate();
            scene.Elapsed(TimeSpan.FromMilliseconds(150));

            Assert.False(scene.IsActive);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"advent-image-scenes-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }
}
