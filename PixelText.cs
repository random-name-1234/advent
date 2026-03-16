using System.Collections.Generic;

namespace advent;

public static class PixelText
{
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

    private static readonly PixelFont DefaultFont = new(
        5,
        1,
        Glyphs,
        FallbackGlyph);

    public static int Height => DefaultFont.Height;
    public static int CharacterSpacing => DefaultFont.CharacterSpacing;

    internal static PixelFont Font => DefaultFont;

    public static int MeasureWidth(string text) => DefaultFont.MeasureWidth(text);
    public static string TrimToWidth(string text, int maxWidth) => DefaultFont.TrimToWidth(text, maxWidth);
    public static void Draw(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img, string text, int x, int y,
        SixLabors.ImageSharp.PixelFormats.Rgba32 color) => DefaultFont.Draw(img, text, x, y, color);
    public static void DrawCentered(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img, string text, int centerX,
        int y, SixLabors.ImageSharp.PixelFormats.Rgba32 color) => DefaultFont.DrawCentered(img, text, centerX, y, color);
    public static void DrawRightAligned(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> img, string text,
        int rightX, int y, SixLabors.ImageSharp.PixelFormats.Rgba32 color) => DefaultFont.DrawRightAligned(img, text, rightX, y, color);
}
