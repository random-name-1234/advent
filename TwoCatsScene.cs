using advent.Data.Home;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class TwoCatsScene(IHomeSnapshotSource source, TimeProvider? timeProvider = null) : PixelStoryScene("Two Cats")
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private HomeSnapshot? snapshot;
    private readonly HashSet<HomeCat> recentTransitions = [];
    private static readonly TabbyCoat Barney = new(
        Color(159, 168, 174), Color(62, 71, 80), Color(230, 233, 225), Color(230, 233, 225),
        Color(203, 188, 116), Color(205, 217, 220), Fluffy: true);
    private static readonly TabbyCoat Beaker = new(
        Color(133, 104, 69), Color(31, 30, 28), Color(185, 173, 147), Color(88, 72, 52),
        Color(158, 174, 112), Color(207, 176, 130), Fluffy: false);

    public override void Activate()
    {
        base.Activate();
        IsActive = source.TryGetSnapshot(out snapshot!) && snapshot.PetsReady(clock.GetUtcNow());
        recentTransitions.Clear();
        if (!IsActive) return;
        var now = clock.GetUtcNow();
        foreach (var cat in snapshot!.Cats)
            if (cat.PreviousLocation != CatLocation.Unknown && cat.PreviousLocation != cat.Location &&
                cat.ChangedAt is { } changed && now >= changed && now - changed <= TimeSpan.FromMinutes(2))
                recentTransitions.Add(cat);
    }

    protected override void Render(Image<Rgba32> image)
    {
        Box(image, 0, 0, 64, 32, Color(3, 7, 11));
        snapshot = source.TryGetSnapshot(out var latest) ? latest : null;
        if (snapshot is null || !snapshot.PetsReady(clock.GetUtcNow()))
        {
            Text(image, "CATS", 8, Color(166, 182, 185));
            Text(image, "NO LIVE DATA", 19, Color(166, 182, 185));
            return;
        }
        DrawCatPanel(image, 0, "BARNEY", snapshot.Cats.FirstOrDefault(c => c.Id == "barney"), Barney);
        DrawCatPanel(image, 33, "BEAKER", snapshot.Cats.FirstOrDefault(c => c.Id == "beaker"), Beaker);
        Box(image, 31, 0, 1, 32, Color(22, 36, 40));
    }

    private void DrawCatPanel(Image<Rgba32> image, int left, string name, HomeCat? cat, TabbyCoat coat)
    {
        RailDmiText.Draw(image, name, left + (31 - RailDmiText.MeasureWidth(name)) / 2, 1, coat.Label);
        var location = cat?.Location ?? CatLocation.Unknown;
        if (location == CatLocation.Unknown)
        {
            RailDmiText.Draw(image, "?", left + 13, 11, Color(120, 139, 147), 2);
            RailDmiText.Draw(image, "NO FIX", left + (31 - RailDmiText.MeasureWidth("NO FIX")) / 2, 25, Color(128, 151, 157));
            return;
        }
        var indoors = location == CatLocation.Indoors;
        Box(image, left + 1, 8, 29, 16, indoors ? Color(49, 40, 35) : Color(8, 28, 27));
        Box(image, left + 1, 22, 29, 2, indoors ? Color(101, 63, 39) : Color(35, 70, 40));
        if (indoors)
        {
            Box(image, left + 3, 9, 6, 6, Color(99, 121, 121));
            Box(image, left + 6, 9, 1, 6, Color(43, 37, 31));
            Box(image, left + 3, 12, 6, 1, Color(43, 37, 31));
        }
        else
        {
            Box(image, left + 2, 17, 27, 1, Color(57, 74, 59));
            for (var x = left + 3; x < left + 30; x += 6) Box(image, x, 15, 1, 8, Color(57, 74, 59));
        }
        var moving = cat is not null && recentTransitions.Contains(cat) && Seconds < 4;
        var cx = left + CatCenter(indoors, moving, Seconds);
        if (moving)
        {
            Box(image, left + 1, 16, 6, 8, Color(15, 17, 19));
            Box(image, left + 2, 16, 4, 1, Color(177, 155, 116));
        }
        Cat(image, cx, 17, coat, Seconds + (left == 0 ? 0 : 1.7), moving);
        var label = indoors ? "IN" : "OUT";
        RailDmiText.Draw(image, label, left + (31 - RailDmiText.MeasureWidth(label)) / 2, 25,
            indoors ? Color(131, 195, 158) : Color(147, 187, 211));
    }

    internal static int CatCenter(bool indoors, bool moving, double seconds)
    {
        var rest = indoors ? 18 : 10;
        return moving ? (int)Math.Round(indoors ? 10 + Math.Clamp(seconds, 0, 4) * 2 :
            18 - Math.Clamp(seconds, 0, 4) * 2) : rest;
    }

    private static void Cat(Image<Rgba32> image, int x, int y, TabbyCoat coat, double t, bool moving)
    {
        var tailX = x - 7 + (int)Math.Round(Math.Sin(t * 1.1));
        Box(image, tailX, y - 1, coat.Fluffy ? 3 : 2, 6, coat.Fur);
        Box(image, tailX, y + 1, coat.Fluffy ? 3 : 2, 1, coat.Stripes);
        Box(image, tailX, y + 3, coat.Fluffy ? 3 : 2, 1, coat.Stripes);
        Box(image, x - 4, y - 1, 10, 6, coat.Fluffy ? coat.Fur : coat.Stripes);
        Box(image, x - 5, y + 1, 2, 3, coat.Fur);
        for (var stripe = 0; stripe < 3; stripe++)
            Box(image, x - 4 + stripe * 3, y + stripe % 2, 1, 3, coat.Fluffy ? coat.Stripes : coat.Fur);

        // The broad white ruff distinguishes Barney; Beaker keeps a dark chest.
        Box(image, x + 1, y - 1, 5, 5, coat.Bib);
        Box(image, x + 2, y + 4, 3, 1, coat.Bib);
        if (coat.Fluffy)
        {
            Dot(image, x, y + 1, coat.Bib);
            Dot(image, x + 6, y, coat.Bib);
            Dot(image, x + 6, y + 2, coat.Bib);
        }

        Box(image, x, y - 6, 8, 6, coat.Fur);
        Box(image, x + 1, y - 7, 6, 1, coat.Fur);
        Dot(image, x, y - 8, coat.Fur);
        Box(image, x, y - 7, 2, 2, coat.Fur);
        Dot(image, x + 7, y - 8, coat.Fur);
        Box(image, x + 6, y - 7, 2, 2, coat.Fur);
        Dot(image, x + 1, y - 6, Color(128, 103, 97));
        Dot(image, x + 6, y - 6, Color(128, 103, 97));
        Box(image, x + 3, y - 7, 2, 2, coat.Stripes);
        Dot(image, x + 2, y - 5, coat.Stripes);
        Dot(image, x + 5, y - 5, coat.Stripes);
        if (coat.Fluffy)
        {
            Box(image, x - 1, y - 3, 1, 3, coat.Fur);
            Box(image, x + 8, y - 3, 1, 3, coat.Fur);
        }
        Box(image, x + 1, y - 4, 2, 1, coat.Stripes);
        Box(image, x + 5, y - 4, 2, 1, coat.Stripes);
        if ((int)(t * 2) % 13 != 0)
        {
            Dot(image, x + 2, y - 4, coat.Eyes);
            Dot(image, x + 5, y - 4, coat.Eyes);
        }
        Box(image, x + 2, y - 2, 4, 2, coat.Muzzle);
        Dot(image, x + 3, y - 2, Color(166, 115, 106));
        Dot(image, x + 4, y - 1, coat.Stripes);
        Dot(image, x, y - 2, coat.Stripes);
        Dot(image, x + 7, y - 2, coat.Stripes);
        var step = moving ? (int)(t * 5) % 2 : 0;
        Box(image, x - 3 + step, y + 5, 2, 1, coat.Fluffy ? coat.Bib : coat.Fur);
        Box(image, x + 3 - step, y + 5, 2, 1, coat.Fluffy ? coat.Bib : coat.Fur);
    }

    private sealed record TabbyCoat(Rgba32 Fur, Rgba32 Stripes, Rgba32 Muzzle, Rgba32 Bib,
        Rgba32 Eyes, Rgba32 Label, bool Fluffy);
}
