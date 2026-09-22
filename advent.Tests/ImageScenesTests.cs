using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class ImageScenesTests
{
    [Theory]
    [InlineData(25, 0, 255, 0)]
    [InlineData(100, 0, 0, 255)]
    [InlineData(1000, 0, 255, 0)]
    public void GifCatchesUpAcrossVariableAndZeroDelays(int milliseconds, byte red, byte green, byte blue)
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "timing.gif");
            using (var animation = new Image<Rgba32>(64, 32, new Rgba32(255, 0, 0)))
            {
                animation.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 0;
                using var second = new Image<Rgba32>(64, 32, new Rgba32(0, 255, 0));
                second.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 3;
                animation.Frames.AddFrame(second.Frames.RootFrame);
                using var third = new Image<Rgba32>(64, 32, new Rgba32(0, 0, 255));
                third.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 7;
                animation.Frames.AddFrame(third.Frames.RootFrame);
                animation.SaveAsGif(path);
            }
            var scene = new AnimatedGifScene(path);
            scene.Activate();
            scene.Elapsed(TimeSpan.FromMilliseconds(milliseconds));
            using var actual = new Image<Rgba32>(64, 32);
            scene.Draw(actual);
            Assert.Equal(new Rgba32(red, green, blue), actual[10, 10]);
            scene.Activate();
            for (var tick = 0; tick < milliseconds; tick++) scene.Elapsed(TimeSpan.FromMilliseconds(1));
            using var stepped = new Image<Rgba32>(64, 32);
            scene.Draw(stepped);
            Assert.Equal(actual[10, 10], stepped[10, 10]);
        }
        finally { Directory.Delete(directory, true); }
    }

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

    [Fact]
    public void ScrollingImageScene_DefaultDuration_CompletesByTwentySeconds()
    {
        var tempDirectory = CreateTempDirectory();
        var imagePath = Path.Combine(tempDirectory, "very-wide-banner.png");

        try
        {
            using (var image = new Image<Rgba32>(640, 32))
                image.Save(imagePath);

            var scene = new ScrollingImageScene(imagePath, "Wide");
            scene.Activate();

            scene.Elapsed(TimeSpan.FromSeconds(19));
            Assert.True(scene.IsActive);

            scene.Elapsed(TimeSpan.FromSeconds(2));
            Assert.False(scene.IsActive);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact]
    public void ScrollingImageScene_ExplicitLongDuration_IsClampedToTwentySeconds()
    {
        var tempDirectory = CreateTempDirectory();
        var imagePath = Path.Combine(tempDirectory, "banner.png");

        try
        {
            using (var image = new Image<Rgba32>(180, 32))
                image.Save(imagePath);

            var scene = new ScrollingImageScene(imagePath, "Banner", TimeSpan.FromSeconds(45));
            scene.Activate();

            scene.Elapsed(TimeSpan.FromSeconds(19));
            Assert.True(scene.IsActive);

            scene.Elapsed(TimeSpan.FromSeconds(2));
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
