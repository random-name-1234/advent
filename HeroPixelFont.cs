using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

// 7x9 pixel font for large temperature readouts. The digit shapes match
// ClockRenderer so the weather hero reads like the clock and rail boards
// instead of a shrunk-down TrueType outline that leaves stray lit pixels.
internal static class HeroPixelFont
{
    private const int Height = 9;
    private const int CharacterSpacing = 1;

    private static readonly string[] FallbackGlyph =
    [
        "0111110",
        "1100011",
        "0000011",
        "0000110",
        "0001100",
        "0001100",
        "0000000",
        "0001100",
        "0001100"
    ];

    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        [' '] =
        [
            "00",
            "00",
            "00",
            "00",
            "00",
            "00",
            "00",
            "00",
            "00"
        ],
        ['-'] =
        [
            "0000",
            "0000",
            "0000",
            "0000",
            "1111",
            "0000",
            "0000",
            "0000",
            "0000"
        ],
        ['\u00b0'] =
        [
            "111",
            "101",
            "111",
            "000",
            "000",
            "000",
            "000",
            "000",
            "000"
        ],
        ['0'] =
        [
            "0111110",
            "1100011",
            "1100011",
            "1100011",
            "1100011",
            "1100011",
            "1100011",
            "1100011",
            "0111110"
        ],
        ['1'] =
        [
            "0001100",
            "0011100",
            "0101100",
            "0001100",
            "0001100",
            "0001100",
            "0001100",
            "0001100",
            "0111111"
        ],
        ['2'] =
        [
            "0111110",
            "1100011",
            "0000011",
            "0000110",
            "0011100",
            "0110000",
            "1100000",
            "1100011",
            "1111111"
        ],
        ['3'] =
        [
            "0111110",
            "1100011",
            "0000011",
            "0000011",
            "0011110",
            "0000011",
            "0000011",
            "1100011",
            "0111110"
        ],
        ['4'] =
        [
            "0000110",
            "0001110",
            "0011110",
            "0110110",
            "1100110",
            "1111111",
            "0000110",
            "0000110",
            "0000110"
        ],
        ['5'] =
        [
            "1111111",
            "1100000",
            "1100000",
            "1111110",
            "0000011",
            "0000011",
            "0000011",
            "1100011",
            "0111110"
        ],
        ['6'] =
        [
            "0111110",
            "1100011",
            "1100000",
            "1100000",
            "1111110",
            "1100011",
            "1100011",
            "1100011",
            "0111110"
        ],
        ['7'] =
        [
            "1111111",
            "1100011",
            "0000110",
            "0000110",
            "0001100",
            "0001100",
            "0011000",
            "0011000",
            "0011000"
        ],
        ['8'] =
        [
            "0111110",
            "1100011",
            "1100011",
            "1100011",
            "0111110",
            "1100011",
            "1100011",
            "1100011",
            "0111110"
        ],
        ['9'] =
        [
            "0111110",
            "1100011",
            "1100011",
            "1100011",
            "0111111",
            "0000011",
            "0000011",
            "1100011",
            "0111110"
        ],
        ['C'] =
        [
            "0111110",
            "1100011",
            "1100000",
            "1100000",
            "1100000",
            "1100000",
            "1100000",
            "1100011",
            "0111110"
        ]
    };

    private static readonly PixelFont Font = new(Height, CharacterSpacing, Glyphs, FallbackGlyph);

    public static int MeasureWidth(string text) => Font.MeasureWidth(text);

    public static void Draw(Image<Rgba32> img, string text, int x, int y, Rgba32 color) =>
        Font.Draw(img, text, x, y, color);

    public static void DrawRightAligned(Image<Rgba32> img, string text, int rightX, int y, Rgba32 color) =>
        Font.DrawRightAligned(img, text, rightX, y, color);
}
