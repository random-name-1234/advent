using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public static class PixelText
{
    public const int Height = 5;
    public const int CharacterSpacing = 1;

    private static readonly string[] FallbackGlyph =
    [
        "1110",
        "0001",
        "0010",
        "0000",
        "0010"
    ];

    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        [' '] =
        [
            "00",
            "00",
            "00",
            "00",
            "00"
        ],
        ['!'] =
        [
            "1",
            "1",
            "1",
            "0",
            "1"
        ],
        ['\''] =
        [
            "1",
            "1",
            "0",
            "0",
            "0"
        ],
        ['+'] =
        [
            "000",
            "010",
            "111",
            "010",
            "000"
        ],
        [','] =
        [
            "0",
            "0",
            "0",
            "1",
            "1"
        ],
        ['-'] =
        [
            "000",
            "000",
            "111",
            "000",
            "000"
        ],
        ['.'] =
        [
            "0",
            "0",
            "0",
            "0",
            "1"
        ],
        ['/'] =
        [
            "001",
            "001",
            "010",
            "100",
            "100"
        ],
        ['0'] =
        [
            "0110",
            "1001",
            "1001",
            "1001",
            "0110"
        ],
        ['1'] =
        [
            "0010",
            "0110",
            "0010",
            "0010",
            "0111"
        ],
        ['2'] =
        [
            "1110",
            "0001",
            "0110",
            "1000",
            "1111"
        ],
        ['3'] =
        [
            "1110",
            "0001",
            "0110",
            "0001",
            "1110"
        ],
        ['4'] =
        [
            "1001",
            "1001",
            "1111",
            "0001",
            "0001"
        ],
        ['5'] =
        [
            "1111",
            "1000",
            "1110",
            "0001",
            "1110"
        ],
        ['6'] =
        [
            "0111",
            "1000",
            "1110",
            "1001",
            "0110"
        ],
        ['7'] =
        [
            "1111",
            "0001",
            "0010",
            "0100",
            "0100"
        ],
        ['8'] =
        [
            "0110",
            "1001",
            "0110",
            "1001",
            "0110"
        ],
        ['9'] =
        [
            "0110",
            "1001",
            "0111",
            "0001",
            "1110"
        ],
        [':'] =
        [
            "0",
            "1",
            "0",
            "1",
            "0"
        ],
        ['?'] =
        [
            "1110",
            "0001",
            "0010",
            "0000",
            "0010"
        ],
        ['A'] =
        [
            "0110",
            "1001",
            "1111",
            "1001",
            "1001"
        ],
        ['B'] =
        [
            "1110",
            "1001",
            "1110",
            "1001",
            "1110"
        ],
        ['C'] =
        [
            "0111",
            "1000",
            "1000",
            "1000",
            "0111"
        ],
        ['D'] =
        [
            "1110",
            "1001",
            "1001",
            "1001",
            "1110"
        ],
        ['E'] =
        [
            "1111",
            "1000",
            "1110",
            "1000",
            "1111"
        ],
        ['F'] =
        [
            "1111",
            "1000",
            "1110",
            "1000",
            "1000"
        ],
        ['G'] =
        [
            "0111",
            "1000",
            "1011",
            "1001",
            "0111"
        ],
        ['H'] =
        [
            "1001",
            "1001",
            "1111",
            "1001",
            "1001"
        ],
        ['I'] =
        [
            "111",
            "010",
            "010",
            "010",
            "111"
        ],
        ['J'] =
        [
            "0011",
            "0001",
            "0001",
            "1001",
            "0110"
        ],
        ['K'] =
        [
            "1001",
            "1010",
            "1100",
            "1010",
            "1001"
        ],
        ['L'] =
        [
            "1000",
            "1000",
            "1000",
            "1000",
            "1111"
        ],
        ['M'] =
        [
            "1001",
            "1111",
            "1111",
            "1001",
            "1001"
        ],
        ['N'] =
        [
            "1001",
            "1101",
            "1011",
            "1001",
            "1001"
        ],
        ['O'] =
        [
            "0110",
            "1001",
            "1001",
            "1001",
            "0110"
        ],
        ['P'] =
        [
            "1110",
            "1001",
            "1110",
            "1000",
            "1000"
        ],
        ['Q'] =
        [
            "0110",
            "1001",
            "1001",
            "1011",
            "0111"
        ],
        ['R'] =
        [
            "1110",
            "1001",
            "1110",
            "1010",
            "1001"
        ],
        ['S'] =
        [
            "0111",
            "1000",
            "0110",
            "0001",
            "1110"
        ],
        ['T'] =
        [
            "1111",
            "0100",
            "0100",
            "0100",
            "0100"
        ],
        ['U'] =
        [
            "1001",
            "1001",
            "1001",
            "1001",
            "0110"
        ],
        ['V'] =
        [
            "1001",
            "1001",
            "1001",
            "0110",
            "0110"
        ],
        ['W'] =
        [
            "1001",
            "1001",
            "1111",
            "1111",
            "1001"
        ],
        ['X'] =
        [
            "1001",
            "0110",
            "0110",
            "0110",
            "1001"
        ],
        ['Y'] =
        [
            "1001",
            "0110",
            "0100",
            "0100",
            "0100"
        ],
        ['Z'] =
        [
            "1111",
            "0010",
            "0100",
            "1000",
            "1111"
        ],
        ['('] =
        [
            "01",
            "10",
            "10",
            "10",
            "01"
        ],
        [')'] =
        [
            "10",
            "01",
            "01",
            "01",
            "10"
        ]
    };

    public static int MeasureWidth(string text)
    {
        var normalized = Normalize(text);
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

    public static string TrimToWidth(string text, int maxWidth)
    {
        var normalized = Normalize(text);
        if (MeasureWidth(normalized) <= maxWidth)
            return normalized;

        var trimmed = normalized;
        while (trimmed.Length > 0 && MeasureWidth(trimmed) > maxWidth)
            trimmed = trimmed[..^1];

        return trimmed.TrimEnd();
    }

    public static void Draw(Image<Rgba32> img, string text, int x, int y, Rgba32 color)
    {
        var normalized = Normalize(text);
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

    public static void DrawCentered(Image<Rgba32> img, string text, int centerX, int y, Rgba32 color)
    {
        var width = MeasureWidth(text);
        if (width == 0)
            return;

        Draw(img, text, centerX - width / 2, y, color);
    }

    public static void DrawRightAligned(Image<Rgba32> img, string text, int rightX, int y, Rgba32 color)
    {
        var width = MeasureWidth(text);
        if (width == 0)
            return;

        Draw(img, text, rightX - width + 1, y, color);
    }

    private static string Normalize(string text)
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

    private static string[] ResolveGlyph(char c)
    {
        return Glyphs.TryGetValue(c, out var glyph) ? glyph : FallbackGlyph;
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
