using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class AquariumScene() : PixelStoryScene("Aquarium")
{
    protected override void Render(Image<Rgba32> image)
    {
        Sky(image, Color(2, 19, 35), Color(1, 7, 15));
        for (var x = 0; x < 64; x++)
        {
            Dot(image, x, 1 + (int)(Math.Sin(x * .25 + Seconds) + 1), Color(8, 42, 56));
            Box(image, x, 29 + (x % 7 == 0 ? 1 : 0), 1, 3, Color(57 + x % 5 * 3, 49, 34));
        }
        for (var stem = 0; stem < 7; stem++)
        {
            var root = stem < 4 ? 3 + stem * 3 : 50 + (stem - 4) * 4;
            var height = 8 + stem % 3 * 3;
            for (var h = 0; h < height; h++)
            {
                var x = root + (int)(Math.Sin(Seconds * .7 + stem + h * .25) * h / 7);
                Dot(image, x, 29 - h, Color(17, 77 + stem * 5, 58));
                if (h % 3 == 0) Dot(image, x + (h % 2 == 0 ? 1 : -1), 29 - h, Color(29, 120, 74));
            }
        }
        Fish(image, Wrap(9 + Seconds * 2.4, 82) - 9, 10 + (int)Math.Sin(Seconds * .9), 1, Color(247, 154, 53), Seconds);
        Fish(image, 70 - Wrap(24 + Seconds * 1.7, 82), 18 + (int)Math.Sin(Seconds * .6), -1, Color(77, 182, 192), Seconds + 1);
        Fish(image, Wrap(42 + Seconds * 1.3, 82) - 9, 6, 1, Color(193, 107, 91), Seconds + 2);
        for (var bubble = 0; bubble < 3; bubble++)
        {
            var y = 28 - Wrap(Seconds * 2 + bubble * 11, 32);
            var x = 43 + (int)Math.Sin(Seconds + bubble * 2);
            Dot(image, x, y, Color(75, 121, 142));
            if (bubble == 0) Dot(image, x + 1, y - 1, Color(22, 56, 76));
        }
    }

    private static void Fish(Image<Rgba32> image, int x, int y, int direction, Rgba32 color, double t)
    {
        Box(image, x - 3, y - 1, 6, 3, color);
        Box(image, x - 1, y - 2, 3, 1, Mix(color, Color(255, 240, 180), .3));
        var tail = x - direction * 4;
        Dot(image, tail, y, color);
        var flutter = (int)(t * 3) % 2;
        Box(image, tail - direction, y - 1 + flutter, 1, 3 - flutter, color);
        Dot(image, x + direction * 2, y - 1, Color(6, 11, 17));
        Dot(image, x, y + 1, Mix(color, Color(4, 18, 24), .5));
    }
}
