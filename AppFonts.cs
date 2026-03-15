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
