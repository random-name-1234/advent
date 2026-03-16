using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public sealed class PixelFont
{
    private readonly string[] fallbackGlyph;
    private readonly IReadOnlyDictionary<char, string[]> glyphs;
    private readonly Func<string, string> normalizeText;

    public PixelFont(
        int height,
        int characterSpacing,
        IReadOnlyDictionary<char, string[]> glyphs,
        string[] fallbackGlyph,
        Func<string, string>? normalizeText = null)
    {
        Height = height;
        CharacterSpacing = characterSpacing;
        this.glyphs = glyphs;
        this.fallbackGlyph = fallbackGlyph;
        this.normalizeText = normalizeText ?? Normalize;
    }

    public int Height { get; }
    public int CharacterSpacing { get; }

    public int MeasureWidth(string text)
    {
        var normalized = this.normalizeText(text);
        if (normalized.Length == 0)
            return 0;

        var width = 0;
        var first = true;
        foreach (var c in normalized)
        {
            if (!first)
                width += CharacterSpacing;

            width += ResolveGlyph(c)[0].Length;
            first = false;
        }

        return width;
    }

    public string TrimToWidth(string text, int maxWidth)
    {
        var normalized = this.normalizeText(text);
        if (MeasureWidth(normalized) <= maxWidth)
            return normalized;

        var trimmed = normalized;
        while (trimmed.Length > 0 && MeasureWidth(trimmed) > maxWidth)
            trimmed = trimmed[..^1];

        return trimmed.TrimEnd();
    }

    public void Draw(Image<Rgba32> img, string text, int x, int y, Rgba32 color)
    {
        var normalized = this.normalizeText(text);
        if (normalized.Length == 0)
            return;

        var cursor = x;
        var first = true;
        foreach (var c in normalized)
        {
            var glyph = ResolveGlyph(c);
            if (!first)
                cursor += CharacterSpacing;

            DrawGlyph(img, glyph, cursor, y, color);
            cursor += glyph[0].Length;
            first = false;
        }
    }

    public void DrawCentered(Image<Rgba32> img, string text, int centerX, int y, Rgba32 color)
    {
        var width = MeasureWidth(text);
        if (width == 0)
            return;

        Draw(img, text, centerX - width / 2, y, color);
    }

    public void DrawRightAligned(Image<Rgba32> img, string text, int rightX, int y, Rgba32 color)
    {
        var width = MeasureWidth(text);
        if (width == 0)
            return;

        Draw(img, text, rightX - width + 1, y, color);
    }

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text
            .Trim()
            .ToUpperInvariant()
            .Replace('•', '.')
            .Replace('·', '.')
            .Replace('…', '.')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('’', '\'')
            .Replace('‘', '\'');
    }

    public static string NormalizePreservingCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text
            .Trim()
            .Replace('•', '.')
            .Replace('·', '.')
            .Replace('…', '.')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('’', '\'')
            .Replace('‘', '\'');
    }

    private string[] ResolveGlyph(char c)
    {
        return glyphs.TryGetValue(c, out var glyph) ? glyph : fallbackGlyph;
    }

    private static void DrawGlyph(Image<Rgba32> img, string[] glyph, int x, int y, Rgba32 color)
    {
        for (var row = 0; row < glyph.Length; row++)
        {
            var bits = glyph[row];
            for (var col = 0; col < bits.Length; col++)
            {
                if (bits[col] != '1')
                    continue;

                var px = x + col;
                var py = y + row;
                if ((uint)px >= img.Width || (uint)py >= img.Height)
                    continue;

                img[px, py] = color;
            }
        }
    }
}
