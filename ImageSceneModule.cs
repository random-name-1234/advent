using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using SixLabors.ImageSharp;

namespace advent;

internal sealed class ImageSceneModule : ISceneModule
{
    private const string DefaultImageDirectory = "advent-images";
    private const string DefaultLocalImageDirectory = "advent-images.local";
    private const string ImageManifestFileName = "manifest.json";
    private const string ExtraImageDirectoriesEnvironmentVariable = "ADVENT_EXTRA_IMAGE_DIRECTORIES";
    private const int MatrixWidth = 64;

    private static readonly IReadOnlySet<string> SupportedStaticImageExtensions = new HashSet<string>(
        [".png", ".jpg", ".jpeg", ".bmp", ".webp"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> SupportedAnimatedImageExtensions = new HashSet<string>(
        [".gif"],
        StringComparer.OrdinalIgnoreCase);

    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        return ResolveImageSceneDirectories(context.ImageSceneDirectory, context.ExtraImageSceneDirectories)
            .SelectMany(imageDirectory => LoadImageSceneRegistrations(context.Month, imageDirectory))
            .ToArray();
    }

    private static IReadOnlyList<SceneCatalogRegistration> LoadImageSceneRegistrations(int month, string imageSceneDirectory)
    {
        if (string.IsNullOrWhiteSpace(imageSceneDirectory))
            return [];

        var manifestOverrides = LoadManifestOverrides(imageSceneDirectory);
        var registrations = new List<SceneCatalogRegistration>();
        var seenRelativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in EnumerateImageFiles(imageSceneDirectory, month))
        {
            var relativePath = GetRelativeImagePath(imageSceneDirectory, filePath);
            if (!seenRelativePaths.Add(relativePath))
                continue;

            manifestOverrides.TryGetValue(relativePath, out var manifestOverride);
            if (manifestOverride is not null && !manifestOverride.AppliesToMonth(month))
                continue;

            var registration = CreateImageSceneRegistration(filePath, manifestOverride);
            if (registration is not null)
                registrations.Add(registration);
        }

        foreach (var manifestOverride in manifestOverrides.Values.OrderBy(
                     static overrideValue => overrideValue.RelativePath,
                     StringComparer.OrdinalIgnoreCase))
        {
            if (!manifestOverride.AppliesToMonth(month) || !seenRelativePaths.Add(manifestOverride.RelativePath))
                continue;

            var overrideFilePath = Path.Combine(
                imageSceneDirectory,
                manifestOverride.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(overrideFilePath))
            {
                Console.WriteLine(
                    $"Skipping manifest image scene '{manifestOverride.RelativePath}': file does not exist.");
                continue;
            }

            if (!IsSupportedImageFile(overrideFilePath))
            {
                Console.WriteLine(
                    $"Skipping manifest image scene '{manifestOverride.RelativePath}': unsupported image extension.");
                continue;
            }

            var registration = CreateImageSceneRegistration(overrideFilePath, manifestOverride);
            if (registration is not null)
                registrations.Add(registration);
        }

        return registrations;
    }

    private static IReadOnlyList<string> ResolveImageSceneDirectories(
        string imageSceneDirectory,
        IEnumerable<string>? extraImageSceneDirectories)
    {
        var directories = new List<string>();
        AddImageSceneDirectory(directories, imageSceneDirectory);

        if (string.Equals(imageSceneDirectory, DefaultImageDirectory, StringComparison.OrdinalIgnoreCase))
            AddImageSceneDirectory(directories, DefaultLocalImageDirectory);

        foreach (var extraDirectory in ReadConfiguredExtraImageDirectories(extraImageSceneDirectories))
            AddImageSceneDirectory(directories, extraDirectory);

        return directories;
    }

    private static IEnumerable<string> ReadConfiguredExtraImageDirectories(
        IEnumerable<string>? extraImageSceneDirectories)
    {
        if (extraImageSceneDirectories is not null)
        {
            foreach (var extraDirectory in extraImageSceneDirectories)
                yield return extraDirectory;
        }

        var configuredDirectories = Environment.GetEnvironmentVariable(ExtraImageDirectoriesEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configuredDirectories))
            yield break;

        foreach (var entry in configuredDirectories.Split(
                     GetImageDirectorySeparators(),
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return entry;
    }

    private static char[] GetImageDirectorySeparators()
    {
        return Path.PathSeparator == ';' ? [';'] : [Path.PathSeparator, ';'];
    }

    private static void AddImageSceneDirectory(List<string> directories, string? imageSceneDirectory)
    {
        if (string.IsNullOrWhiteSpace(imageSceneDirectory))
            return;

        var normalizedDirectory = imageSceneDirectory.Trim();
        if (!directories.Contains(normalizedDirectory, StringComparer.OrdinalIgnoreCase))
            directories.Add(normalizedDirectory);
    }

    private static IEnumerable<string> EnumerateImageFiles(string imageSceneDirectory, int month)
    {
        if (!Directory.Exists(imageSceneDirectory))
            return [];

        var monthDirectory = Path.Combine(imageSceneDirectory, month.ToString(CultureInfo.InvariantCulture));
        var rootFiles = Directory.EnumerateFiles(imageSceneDirectory)
            .Where(IsSupportedImageFile)
            .OrderBy(static filePath => filePath, StringComparer.OrdinalIgnoreCase);

        var monthFiles = Directory.Exists(monthDirectory)
            ? Directory.EnumerateFiles(monthDirectory)
                .Where(IsSupportedImageFile)
                .OrderBy(static filePath => filePath, StringComparer.OrdinalIgnoreCase)
            : Enumerable.Empty<string>();

        return rootFiles.Concat(monthFiles);
    }

    private static bool IsSupportedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedStaticImageExtensions.Contains(extension) || SupportedAnimatedImageExtensions.Contains(extension);
    }

    private static Dictionary<string, ManifestImageOverride> LoadManifestOverrides(string imageSceneDirectory)
    {
        var overrides = new Dictionary<string, ManifestImageOverride>(StringComparer.OrdinalIgnoreCase);
        var manifestPath = Path.Combine(imageSceneDirectory, ImageManifestFileName);
        if (!File.Exists(manifestPath))
            return overrides;

        try
        {
            var manifest = JsonSerializer.Deserialize<ImageSceneManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (manifest?.Images is null)
                return overrides;

            foreach (var entry in manifest.Images)
            {
                if (string.IsNullOrWhiteSpace(entry.File))
                {
                    Console.WriteLine("Skipping manifest image scene entry: missing 'file'.");
                    continue;
                }

                var relativePath = NormalizeRelativePath(entry.File);
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    Console.WriteLine("Skipping manifest image scene entry: invalid 'file' path.");
                    continue;
                }

                if (Path.IsPathRooted(entry.File) ||
                    relativePath.StartsWith("../", StringComparison.Ordinal) ||
                    relativePath.Contains("/../", StringComparison.Ordinal))
                {
                    Console.WriteLine(
                        $"Skipping manifest image scene entry '{entry.File}': path must stay within {DefaultImageDirectory}/.");
                    continue;
                }

                if (!TryParseImageSceneKind(entry.Type, out var kind))
                {
                    Console.WriteLine(
                        $"Unknown scene type '{entry.Type}' for '{relativePath}'. Falling back to auto detection.");
                    kind = ImageSceneKind.Auto;
                }

                HashSet<int>? months = null;
                if (entry.Months is { Length: > 0 })
                {
                    months = [.. entry.Months.Where(static month => month is >= 1 and <= 12)];
                    if (months.Count != entry.Months.Length)
                        Console.WriteLine(
                            $"Manifest entry '{relativePath}' contains invalid months. Only values 1-12 are used.");
                }

                TimeSpan? duration = null;
                if (entry.DurationSeconds is { } durationSeconds)
                {
                    if (durationSeconds > 0)
                    {
                        duration = TimeSpan.FromSeconds(durationSeconds);
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Manifest entry '{relativePath}' has non-positive durationSeconds. Default duration will be used.");
                    }
                }

                var sceneName = string.IsNullOrWhiteSpace(entry.Name) ? null : entry.Name.Trim();
                overrides[relativePath] =
                    new ManifestImageOverride(relativePath, sceneName, kind, duration, months);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skipping image scene manifest '{manifestPath}': {ex.Message}");
        }

        return overrides;
    }

    private static bool TryParseImageSceneKind(string? type, out ImageSceneKind kind)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            kind = ImageSceneKind.Auto;
            return true;
        }

        switch (type.Trim().ToLowerInvariant())
        {
            case "auto":
                kind = ImageSceneKind.Auto;
                return true;
            case "gif":
            case "animated":
            case "animation":
                kind = ImageSceneKind.Animated;
                return true;
            case "static":
                kind = ImageSceneKind.Static;
                return true;
            case "scroll":
            case "scrolling":
            case "banner":
                kind = ImageSceneKind.Scrolling;
                return true;
            default:
                kind = ImageSceneKind.Auto;
                return false;
        }
    }

    private static SceneCatalogRegistration? CreateImageSceneRegistration(string filePath, ManifestImageOverride? manifestOverride)
    {
        var sceneName = manifestOverride is { Name.Length: > 0 } ? manifestOverride.Name : BuildImageSceneName(filePath);
        var sceneDuration = manifestOverride?.Duration;
        var extension = Path.GetExtension(filePath);

        if (manifestOverride?.Kind == ImageSceneKind.Animated)
        {
            if (!SupportedAnimatedImageExtensions.Contains(extension))
            {
                Console.WriteLine(
                    $"Skipping image scene '{filePath}': manifest type 'animated' requires a gif file.");
                return null;
            }

            return new SceneCatalogRegistration(sceneName, () => new AnimatedGifScene(filePath, sceneName, sceneDuration));
        }

        if (manifestOverride?.Kind == ImageSceneKind.Static)
        {
            if (!SupportedStaticImageExtensions.Contains(extension))
            {
                Console.WriteLine($"Skipping image scene '{filePath}': manifest type 'static' requires a static image.");
                return null;
            }

            return new SceneCatalogRegistration(sceneName, () => new StaticImageScene(filePath, sceneName, sceneDuration));
        }

        if (manifestOverride?.Kind == ImageSceneKind.Scrolling)
        {
            if (!SupportedStaticImageExtensions.Contains(extension))
            {
                Console.WriteLine(
                    $"Skipping image scene '{filePath}': manifest type 'scroll' requires a static image.");
                return null;
            }

            return new SceneCatalogRegistration(sceneName, () => new ScrollingImageScene(filePath, sceneName, sceneDuration));
        }

        if (SupportedAnimatedImageExtensions.Contains(extension))
            return new SceneCatalogRegistration(sceneName, () => new AnimatedGifScene(filePath, sceneName, sceneDuration));

        if (!SupportedStaticImageExtensions.Contains(extension))
            return null;

        try
        {
            var info = Image.Identify(filePath);
            if (info is null)
                return null;

            if (info.Width > MatrixWidth && info.Width >= info.Height)
                return new SceneCatalogRegistration(sceneName, () => new ScrollingImageScene(filePath, sceneName, sceneDuration));

            return new SceneCatalogRegistration(sceneName, () => new StaticImageScene(filePath, sceneName, sceneDuration));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Skipping image scene '{filePath}': {ex.Message}");
            return null;
        }
    }

    private static string GetRelativeImagePath(string imageSceneDirectory, string filePath)
    {
        return NormalizeRelativePath(Path.GetRelativePath(imageSceneDirectory, filePath));
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').TrimStart('/');
    }

    private static string BuildImageSceneName(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        return string.IsNullOrWhiteSpace(name) ? "Image Scene" : name.Replace('_', ' ');
    }

    private sealed class ImageSceneManifest
    {
        public ManifestImageEntry[]? Images { get; init; }
    }

    private sealed class ManifestImageEntry
    {
        public string? File { get; init; }
        public string? Name { get; init; }
        public string? Type { get; init; }
        public double? DurationSeconds { get; init; }
        public int[]? Months { get; init; }
    }

    private sealed record ManifestImageOverride(
        string RelativePath,
        string? Name,
        ImageSceneKind Kind,
        TimeSpan? Duration,
        HashSet<int>? Months)
    {
        public bool AppliesToMonth(int month)
        {
            return Months is null || Months.Count == 0 || Months.Contains(month);
        }
    }

    private enum ImageSceneKind
    {
        Auto,
        Animated,
        Static,
        Scrolling
    }
}
