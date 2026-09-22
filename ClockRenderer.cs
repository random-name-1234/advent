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

    public void Draw(Image<Rgba32> img) => DrawAt(img, DateTime.Now);

    internal void DrawAt(Image<Rgba32> img, DateTime now)
    {
        var colonVisible = now.Millisecond < 500;

        // Time vertically: y=3, height=9 → ends at y=11
        const int timeY = 3;
        DrawTimeDigits(img, now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), TimeStartX, timeY, TimeColor,
            colonVisible ? ColonColor : ColonDimColor);

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

    internal static void DrawTimeDigits(Image<Rgba32> img, string time, int x, int y,
        Rgba32 color, Rgba32? colonColor = null) =>
        HeadlineText.DrawTime(img, time, x, y, color, colonColor);

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x < img.Width && (uint)y < img.Height)
            img[x, y] = color;
    }
}
