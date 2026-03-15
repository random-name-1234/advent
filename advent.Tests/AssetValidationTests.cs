using System.Text.Json;
using SixLabors.ImageSharp;
using Xunit;

namespace advent.Tests;

public class AssetValidationTests
{
    private static readonly IReadOnlyDictionary<string, (int Width, int Height)> RequiredCoreAssetSizes =
        new Dictionary<string, (int Width, int Height)>
        {
            ["assets/cat/cat2-face.png"] = (64, 72),
            ["assets/cat/cat2-eyes.png"] = (64, 72),
            ["assets/error/error.png"] = (64, 32),
            ["assets/error/sad-face.png"] = (64, 32),
            ["assets/santa/santa-256x32.png"] = (256, 32),
            ["assets/santa/santa-256x32-legs1.png"] = (256, 32),
            ["assets/santa/santa-256x32-legs2.png"] = (256, 32),
            ["assets/space-invaders/space-invaders-player.png"] = (7, 3),
            ["assets/space-invaders/space-invaders-invader1.png"] = (7, 5),
            ["assets/space-invaders/space-invaders-exploding1.png"] = (7, 5),
            ["assets/space-invaders/space-invaders-blocks.png"] = (64, 32)
        };

    [Fact]
    public void CoreAssets_Exist_AndMatchExpectedDimensions()
    {
        var projectRoot = ResolveProjectRoot();

        foreach (var (relativePath, expectedSize) in RequiredCoreAssetSizes)
        {
            var absolutePath = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(absolutePath), $"Required asset is missing: {relativePath}");

            var info = Image.Identify(absolutePath);
            Assert.NotNull(info);
            Assert.Equal(expectedSize.Width, info!.Width);
            Assert.Equal(expectedSize.Height, info.Height);
        }
    }

    [Fact]
    public void ImageManifest_ReferencesValidFiles_WhenPresent()
    {
        var projectRoot = ResolveProjectRoot();
        var imageRoot = Path.Combine(projectRoot, "advent-images");
        var manifestPath = Path.Combine(imageRoot, "manifest.json");

        if (!File.Exists(manifestPath))
            return;

        using var json = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (!json.RootElement.TryGetProperty("images", out var imagesElement) ||
            imagesElement.ValueKind != JsonValueKind.Array)
            return;

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "auto",
            "gif",
            "animated",
            "animation",
            "static",
            "scroll",
            "scrolling",
            "banner"
        };

        foreach (var imageElement in imagesElement.EnumerateArray())
        {
            Assert.True(imageElement.TryGetProperty("file", out var fileElement) &&
                        fileElement.ValueKind == JsonValueKind.String &&
                        !string.IsNullOrWhiteSpace(fileElement.GetString()),
                "Each manifest entry must include a non-empty string 'file'.");

            var relativePath = NormalizeRelativePath(fileElement.GetString()!);
            Assert.False(Path.IsPathRooted(relativePath), $"Manifest file path must be relative: {relativePath}");
            Assert.False(relativePath.StartsWith("../", StringComparison.Ordinal), $"Manifest path escapes root: {relativePath}");
            Assert.False(relativePath.Contains("/../", StringComparison.Ordinal),
                $"Manifest path escapes root: {relativePath}");

            var fullPath = Path.GetFullPath(Path.Combine(imageRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.True(fullPath.StartsWith(Path.GetFullPath(imageRoot) + Path.DirectorySeparatorChar,
                StringComparison.Ordinal),
                $"Manifest file path resolves outside image root: {relativePath}");
            Assert.True(File.Exists(fullPath), $"Manifest references missing file: {relativePath}");

            var info = Image.Identify(fullPath);
            Assert.NotNull(info);

            if (imageElement.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(typeElement.GetString()))
            {
                var typeValue = typeElement.GetString()!.Trim();
                Assert.Contains(typeValue, allowedTypes);
            }

            if (imageElement.TryGetProperty("durationSeconds", out var durationElement) &&
                durationElement.ValueKind == JsonValueKind.Number)
            {
                Assert.True(durationElement.TryGetDouble(out var durationSeconds),
                    "durationSeconds must be numeric.");
                Assert.True(durationSeconds > 0, "durationSeconds must be > 0.");
            }

            if (imageElement.TryGetProperty("months", out var monthsElement))
            {
                Assert.Equal(JsonValueKind.Array, monthsElement.ValueKind);
                foreach (var monthElement in monthsElement.EnumerateArray())
                {
                    Assert.Equal(JsonValueKind.Number, monthElement.ValueKind);
                    Assert.True(monthElement.TryGetInt32(out var month), "Each month must be an integer.");
                    Assert.InRange(month, 1, 12);
                }
            }
        }
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
    }

    private static string ResolveProjectRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "assets")) &&
                Directory.Exists(Path.Combine(directory.FullName, "advent-images")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate project root with assets and advent-images.");
    }
}
