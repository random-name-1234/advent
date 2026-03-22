using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

/// <summary>
/// Renders a pixel-art clock face with large time digits, blinking colon,
/// and a date line, designed for legibility on a 64x32 LED matrix.
/// </summary>
internal sealed class ClockRenderer
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

    private const int DigitWidth = 7;
    private const int DigitHeight = 9;
    private const int ColonWidth = 2;
    private const int DigitKern = 1;   // gap between two digits in same group
    private const int ColonPad = 2;    // gap either side of colon

    // HH:MM = 7+1+7 + 2+2+2 + 7+1+7 = 36px, centered in 64 = offset 14
    private static readonly int TotalTimeWidth =
        DigitWidth + DigitKern + DigitWidth +
        ColonPad + ColonWidth + ColonPad +
        DigitWidth + DigitKern + DigitWidth;

    private static readonly int TimeStartX = (Width - TotalTimeWidth) / 2;

    private static readonly Rgba32 TimeColor = new(220, 230, 255);
    private static readonly Rgba32 ColonColor = new(150, 165, 200);
    private static readonly Rgba32 ColonDimColor = new(35, 40, 52);
    private static readonly Rgba32 DateColor = new(110, 120, 150);
    private static readonly Rgba32 SeparatorColor = new(28, 32, 42);

    public void Draw(Image<Rgba32> img)
    {
        var now = DateTime.Now;
        var hours = now.Hour;
        var minutes = now.Minute;
        var colonVisible = now.Millisecond < 500;

        // Time vertically: y=3, height=9 → ends at y=11
        const int timeY = 3;
        var x = TimeStartX;

        // Hour tens
        DrawLargeDigit(img, hours / 10, x, timeY, TimeColor);
        x += DigitWidth + DigitKern;

        // Hour units
        DrawLargeDigit(img, hours % 10, x, timeY, TimeColor);
        x += DigitWidth + ColonPad;

        // Colon
        DrawGlyph(img, ColonGlyph, ColonWidth, x, timeY, colonVisible ? ColonColor : ColonDimColor);
        x += ColonWidth + ColonPad;

        // Minute tens
        DrawLargeDigit(img, minutes / 10, x, timeY, TimeColor);
        x += DigitWidth + DigitKern;

        // Minute units
        DrawLargeDigit(img, minutes % 10, x, timeY, TimeColor);

        // Thin separator line
        const int sepY = timeY + DigitHeight + 2; // y=14
        for (var px = TimeStartX; px < TimeStartX + TotalTimeWidth; px++)
            SetPixel(img, px, sepY, SeparatorColor);

        // Date line: "FRI 21 MAR" centered, y=17
        const int dateY = sepY + 3;
        var dateText = FormatDate(now);
        RailDmiText.DrawCentered(img, dateText, Width / 2, dateY, DateColor);

        // Seconds progress bar — 2px tall with accent color
        var barY = dateY + RailDmiText.Height + 3;
        var barWidth = TotalTimeWidth;
        var barStart = TimeStartX;
        var filled = (int)MathF.Round(barWidth * (now.Second + now.Millisecond / 1000f) / 60f);
        for (var row = 0; row < 2; row++)
        for (var px = barStart; px < barStart + barWidth; px++)
        {
            var color = px - barStart < filled
                ? new Rgba32(80, 110, 180)
                : new Rgba32(20, 22, 30);
            SetPixel(img, px, barY + row, color);
        }
    }

    private static string FormatDate(DateTime now)
    {
        var dayOfWeek = now.ToString("ddd").ToUpperInvariant();
        var day = now.Day.ToString();
        var month = now.ToString("MMM").ToUpperInvariant();
        return $"{dayOfWeek} {day} {month}";
    }

    private static void DrawLargeDigit(Image<Rgba32> img, int digit, int x, int y, Rgba32 color)
    {
        if (digit is < 0 or > 9)
            return;

        var glyph = LargeDigits[(char)('0' + digit)];
        DrawGlyph(img, glyph, DigitWidth, x, y, color);
    }

    private static void DrawGlyph(Image<Rgba32> img, string[] glyph, int width, int x, int y, Rgba32 color)
    {
        for (var row = 0; row < glyph.Length; row++)
        {
            var bits = glyph[row];
            for (var col = 0; col < bits.Length && col < width; col++)
            {
                if (bits[col] != '1')
                    continue;

                SetPixel(img, x + col, y + row, color);
            }
        }
    }

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x < img.Width && (uint)y < img.Height)
            img[x, y] = color;
    }
}
