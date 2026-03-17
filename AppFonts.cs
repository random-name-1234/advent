using System;
using System.Linq;
using SixLabors.Fonts;

namespace advent;

internal static class AppFonts
{
    private static readonly string[] PreferredFontFamilies =
    [
        "Arial",
        "DejaVu Sans",
        "Liberation Sans",
        "Noto Sans",
        "Ubuntu",
        "Cantarell",
        "Segoe UI",
        "Helvetica"
    ];

    private static readonly Lazy<FontFamily> UiFamily = new(ResolveUiFamily);

    public static Font Create(float size)
    {
        return new Font(UiFamily.Value, size);
    }

    public static Font CreateFitting(string sampleText, float preferredSize, float minSize, float maxWidth, float step = 0.5f)
    {
        if (string.IsNullOrWhiteSpace(sampleText))
            throw new ArgumentException("Sample text is required to fit a font.", nameof(sampleText));
        if (preferredSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(preferredSize), "Preferred size must be positive.");
        if (minSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(minSize), "Minimum size must be positive.");
        if (preferredSize < minSize)
            throw new ArgumentOutOfRangeException(nameof(preferredSize), "Preferred size must be at least the minimum size.");
        if (maxWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxWidth), "Maximum width must be positive.");
        if (step <= 0)
            throw new ArgumentOutOfRangeException(nameof(step), "Step size must be positive.");

        for (var size = preferredSize; size >= minSize; size -= step)
        {
            var font = Create(size);
            if (MeasureWidth(sampleText, font) <= maxWidth)
                return font;
        }

        return Create(minSize);
    }

    internal static float MeasureWidth(string text, Font font)
    {
        return TextMeasurer.MeasureSize(text, new TextOptions(font)).Width;
    }

    private static FontFamily ResolveUiFamily()
    {
        foreach (var familyName in PreferredFontFamilies)
            try
            {
                return SystemFonts.Get(familyName);
            }
            catch (FontFamilyNotFoundException)
            {
                // Continue trying fallbacks.
            }

        var families = SystemFonts.Collection.Families;
        if (families.Any())
            return families.First();

        throw new InvalidOperationException(
            "No system fonts are available. Install at least one TTF/OTF font on the host.");
    }
}
