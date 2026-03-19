using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace advent.Tests;

public class SceneLifecycleTests
{
    [Fact]
    public void RainbowSnowScene_ActivatesAndExpires()
    {
        var scene = new RainbowSnowScene();
        scene.Activate();

        Assert.True(scene.IsActive);
        Assert.True(scene.RainbowSnow);
        Assert.False(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromSeconds(11));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Fact]
    public void FadeInOutScene_DrawsAndExpires()
    {
        var scene = new FadeInOutScene(Fade.Out);
        scene.Activate();

        using var canvas = new Image<Rgba32>(64, 32);
        canvas.Mutate(x => x.Fill(Color.White));

        scene.Elapsed(TimeSpan.FromMilliseconds(500));
        scene.Draw(canvas);

        Assert.True(scene.IsActive);
        Assert.True(canvas[10, 10].R < 255);

        scene.Elapsed(TimeSpan.FromMilliseconds(600));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Fact]
    public void FadingScene_RunsFadeOutMainAndFadeIn()
    {
        var main = new OneTickScene("Main", hidesTime: true);
        var scene = new FadingScene(main);
        scene.Activate();

        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);
        Assert.Equal("Main", scene.Name);

        scene.Elapsed(TimeSpan.FromMilliseconds(500));
        Assert.Equal(0, main.ElapsedCount);

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);
        Assert.Equal(1, main.DrawCount);
        Assert.True(canvas[0, 0].R > 0);

        scene.Elapsed(TimeSpan.FromMilliseconds(500));
        Assert.Equal(0, main.ElapsedCount);

        scene.Elapsed(TimeSpan.FromMilliseconds(10));
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);
        Assert.Equal(1, main.ElapsedCount);

        scene.Elapsed(TimeSpan.FromMilliseconds(900));
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromMilliseconds(200));
        Assert.False(scene.IsActive);
    }

    [Fact]
    public void CatScene_ActivatesDrawsAndExpires()
    {
        RunInProjectRoot(() =>
        {
            var scene = new CatScene();
            scene.Activate();

            scene.Elapsed(TimeSpan.FromMilliseconds(500));
            using var canvas = new Image<Rgba32>(64, 32);
            scene.Draw(canvas);

            Assert.True(scene.IsActive);

            scene.Elapsed(TimeSpan.FromSeconds(9));
            Assert.False(scene.IsActive);
        });
    }

    [Fact]
    public void ErrorScene_ActivatesDrawsAndExpires()
    {
        RunInProjectRoot(() =>
        {
            var scene = new ErrorScene();
            scene.Activate();

            Assert.True(scene.IsActive);
            Assert.True(scene.HidesTime);

            scene.Elapsed(TimeSpan.FromSeconds(4));
            using var canvas = new Image<Rgba32>(64, 32);
            scene.Draw(canvas);

            scene.Elapsed(TimeSpan.FromSeconds(4));
            Assert.False(scene.IsActive);
            Assert.False(scene.HidesTime);
        });
    }

    [Fact]
    public void SantaScene_ActivatesDrawsAndExpires()
    {
        RunInProjectRoot(() =>
        {
            var scene = new SantaScene();
            scene.Activate();

            Assert.True(scene.IsActive);
            Assert.True(scene.HidesTime);

            scene.Elapsed(TimeSpan.FromSeconds(1));
            using var canvas = new Image<Rgba32>(64, 32);
            scene.Draw(canvas);

            scene.Elapsed(TimeSpan.FromSeconds(8));
            Assert.False(scene.IsActive);
            Assert.False(scene.HidesTime);
        });
    }

    [Fact]
    public void SpaceInvadersScene_ActivatesDrawsAndExpires()
    {
        var scene = new SpaceInvadersScene();
        scene.Activate();

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Elapsed(TimeSpan.FromMilliseconds(250));
        scene.Draw(canvas);

        Assert.True(scene.IsActive);

        for (var i = 0; i < 240 && scene.IsActive; i++)
        {
            scene.Elapsed(TimeSpan.FromMilliseconds(100));
            scene.Draw(canvas);
        }

        Assert.False(scene.IsActive);
    }

    [Fact]
    public void WeatherScene_ActivatesDrawsLoadingStateAndExpires()
    {
        var scene = new WeatherScene();
        scene.Activate();

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Elapsed(TimeSpan.FromMilliseconds(250));
        scene.Draw(canvas);

        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromSeconds(21));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    [Fact]
    public void LegibilityLabScene_ActivatesDrawsAndExpires()
    {
        var scene = new LegibilityLabScene();
        scene.Activate();

        using var canvas = new Image<Rgba32>(64, 32);
        scene.Elapsed(TimeSpan.FromMilliseconds(250));
        scene.Draw(canvas);

        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromSeconds(61));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    private static void RunInProjectRoot(Action testAction)
    {
        var originalDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(ResolveProjectRoot());
        try
        {
            testAction();
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private static string ResolveProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "assets")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate project root with an assets directory.");
    }

    private sealed class OneTickScene(string name, bool hidesTime) : ISpecialScene
    {
        public int DrawCount { get; private set; }
        public int ElapsedCount { get; private set; }

        public bool IsActive { get; private set; }
        public bool HidesTime => hidesTime;
        public bool RainbowSnow => false;
        public string Name { get; } = name;

        public void Activate()
        {
            IsActive = true;
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            ElapsedCount++;
            IsActive = false;
        }

        public void Draw(Image<Rgba32> img)
        {
            DrawCount++;
            img[0, 0] = new Rgba32(255, 255, 255);
        }
    }
}
