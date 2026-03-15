using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;

namespace advent;

public sealed class SceneSelector
{
    private const string DefaultImageDirectory = "advent-images";
    private const int MatrixWidth = 64;

    private static readonly IReadOnlySet<string> SupportedStaticImageExtensions = new HashSet<string>(
        [".png", ".jpg", ".jpeg", ".bmp", ".webp"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> SupportedAnimatedImageExtensions = new HashSet<string>(
        [".gif"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<SceneDefinition> BaseSceneDefinitions =
    [
        new SceneDefinition("Weather", static () => new FadingScene(new WeatherScene())),
        new SceneDefinition("Cat", static () => new CatScene()),
        new SceneDefinition("Rainbow", static () => new RainbowSnowScene()),
        new SceneDefinition("Game of Life", static () => new FadingScene(new GameOfLifeScene())),
        new SceneDefinition("Starfield Parallax", static () => new FadingScene(new StarfieldParallaxScene())),
        new SceneDefinition("Plasma SDF", static () => new FadingScene(new PlasmaSdfScene())),
        new SceneDefinition("Matrix Rain", static () => new FadingScene(new MatrixRainScene())),
        new SceneDefinition("Synthwave Grid", static () => new FadingScene(new SynthwaveGridScene())),
        new SceneDefinition("Orbital", static () => new FadingScene(new OrbitalScene())),
        new SceneDefinition("Error", static () => new FadingScene(new ErrorScene())),
        new SceneDefinition("Space Invaders", static () => new FadingScene(new SpaceInvadersScene()))
    ];

    private static readonly IReadOnlyList<SceneDefinition> DecemberSceneDefinitions =
    [
        new SceneDefinition("Santa", static () => new FadingScene(new SantaScene()))
    ];

    private readonly IReadOnlyList<SceneDefinition> sceneDefinitions;
    private readonly IReadOnlyList<SceneDefinition> cycleSceneDefinitions;
    private readonly Func<int, int> nextIndex;
    private int cycleIndex;

    public SceneSelector()
        : this(DateTime.Now.Month)
    {
    }

    public SceneSelector(int month, Func<int, int>? nextIndex = null, string imageSceneDirectory = DefaultImageDirectory)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be in the range 1-12.");

        var monthSceneDefinitions = BaseSceneDefinitions.ToList();
        if (month == 12)
            monthSceneDefinitions.AddRange(DecemberSceneDefinitions);
        monthSceneDefinitions.AddRange(LoadImageSceneDefinitions(month, imageSceneDirectory));

        sceneDefinitions = monthSceneDefinitions.ToArray();
        cycleSceneDefinitions = sceneDefinitions;

        this.nextIndex = nextIndex ?? Random.Shared.Next;
        AvailableSceneNames = sceneDefinitions.Select(static x => x.Name).ToArray();
        AllSceneNames = cycleSceneDefinitions.Select(static x => x.Name).ToArray();
        cycleIndex = 0;
    }

    public IReadOnlyList<string> AvailableSceneNames { get; }
    public IReadOnlyList<string> AllSceneNames { get; }

    public ISpecialScene GetScene()
    {
        var index = nextIndex(sceneDefinitions.Count);
        if ((uint)index >= (uint)sceneDefinitions.Count)
            throw new InvalidOperationException(
                $"Scene index provider returned {index}, but valid range is 0..{sceneDefinitions.Count - 1}.");

        return sceneDefinitions[index].Create();
    }

    public string GetNextSceneNameInCycle()
    {
        var scene = GetNextSceneDefinitionInCycle();
        return scene.Name;
    }

    public ISpecialScene GetNextSceneInCycle()
    {
        var scene = GetNextSceneDefinitionInCycle();
        return scene.Create();
    }

    private SceneDefinition GetNextSceneDefinitionInCycle()
    {
        if (cycleSceneDefinitions.Count == 0)
            throw new InvalidOperationException("No scenes are available for cycling.");

        var selectedScene = cycleSceneDefinitions[cycleIndex];
        cycleIndex = (cycleIndex + 1) % cycleSceneDefinitions.Count;
        return selectedScene;
    }

    private static IReadOnlyList<SceneDefinition> LoadImageSceneDefinitions(int month, string imageSceneDirectory)
    {
        if (string.IsNullOrWhiteSpace(imageSceneDirectory))
            return [];

        var definitions = new List<SceneDefinition>();

        foreach (var filePath in EnumerateImageFiles(imageSceneDirectory, month))
        {
            var definition = CreateImageSceneDefinition(filePath);
            if (definition is not null)
                definitions.Add(definition);
        }

        return definitions;
    }

    private static IEnumerable<string> EnumerateImageFiles(string imageSceneDirectory, int month)
    {
        if (!Directory.Exists(imageSceneDirectory))
            return [];

        var monthDirectory = Path.Combine(imageSceneDirectory, month.ToString(CultureInfo.InvariantCulture));
        var rootFiles = Directory.EnumerateFiles(imageSceneDirectory)
            .Where(IsSupportedImageFile)
            .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase);

        var monthFiles = Directory.Exists(monthDirectory)
            ? Directory.EnumerateFiles(monthDirectory)
                .Where(IsSupportedImageFile)
                .OrderBy(static x => x, StringComparer.OrdinalIgnoreCase)
            : Enumerable.Empty<string>();

        return rootFiles.Concat(monthFiles);
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedStaticImageExtensions.Contains(extension) || SupportedAnimatedImageExtensions.Contains(extension);
    }

    private static SceneDefinition? CreateImageSceneDefinition(string filePath)
    {
        var sceneName = BuildImageSceneName(filePath);
        var extension = Path.GetExtension(filePath);

        if (SupportedAnimatedImageExtensions.Contains(extension))
            return new SceneDefinition(sceneName, () => new FadingScene(new AnimatedGifScene(filePath, sceneName)));

        if (!SupportedStaticImageExtensions.Contains(extension))
            return null;

        try
        {
            var info = Image.Identify(filePath);
            if (info is null)
                return null;

            if (info.Width > MatrixWidth && info.Width >= info.Height)
                return new SceneDefinition(sceneName, () => new FadingScene(new ScrollingImageScene(filePath, sceneName)));

            return new SceneDefinition(sceneName, () => new FadingScene(new StaticImageScene(filePath, sceneName)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skipping image scene '{filePath}': {ex.Message}");
            return null;
        }
    }

    private static string BuildImageSceneName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return string.IsNullOrWhiteSpace(name) ? "Image Scene" : name.Replace('_', ' ');
    }

    private sealed record SceneDefinition(string Name, Func<ISpecialScene> Create);
}
