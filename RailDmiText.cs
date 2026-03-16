using System;
using System.Collections.Generic;
using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

internal static class RailDmiText
{
    private const string AssetFileName = "uk-national-rail-station-other-dmi.otf";
    private const float SourcePointSize = 48f;
    private const int TargetHeight = 5;

    private static readonly char[] SupportedCharacters =
        " !'()+,-./0123456789:?ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

    private static readonly string[] SpaceGlyph =
    [
        "00",
        "00",
        "00",
        "00",
        "00"
    ];

    private static readonly string[] FallbackGlyph =
    [
        "1110",
        "0001",
        "0010",
        "0000",
        "0010"
    ];

    private static readonly Lazy<PixelFont> Font = new(BuildFont);

    public static int Height => Font.Value.Height;

    public static int MeasureWidth(string text) => Font.Value.MeasureWidth(text);

    public static string TrimToWidth(string text, int maxWidth) => Font.Value.TrimToWidth(text, maxWidth);

    public static void Draw(Image<Rgba32> img, string text, int x, int y, Rgba32 color) =>
        Font.Value.Draw(img, text, x, y, color);

    public static void DrawCentered(Image<Rgba32> img, string text, int centerX, int y, Rgba32 color) =>
        Font.Value.DrawCentered(img, text, centerX, y, color);

    public static void DrawRightAligned(Image<Rgba32> img, string text, int rightX, int y, Rgba32 color) =>
        Font.Value.DrawRightAligned(img, text, rightX, y, color);

    private static PixelFont BuildFont()
    {
        try
        {
            return BuildFontFromAsset();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Rail DMI font bake failed, falling back to default pixel font: {ex.Message}");
            return PixelText.Font;
        }
    }

    private static PixelFont BuildFontFromAsset()
    {
        var path = ResolveFontPath();
        var collection = new FontCollection();
        var family = collection.Add(path);
        var font = family.CreateFont(SourcePointSize, FontStyle.Regular);
        var glyphs = new Dictionary<char, string[]>();

        foreach (var c in SupportedCharacters)
            glyphs[c] = c == ' ' ? SpaceGlyph : BakeGlyph(font, c);

        ApplyManualOverrides(glyphs);

        var fallbackGlyph = glyphs.TryGetValue('?', out var fallback) ? fallback : FallbackGlyph;

        return new PixelFont(TargetHeight, 1, glyphs, fallbackGlyph);
    }

    private static string ResolveFontPath()
    {
        var direct = Path.Combine(AppContext.BaseDirectory, "assets", AssetFileName);
        if (File.Exists(direct))
            return direct;

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "assets", AssetFileName);
        if (File.Exists(cwd))
            return cwd;

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "assets", AssetFileName);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException($"Could not locate rail DMI font asset '{AssetFileName}'.");
    }

    private static string[] BakeGlyph(Font font, char c)
    {
        using var canvas = new Image<Rgba32>(96, 96);
        var drawingOptions = new DrawingOptions
        {
            GraphicsOptions = new GraphicsOptions
            {
                Antialias = true
            }
        };

        canvas.Mutate(ctx => ctx.DrawText(drawingOptions, c.ToString(), font, Color.White, new PointF(12, 8)));
        if (!TryGetInkBounds(canvas, out var left, out var top, out var right, out var bottom))
            return SpaceGlyph;

        left = Math.Max(0, left - 1);
        top = Math.Max(0, top - 1);
        right = Math.Min(canvas.Width - 1, right + 1);
        bottom = Math.Min(canvas.Height - 1, bottom + 1);

        var cropWidth = right - left + 1;
        var cropHeight = bottom - top + 1;
        var targetWidth = Math.Max(1, (int)MathF.Round(cropWidth * (TargetHeight / (float)cropHeight)));

        using var cropped = canvas.Clone(ctx => ctx.Crop(new Rectangle(left, top, cropWidth, cropHeight)));
        using var scaled = cropped.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
            Size = new Size(targetWidth, TargetHeight)
        }));

        var glyph = BuildGlyphRows(scaled, 0.2f);
        if (CountLitPixels(glyph) == 0)
            glyph = BuildGlyphRows(scaled, 0.1f);

        return TrimColumns(glyph);
    }

    private static bool TryGetInkBounds(
        Image<Rgba32> img,
        out int left,
        out int top,
        out int right,
        out int bottom)
    {
        left = img.Width;
        top = img.Height;
        right = -1;
        bottom = -1;

        for (var y = 0; y < img.Height; y++)
        for (var x = 0; x < img.Width; x++)
        {
            var pixel = img[x, y];
            if (pixel.A < 12)
                continue;

            left = Math.Min(left, x);
            top = Math.Min(top, y);
            right = Math.Max(right, x);
            bottom = Math.Max(bottom, y);
        }

        return right >= left && bottom >= top;
    }

    private static string[] BuildGlyphRows(Image<Rgba32> img, float threshold)
    {
        var rows = new string[img.Height];
        for (var y = 0; y < img.Height; y++)
        {
            var chars = new char[img.Width];
            for (var x = 0; x < img.Width; x++)
            {
                var pixel = img[x, y];
                var intensity = pixel.A / 255f;
                chars[x] = intensity >= threshold ? '1' : '0';
            }

            rows[y] = new string(chars);
        }

        return rows;
    }

    private static string[] TrimColumns(string[] rows)
    {
        var left = int.MaxValue;
        var right = -1;

        for (var y = 0; y < rows.Length; y++)
        for (var x = 0; x < rows[y].Length; x++)
        {
            if (rows[y][x] != '1')
                continue;

            left = Math.Min(left, x);
            right = Math.Max(right, x);
        }

        if (right < left)
            return SpaceGlyph;

        var trimmed = new string[rows.Length];
        var width = right - left + 1;
        for (var y = 0; y < rows.Length; y++)
            trimmed[y] = rows[y].Substring(left, width);

        return trimmed;
    }

    private static int CountLitPixels(string[] rows)
    {
        var total = 0;
        foreach (var row in rows)
        foreach (var bit in row)
            if (bit == '1')
                total++;

        return total;
    }

    // A few narrow glyphs read better with tiny manual corrections than with pure auto-bake.
    private static void ApplyManualOverrides(IDictionary<char, string[]> glyphs)
    {
        glyphs['1'] =
        [
            "0010",
            "0110",
            "0010",
            "0010",
            "0111"
        ];

        glyphs['I'] =
        [
            "111",
            "010",
            "010",
            "010",
            "111"
        ];
    }
}
