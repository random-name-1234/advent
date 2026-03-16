using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public sealed class LegibilityLabScene : ISpecialScene
{
    public static readonly TimeSpan MaxSceneDuration = TimeSpan.FromSeconds(60);

    private const int Width = 64;
    private const int Height = 32;

    private static readonly TimeSpan SamplePageDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan LayoutPageDuration = TimeSpan.FromSeconds(8);

    private static readonly DrawingOptions CrispDrawingOptions = new()
    {
        GraphicsOptions = new GraphicsOptions
        {
            Antialias = false
        }
    };

    private static readonly Font HeaderFont = AppFonts.Create(8f);
    private static readonly Font FooterFont = AppFonts.Create(5.25f);
    private static readonly Rgba32 BackgroundColor = new(0, 0, 0);
    private static readonly Rgba32 DividerColor = new(30, 18, 8);
    private static readonly Rgba32 PrimaryAmber = new(255, 186, 82);
    private static readonly Rgba32 DimAmber = new(156, 98, 34);
    private static readonly Rgba32 SoftAmber = new(214, 150, 62);

    private readonly List<LabPage> pages = [];
    private TimeSpan elapsedOnPage;
    private int pageIndex;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Legibility Lab";

    public void Activate()
    {
        pages.Clear();
        pages.AddRange(BuildPages());
        elapsedOnPage = TimeSpan.Zero;
        pageIndex = 0;
        IsActive = true;
        HidesTime = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        if (pages.Count == 0)
        {
            IsActive = false;
            HidesTime = false;
            return;
        }

        elapsedOnPage += timeSpan;
        while (pageIndex < pages.Count && elapsedOnPage >= pages[pageIndex].Duration)
        {
            elapsedOnPage -= pages[pageIndex].Duration;
            pageIndex++;

            if (pageIndex >= pages.Count)
            {
                IsActive = false;
                HidesTime = false;
                return;
            }
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive)
            return;

        Clear(img);
        if (pages.Count == 0)
        {
            DrawHeader(img, "LEGIBILITY", "0/0");
            DrawCenteredText(img, "NO PAGES", FooterFont, PrimaryAmber, Width / 2, 14);
            return;
        }

        var page = pages[Math.Clamp(pageIndex, 0, pages.Count - 1)];
        DrawHeader(img, page.Title, page.Badge);
        page.Render(img);
        DrawPageIndicators(img);
    }

    private static IReadOnlyList<LabPage> BuildPages()
    {
        var defaultFamily = AppFonts.Create(6f).Family;
        var pages = new List<LabPage>
        {
            CreateSizePage(defaultFamily, 5.5f),
            CreateSizePage(defaultFamily, 6.0f),
            CreateSizePage(defaultFamily, 6.5f),
            CreateLayoutPage(defaultFamily, oneLine: true),
            CreateLayoutPage(defaultFamily, oneLine: false),
            CreateColorPage(defaultFamily, "AMBER 75", Scale(PrimaryAmber, 0.75f)),
            CreateColorPage(defaultFamily, "AMBER 100", PrimaryAmber)
        };

        foreach (var family in ResolveComparisonFamilies(defaultFamily).Take(2))
            pages.Add(CreateFontPage(family));

        return pages;
    }

    private static IEnumerable<FontFamily> ResolveComparisonFamilies(FontFamily defaultFamily)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            defaultFamily.Name
        };

        foreach (var familyName in new[]
                 {
                     "DejaVu Sans",
                     "Liberation Sans",
                     "Noto Sans",
                     "Cantarell",
                     "Arial",
                     "Helvetica"
                 })
        {
            FontFamily family;
            try
            {
                family = SystemFonts.Get(familyName);
            }
            catch (FontFamilyNotFoundException)
            {
                continue;
            }

            if (seen.Add(family.Name))
                yield return family;
        }
    }

    private static LabPage CreateSizePage(FontFamily family, float bodySize)
    {
        var label = bodySize.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        return new LabPage(
            $"BODY {label}",
            $"{ShortFamilyName(family.Name)} 2L",
            SamplePageDuration,
            img => DrawTwoLineBoard(img, family, bodySize, PrimaryAmber));
    }

    private static LabPage CreateLayoutPage(FontFamily family, bool oneLine)
    {
        return new LabPage(
            oneLine ? "LAYOUT 1" : "LAYOUT 2",
            oneLine ? "1-LINE" : "2-LINE",
            LayoutPageDuration,
            img =>
            {
                if (oneLine)
                    DrawOneLineBoard(img, family, 5.5f, PrimaryAmber);
                else
                    DrawTwoLineBoard(img, family, 6.0f, PrimaryAmber);
            });
    }

    private static LabPage CreateColorPage(FontFamily family, string title, Rgba32 primaryColor)
    {
        return new LabPage(
            title,
            $"{ShortFamilyName(family.Name)} 6.0",
            SamplePageDuration,
            img => DrawTwoLineBoard(img, family, 6.0f, primaryColor));
    }

    private static LabPage CreateFontPage(FontFamily family)
    {
        return new LabPage(
            $"FONT {ShortFamilyName(family.Name)}",
            "6.0 2L",
            SamplePageDuration,
            img => DrawTwoLineBoard(img, family, 6.0f, PrimaryAmber));
    }

    private static void DrawTwoLineBoard(Image<Rgba32> img, FontFamily family, float bodySize, Rgba32 primaryColor)
    {
        var rowFont = new Font(family, bodySize);
        var detailFont = new Font(family, Math.Max(5f, bodySize - 1f));

        DrawServiceBlock(img, rowFont, detailFont, primaryColor, 10, "07:25", "KGX", "P4", "ON TIME");
        FillRect(img, 1, 18, Width - 2, 1, DividerColor);
        DrawServiceBlock(img, rowFont, detailFont, primaryColor, 20, "07:31", "LIV ST", "P2", "+5 DELAY");
    }

    private static void DrawServiceBlock(
        Image<Rgba32> img,
        Font rowFont,
        Font detailFont,
        Rgba32 primaryColor,
        int topY,
        string time,
        string destination,
        string platform,
        string status)
    {
        DrawText(img, time, rowFont, primaryColor, 2, topY);
        DrawText(img, FitText(destination, rowFont, 22), rowFont, primaryColor, 19, topY);

        var platformSize = TextMeasurer.MeasureSize(platform, new TextOptions(rowFont));
        var platformX = Width - (int)MathF.Ceiling(platformSize.Width) - 2;
        DrawText(img, platform, rowFont, primaryColor, platformX, topY);

        DrawText(img, status, detailFont, SoftAmber, 19, topY + 6);
    }

    private static void DrawOneLineBoard(Image<Rgba32> img, FontFamily family, float bodySize, Rgba32 primaryColor)
    {
        var rowFont = new Font(family, bodySize);
        DrawOneLineRow(img, rowFont, primaryColor, 10, "07:25", "KGX", "P4", "ON");
        FillRect(img, 1, 15, Width - 2, 1, DividerColor);
        DrawOneLineRow(img, rowFont, primaryColor, 17, "07:31", "LIV", "P2", "+5");
        FillRect(img, 1, 22, Width - 2, 1, DividerColor);
        DrawOneLineRow(img, rowFont, primaryColor, 24, "08:02", "PBO", "P1", "CAN");
    }

    private static void DrawOneLineRow(
        Image<Rgba32> img,
        Font rowFont,
        Rgba32 primaryColor,
        int y,
        string time,
        string destination,
        string platform,
        string status)
    {
        DrawText(img, time, rowFont, primaryColor, 2, y);
        DrawText(img, destination, rowFont, primaryColor, 21, y);
        DrawText(img, platform, rowFont, primaryColor, 41, y);
        DrawText(img, status, rowFont, primaryColor, 53, y);
    }

    private static string FitText(string text, Font font, int maxWidth)
    {
        if (TextMeasurer.MeasureSize(text, new TextOptions(font)).Width <= maxWidth)
            return text;

        var trimmed = text;
        while (trimmed.Length > 3 &&
               TextMeasurer.MeasureSize(trimmed, new TextOptions(font)).Width > maxWidth)
            trimmed = trimmed[..^1];

        return trimmed;
    }

    private static string ShortFamilyName(string familyName)
    {
        return familyName.Trim().ToUpperInvariant() switch
        {
            "DEJAVU SANS" => "DEJAVU",
            "LIBERATION SANS" => "LIB SANS",
            "NOTO SANS" => "NOTO",
            "CANTARELL" => "CANTARELL",
            "ARIAL" => "ARIAL",
            "HELVETICA" => "HELV",
            _ => familyName.Trim().ToUpperInvariant()
        };
    }

    private static void DrawHeader(Image<Rgba32> img, string title, string badge)
    {
        FillRect(img, 0, 0, Width, 8, BackgroundColor);
        FillRect(img, 0, 8, Width, 1, DividerColor);
        DrawText(img, title, HeaderFont, PrimaryAmber, 2, -1);

        var badgeSize = TextMeasurer.MeasureSize(badge, new TextOptions(FooterFont));
        var badgeWidth = (int)MathF.Ceiling(badgeSize.Width) + 4;
        var badgeX = Width - badgeWidth - 2;
        FillRect(img, badgeX, 1, badgeWidth, 5, DividerColor);
        DrawText(img, badge, FooterFont, PrimaryAmber, badgeX + 2, 0);
    }

    private void DrawPageIndicators(Image<Rgba32> img)
    {
        if (pages.Count <= 1)
            return;

        var totalWidth = pages.Count * 4 - 1;
        var startX = (Width - totalWidth) / 2;
        for (var i = 0; i < pages.Count; i++)
        {
            var color = i == pageIndex ? PrimaryAmber : DimAmber;
            FillRect(img, startX + i * 4, Height - 2, 3, 1, color);
        }
    }

    private static void DrawCenteredText(Image<Rgba32> img, string text, Font font, Rgba32 color, int centerX, int y)
    {
        var size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        var left = Math.Clamp(centerX - size.Width / 2f, 2f, Width - size.Width - 2f);
        DrawText(img, text, font, color, (int)MathF.Round(left), y);
    }

    private static void DrawText(Image<Rgba32> img, string text, Font font, Rgba32 color, int x, int y)
    {
        img.Mutate(ctx => ctx.DrawText(CrispDrawingOptions, text, font, color, new PointF(x, y)));
    }

    private static void Clear(Image<Rgba32> img)
    {
        FillRect(img, 0, 0, Width, Height, BackgroundColor);
    }

    private static Rgba32 Scale(Rgba32 color, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(
            (byte)Math.Clamp((int)Math.Round(color.R * clamped), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.G * clamped), 0, 255),
            (byte)Math.Clamp((int)Math.Round(color.B * clamped), 0, 255));
    }

    private static void FillRect(Image<Rgba32> img, int x, int y, int width, int height, Rgba32 color)
    {
        for (var yy = y; yy < y + height; yy++)
        for (var xx = x; xx < x + width; xx++)
            SetPixel(img, xx, yy, color);
    }

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return;

        img[x, y] = color;
    }

    private sealed record LabPage(string Title, string Badge, TimeSpan Duration, Action<Image<Rgba32>> Render);
}
