using System.Reflection;
using System.Text.Json;
using advent.Data.Weather;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace advent.Tests;

public class SceneLayoutCaptureTests
{
    private static readonly DateTime ClockTime = new(2026, 9, 21, 18, 12, 30);

    [Fact]
    public void CaptureInformationLayouts()
    {
        var output = Environment.GetEnvironmentVariable("ADVENT_SCENE_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(output)) Directory.CreateDirectory(output);
        var frames = new List<(string Name, Image<Rgba32> Image)>();
        void Add(string name, Action<Image<Rgba32>> draw)
        {
            var image = new Image<Rgba32>(64, 32, Color.Black);
            draw(image);
            frames.Add((name, image));
            Assert.Contains(Enumerable.Range(0, 64 * 32), index => image[index % 64, index / 64].R > 0);
        }
        Add("01-clock-reference", image => new ClockRenderer().DrawAt(image, ClockTime));
        var normal = new WeatherSnapshot(12, 10, 8, 1, true,
            [new("TODAY", 1, 15, 9, 20, 12), new("TOM", 61, 14, 7, 100, 22), new("WED", 3, 11, 5, 10, 8)]);
        for (var panel = 0; panel < 3; panel++)
        {
            var index = panel;
            Add($"0{panel + 2}-weather-{panel}", image => WeatherScene.DrawPanel(image, normal, index, TimeSpan.Zero));
        }
        Add("05-weather-negative", image => WeatherScene.DrawPanel(image, InformationLayoutTests.WeatherFixture(-12, 95, 100, 99), 0, TimeSpan.Zero));
        Add("06-weather-night", image => WeatherScene.DrawPanel(image, normal with { IsDay = false }, 0, TimeSpan.Zero));
        Add("07-weather-unknown", image => WeatherScene.DrawPanel(image, InformationLayoutTests.WeatherFixture(float.NaN, -1, -1, float.NaN), 0, TimeSpan.Zero));
        var messages = new[] { "Hello Alan", "Good morning everyone", "Train to London Kings Cross is delayed by twenty minutes. Please check the platform before boarding.", "Rain 100% & wind 25mph" };
        var counter = 8;
        foreach (var message in messages)
        {
            var layout = MessageLayout.Create(message, null);
            for (var page = 0; page < layout.Pages.Count; page++)
            {
                var time = TimeSpan.FromSeconds(page * 4);
                Add($"{counter++:00}-message-page-{page + 1}", image => MessageScene.DrawPage(image, layout, time, new Rgba32(220, 230, 255)));
            }
        }
        for (var sample = 0; sample < LegibilityLabScene.SampleCount; sample++)
        {
            var index = sample;
            Add($"{counter++:00}-lab-{sample + 1}", image => LegibilityLabScene.DrawSample(image, index, TimeSpan.Zero));
        }
        using var sheet = new Image<Rgba32>(4 * 68, (int)Math.Ceiling(frames.Count / 4d) * 38, new Rgba32(18, 18, 18));
        try
        {
            for (var index = 0; index < frames.Count; index++)
            {
                var (name, image) = frames[index];
                if (string.IsNullOrWhiteSpace(output)) continue;
                image.SaveAsPng(Path.Combine(output, name + ".png"));
                sheet.Mutate(context => context.DrawImage(image, new Point(index % 4 * 68 + 2, index / 4 * 38 + 2), 1));
                using var large = image.Clone(context => context.Resize(640, 320, KnownResamplers.NearestNeighbor));
                large.SaveAsPng(Path.Combine(output, name + "-large.png"));
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                sheet.Mutate(context => context.Resize(sheet.Width * 4, sheet.Height * 4, KnownResamplers.NearestNeighbor));
                sheet.SaveAsPng(Path.Combine(output, "information-contact-sheet.png"));
                File.WriteAllText(Path.Combine(output, "information.json"), JsonSerializer.Serialize(frames.Select(frame => frame.Name)));
            }
        }
        finally { foreach (var frame in frames) frame.Image.Dispose(); }
    }

    [Fact]
    public void CaptureRemainingScenes_WhenRequested()
    {
        var output = Environment.GetEnvironmentVariable("ADVENT_SCENE_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        var root = FindRoot();
        var original = Environment.CurrentDirectory;
        var names = new List<string>();
        Directory.CreateDirectory(output);
        try
        {
            Environment.CurrentDirectory = root;
            ISpecialScene[] scenes = [new CatScene(), new StarfieldParallaxScene(), new MetaballsScene(),
                new DonkeyKongScene(), new SpaceInvadersScene(), new BonkersParadeScene(), new SynthwaveGridScene(),
                new OrbitalScene(new NewSceneCapture.FixedClock(NewSceneCapture.FixtureTime)), new FireworksScene(), new BoidsScene(), new TetrisScene(),
                new SunriseSunsetScene(timeProvider: new NewSceneCapture.FixedClock(NewSceneCapture.FixtureTime)),
                new ErrorScene(), new SantaScene(), new MatrixRainScene(), new WarpCoreScene(), new GameOfLifeScene(), new PlasmaSdfScene(), new RainbowSnowScene()];
            var seasonal = Directory.GetFiles(Path.Combine(root, "advent-images", "12"), "*.gif").Order()
                .Select(path => (ISpecialScene)new AnimatedGifScene(path));
            var allScenes = scenes.Concat(seasonal).ToArray();
            using var sheet = new Image<Rgba32>(68 * 5, 38 * ((allScenes.Length + 4) / 5), new Rgba32(18, 18, 18));
            foreach (var (scene, sceneIndex) in allScenes.Select((scene, index) => (scene, index)))
            {
                var label = scene is AnimatedGifScene ? $"christmas-{sceneIndex - scenes.Length + 1}" : scene.Name;
                var id = string.Concat(label.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-'));
                names.Add(id);
                foreach (var field in scene.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                    if (field.FieldType == typeof(Random)) field.SetValue(scene, new Random(1234));
                scene.Activate();
                if (scene is ErrorScene)
                    typeof(ErrorScene).GetField("noiseSeed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(scene, 1234);
                Image<Rgba32>? animation = null;
                try
                {
                    for (var frame = 0; frame < 240 && scene.IsActive; frame++)
                    {
                        scene.Elapsed(TimeSpan.FromSeconds(1d / 30));
                        if (frame % 3 != 0) continue;
                        using var image = new Image<Rgba32>(64, 32, Color.Black);
                        scene.Draw(image);
                        if (!scene.HidesTime) new ClockRenderer().DrawAt(image, ClockTime);
                        if (frame is 45 or 135 or 195) image.SaveAsPng(Path.Combine(output, $"{id}-{frame}.png"));
                        if (frame == 135)
                            sheet.Mutate(context => context.DrawImage(image, new Point(sceneIndex % 5 * 68 + 2, sceneIndex / 5 * 38 + 2), 1));
                        image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 10;
                        if (animation is null) animation = image.Clone();
                        else animation.Frames.AddFrame(image.Frames.RootFrame);
                    }
                    animation?.SaveAsGif(Path.Combine(output, id + ".gif"));
                }
                finally
                {
                    animation?.Dispose();
                    if (scene is IDisposable disposable) disposable.Dispose();
                }
            }
            File.WriteAllText(Path.Combine(output, "remaining-scenes.json"), JsonSerializer.Serialize(names));
            sheet.Mutate(context => context.Resize(sheet.Width * 4, sheet.Height * 4, KnownResamplers.NearestNeighbor));
            sheet.SaveAsPng(Path.Combine(output, "remaining-contact-sheet.png"));
        }
        finally { Environment.CurrentDirectory = original; }
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "advent.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException("Cannot find the scene assets.");
    }
}
