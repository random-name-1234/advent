using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal static class HeadlineText
{
    // 7px wide, 9px tall digit font — clean single-pixel strokes.
    private static readonly Dictionary<char, string[]> LargeDigits = new()
    {
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
        ]
    };

    private static readonly string[] ColonGlyph =
    [
        "00",
        "11",
        "11",
        "00",
        "00",
        "00",
        "11",
        "11",
        "00"
    ];

    internal const int Height = 9;
    internal const int TimeWidth = 36;
    private static readonly PixelFont Font = BuildFont();

    private static PixelFont BuildFont()
    {
        var glyphs = new Dictionary<char, string[]>(LargeDigits)
        {
            ['-'] = ["0000000", "0000000", "0000000", "0000000", "0111110", "0000000", "0000000", "0000000", "0000000"],
            ['\u00b0'] = ["111", "101", "111", "000", "000", "000", "000", "000", "000"],
            ['C'] = ["01111", "11000", "11000", "11000", "11000", "11000", "11000", "11000", "01111"],
            [':'] = ColonGlyph
        };
        return new PixelFont(Height, 1, glyphs, glyphs['-']);
    }

    internal static int MeasureWidth(string text) => Font.MeasureWidth(text);
    internal static void Draw(Image<Rgba32> image, string text, int x, int y, Rgba32 color) =>
        Font.Draw(image, text, x, y, color);

    internal static void DrawTime(Image<Rgba32> image, string time, int x, int y,
        Rgba32 color, Rgba32? colonColor = null)
    {
        if (time.Length != 5 || time[2] != ':')
            time = "--:--";

        ReadOnlySpan<int> offsets = [0, 8, 17, 21, 29];
        for (var index = 0; index < time.Length; index++)
        {
            var character = index == 2 ? ':' : time[index] is >= '0' and <= '9' ? time[index] : '-';
            Font.Draw(image, character.ToString(), x + offsets[index], y, index == 2 ? colonColor ?? color : color);
        }
    }
}
