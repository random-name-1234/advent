using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public sealed class LegibilityLabScene : ISpecialScene
{
    public static readonly TimeSpan MaxSceneDuration = TimeSpan.FromSeconds(60);

    private const int Width = 64;
    private const int Height = 32;

    private static readonly TimeSpan SamplePageDuration = TimeSpan.FromSeconds(8);

    private static readonly Rgba32 BackgroundColor = new(0, 0, 0);
    private static readonly Rgba32 DividerColor = new(30, 18, 8);
    private static readonly Rgba32 PrimaryAmber = new(255, 186, 82);
    private static readonly Rgba32 DimAmber = new(156, 98, 34);
    private static readonly Rgba32 SoftAmber = new(214, 150, 62);
    private static readonly Rgba32 AlertRed = new(255, 100, 84);
    private static readonly Rgba32 CoolBlue = new(132, 186, 255);

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
            DrawHeader(img, "LEG LAB", "0/0");
            PixelText.DrawCentered(img, "NO PAGES", Width / 2, 13, PrimaryAmber);
            return;
        }

        var page = pages[Math.Clamp(pageIndex, 0, pages.Count - 1)];
        DrawHeader(img, page.Title, page.Badge);
        page.Render(img);
        DrawPageIndicators(img);
    }

    private static IReadOnlyList<LabPage> BuildPages()
    {
        return
        [
            new LabPage("BOARD", "2 ROW", SamplePageDuration, DrawTwoRowBoard),
            new LabPage("BOARD", "3 ROW", SamplePageDuration, DrawThreeRowBoard),
            new LabPage("DETAIL", "STATUS", SamplePageDuration, DrawDetailCard),
            new LabPage("ALERT", "WRAP", SamplePageDuration, DrawAlertCard),
            new LabPage("WX", "LABELS", SamplePageDuration, DrawWeatherCard)
        ];
    }

    private static void DrawTwoRowBoard(Image<Rgba32> img)
    {
        DrawBoardRow(img, 10, "07:25", "KGX", "P4", "ON TIME", PrimaryAmber, SoftAmber);
        FillRect(img, 1, 19, Width - 2, 1, DividerColor);
        DrawBoardRow(img, 20, "07:31", "LST", "P2", "+5", PrimaryAmber, SoftAmber);
    }

    private static void DrawThreeRowBoard(Image<Rgba32> img)
    {
        DrawDenseRow(img, 10, "07:25", "KGX", "P4");
        FillRect(img, 1, 15, Width - 2, 1, DividerColor);
        DrawDenseRow(img, 16, "07:31", "LST", "+5");
        FillRect(img, 1, 21, Width - 2, 1, DividerColor);
        DrawDenseRow(img, 22, "08:02", "PBO", "CAN");
    }

    private static void DrawDetailCard(Image<Rgba32> img)
    {
        RailDmiText.Draw(img, "07:25", 1, 10, PrimaryAmber);
        RailDmiText.DrawRightAligned(img, "P4", Width - 2, 10, PrimaryAmber);
        RailDmiText.DrawCentered(img, "LONDON KX", Width / 2, 16, PrimaryAmber);
        RailDmiText.Draw(img, "ON TIME", 1, 22, PrimaryAmber);
        RailDmiText.DrawRightAligned(img, "GN", Width - 2, 22, SoftAmber);
    }

    private static void DrawAlertCard(Image<Rgba32> img)
    {
        RailDmiText.DrawCentered(img, "SIGNAL FAIL", Width / 2, 10, AlertRed);
        RailDmiText.DrawCentered(img, "DELAYS ON", Width / 2, 16, PrimaryAmber);
        RailDmiText.DrawCentered(img, "CAM ROUTE", Width / 2, 22, PrimaryAmber);
    }

    private static void DrawWeatherCard(Image<Rgba32> img)
    {
        DrawSun(img, 4, 10, 10, CoolBlue);
        RailDmiText.Draw(img, "WED", 2, 1 + RailDmiText.Height + 4, PrimaryAmber);
        RailDmiText.DrawRightAligned(img, "NOW", Width - 2, 1 + RailDmiText.Height + 4, PrimaryAmber);
        RailDmiText.DrawCentered(img, "CLEAR", 43, 12, PrimaryAmber);
        RailDmiText.Draw(img, "HI 16C", 25, 18, PrimaryAmber);
        RailDmiText.Draw(img, "LO 09C", 25, 24, CoolBlue);
    }

    private static void DrawBoardRow(
        Image<Rgba32> img,
        int y,
        string time,
        string destination,
        string platform,
        string status,
        Rgba32 primaryColor,
        Rgba32 detailColor)
    {
        RailDmiText.Draw(img, time, 1, y, primaryColor);
        RailDmiText.Draw(img, destination, 25, y, primaryColor);
        RailDmiText.DrawRightAligned(img, platform, Width - 2, y, primaryColor);
        RailDmiText.Draw(img, status, 25, y + 5, detailColor);
    }

    private static void DrawDenseRow(Image<Rgba32> img, int y, string time, string destination, string right)
    {
        RailDmiText.Draw(img, time, 1, y, PrimaryAmber);
        RailDmiText.Draw(img, destination, 25, y, PrimaryAmber);
        RailDmiText.DrawRightAligned(img, right, Width - 2, y, SoftAmber);
    }

    private static void DrawHeader(Image<Rgba32> img, string title, string badge)
    {
        FillRect(img, 0, 7, Width, 1, DividerColor);
        PixelText.Draw(img, title, 2, 1, PrimaryAmber);

        var badgeWidth = PixelText.MeasureWidth(badge) + 4;
        var badgeX = Width - badgeWidth - 2;
        FillRect(img, badgeX, 1, badgeWidth, PixelText.Height, DividerColor);
        PixelText.Draw(img, badge, badgeX + 2, 1, PrimaryAmber);
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
            FillRect(img, startX + i * 4, Height - 1, 3, 1, color);
        }
    }

    private static void DrawSun(Image<Rgba32> img, int x, int y, int size, Rgba32 color)
    {
        var centerX = x + size / 2;
        var centerY = y + size / 2;

        FillRect(img, centerX - 1, y, 2, 2, color);
        FillRect(img, centerX - 1, y + size - 2, 2, 2, color);
        FillRect(img, x, centerY - 1, 2, 2, color);
        FillRect(img, x + size - 2, centerY - 1, 2, 2, color);

        FillRect(img, centerX - 2, centerY - 2, 5, 5, color);
    }

    private static void Clear(Image<Rgba32> img)
    {
        FillRect(img, 0, 0, Width, Height, BackgroundColor);
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
