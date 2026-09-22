using System.Globalization;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal readonly record struct MatrixTextRun(string Text, int X, int Y, Rgba32 Color, int Scale = 1)
{
    internal Rectangle Bounds => new(X, Y, RailDmiText.MeasureWidth(Text) * Scale, RailDmiText.Height * Scale);
    internal void Draw(Image<Rgba32> image) => RailDmiText.Draw(image, Text, X, Y, Color, Scale);
}

internal static class MatrixTextLayout
{
    internal const int TextWidth = 60;

    internal static void Clear(Image<Rgba32> image) => image.ProcessPixelRows(rows =>
    {
        for (var y = 0; y < rows.Height; y++)
            rows.GetRowSpan(y).Fill(new Rgba32(0, 0, 0));
    });

    internal static string Normalize(string text)
    {
        var normalized = PixelFont.Normalize(text).Replace('\u2026', '.').Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (var rune in normalized.EnumerateRunes())
        {
            if (Rune.GetUnicodeCategory(rune) == UnicodeCategory.NonSpacingMark) continue;
            if (Rune.IsWhiteSpace(rune)) result.Append(' ');
            else if (rune.IsAscii && RailDmiText.HasGlyph((char)rune.Value)) result.Append((char)rune.Value);
            else if (rune.Value == '\u00b0') result.Append('\u00b0');
            else result.Append('?');
        }
        return string.Join(' ', result.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    internal static IReadOnlyList<string> Wrap(string normalized, int maxWidth)
    {
        var lines = new List<string>();
        var line = "";
        foreach (var word in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && RailDmiText.MeasureWidth($"{line} {word}") <= maxWidth)
            {
                line += $" {word}";
                continue;
            }
            if (line.Length > 0) lines.Add(line);
            var remaining = word;
            while (RailDmiText.MeasureWidth(remaining) > maxWidth)
            {
                var part = RailDmiText.TrimToWidth(remaining, maxWidth);
                if (part.Length == 0) throw new ArgumentOutOfRangeException(nameof(maxWidth));
                lines.Add(part);
                remaining = remaining[part.Length..];
            }
            line = remaining;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }
}
