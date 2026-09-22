using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class NightTrainScene() : PixelStoryScene("Night Train")
{
    internal const int CoachLength = 30;
    internal const int TrainLength = CoachLength * 3;
    private static readonly Rgba32 Silver = Color(167, 185, 191);
    private static readonly Rgba32 Blue = Color(28, 67, 111);
    private static readonly Rgba32 CabYellow = Color(236, 197, 56);
    private static readonly Rgba32 Window = Color(231, 211, 149);

    internal static double FrontPosition(double seconds)
    {
        var approach = Math.Clamp((seconds - 3) / 6, 0, 1);
        var front = 72 - 57 * (1 - Math.Pow(1 - approach, 2));
        return seconds <= 15 ? front : front - Math.Pow(seconds - 15, 2) * 5;
    }

    internal static bool DoorsOpen(double seconds) => seconds is >= 9.5 and < 13.5;
    internal static bool DepartureSignalClear(double seconds) => seconds is >= 14 and < 17;

    protected override void Render(Image<Rgba32> image)
    {
        Sky(image, Color(2, 4, 14), Color(13, 24, 36));
        foreach (var (x, y) in new[] { (4, 3), (25, 1), (39, 4), (57, 2) })
            Dot(image, x, y, Color(94, 120, 141));
        Disc(image, 48, 3, 1, Color(172, 181, 153));
        DrawStation(image);

        // The pantograph meets the contact wire at y=17.
        Box(image, 0, 17, 64, 1, Color(56, 72, 87));
        Box(image, 62, 5, 1, 22, Color(75, 91, 102));
        Box(image, 53, 6, 10, 1, Color(75, 91, 102));
        Box(image, 54, 7, 1, 10, Color(59, 76, 87));
        Box(image, 0, 25, 64, 2, Color(116, 121, 115));
        Box(image, 0, 25, 64, 1, Color(197, 180, 104));
        Box(image, 0, 27, 64, 1, Color(52, 59, 62));
        Box(image, 0, 31, 64, 1, Color(89, 103, 113));
        for (var x = 2; x < 64; x += 5) Box(image, x, 29, 2, 2, Color(54, 50, 45));

        // A platform starting signal sits ahead of this leftward-moving train.
        Box(image, 3, 16, 1, 10, Color(87, 99, 106));
        Box(image, 1, 10, 5, 8, Color(4, 7, 11));
        var clear = DepartureSignalClear(Seconds);
        Box(image, 3, 12, 1, 2, clear ? Color(54, 227, 100) : Color(14, 46, 30));
        Box(image, 3, 15, 1, 2, clear ? Color(61, 19, 18) : Color(237, 49, 36));

        var front = (int)Math.Round(FrontPosition(Seconds));
        if (Seconds >= 3 && front + TrainLength >= 0) DrawTrain(image, front, DoorsOpen(Seconds));
    }

    private static void DrawStation(Image<Rgba32> image)
    {
        Box(image, 9, 10, 41, 15, Color(78, 45, 35));
        for (var y = 14; y < 24; y += 3)
        for (var x = 10 + y % 2 * 3; x < 50; x += 6)
            Box(image, x, y, 3, 1, Color(101, 61, 42));
        Box(image, 7, 8, 46, 2, Color(41, 76, 70));
        Box(image, 7, 10, 46, 1, Color(153, 174, 152));
        for (var x = 8; x < 53; x += 2) Dot(image, x, 11, Color(130, 153, 134));
        Box(image, 10, 17, 1, 8, Color(114, 146, 126));
        Box(image, 48, 17, 1, 8, Color(114, 146, 126));
        Box(image, 14, 19, 6, 5, Color(187, 157, 86));
        Box(image, 17, 19, 1, 5, Color(70, 61, 43));
        Box(image, 31, 21, 9, 2, Color(38, 66, 58));
        Box(image, 32, 23, 1, 2, Color(88, 103, 94));
        Box(image, 38, 23, 1, 2, Color(88, 103, 94));
    }

    private static void DrawTrain(Image<Rgba32> image, int front, bool doorsOpen)
    {
        for (var coach = 0; coach < 3; coach++)
        {
            var x = front + coach * CoachLength;
            Box(image, x + 2, 19, 27, 1, Color(105, 126, 139));
            Box(image, x + 1, 20, 28, 8, Silver);
            Box(image, x + 2, 21, 26, 4, Color(25, 46, 63));
            Box(image, x + 1, 26, 28, 2, Blue);
            Box(image, x + 3, 28, 24, 1, Color(51, 68, 82));
            for (var window = 0; window < 3; window++)
                Box(image, x + 11 + window * 5, 22, 4, 2, Window);
            Box(image, x + 6, 21, 4, 7, Blue);
            if (doorsOpen) Box(image, x + 7, 22, 2, 5, Color(245, 217, 155));
            else
            {
                Box(image, x + 7, 22, 2, 2, Color(114, 153, 173));
                Box(image, x + 8, 25, 1, 3, Color(12, 34, 65));
            }
            Box(image, x + 4, 29, 4, 1, Color(6, 11, 17));
            Box(image, x + 23, 29, 4, 1, Color(6, 11, 17));
            if (coach < 2) Box(image, x + 29, 21, 1, 7, Color(41, 51, 62));
        }

        // Sloping yellow cab and dark windscreen, not a separate locomotive.
        Box(image, front, 22, 3, 6, CabYellow);
        Box(image, front + 1, 21, 2, 2, CabYellow);
        Box(image, front + 2, 20, 3, 1, Silver);
        Box(image, front + 2, 21, 3, 3, Color(13, 32, 47));
        Dot(image, front + 2, 21, Color(99, 141, 158));
        Dot(image, front, 26, Color(253, 249, 214));
        Dot(image, front + 1, 28, Color(35, 50, 62));

        var rear = front + TrainLength - 1;
        Box(image, rear - 2, 22, 3, 6, CabYellow);
        Box(image, rear - 4, 21, 3, 3, Color(13, 32, 47));
        Dot(image, rear, 26, Color(225, 43, 36));

        var pantograph = front + 39;
        Box(image, pantograph - 2, 17, 5, 1, Color(115, 127, 133));
        Dot(image, pantograph, 18, Color(115, 127, 133));
        Box(image, pantograph - 2, 19, 5, 1, Color(68, 89, 105));
    }
}
