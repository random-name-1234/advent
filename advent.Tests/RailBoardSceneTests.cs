using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class RailBoardSceneTests
{
    [Fact]
    public void RailBoardScene_UsesProvidedSnapshot_AndExpires()
    {
        var scene = new RailBoardScene(
            static (_, _) => Task.FromResult(
                new RailBoardScene.RailBoardSnapshot(
                    [
                        CreatePanel("DEP"),
                        CreatePanel("ARR")
                    ],
                    new DateTimeOffset(2026, 3, 16, 18, 42, 0, TimeSpan.Zero))));

        scene.Activate();
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);

        scene.Elapsed(TimeSpan.FromMilliseconds(50));
        using var canvas = new Image<Rgba32>(64, 32);
        scene.Draw(canvas);

        Assert.NotEqual(new Rgba32(0, 0, 0, 0), canvas[5, 5]);

        scene.Elapsed(TimeSpan.FromSeconds(21));

        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
    }

    private static RailBoardScene.RailBoardPanel CreatePanel(string board)
    {
        return new RailBoardScene.RailBoardPanel(
            board,
            [
                CreateSection("CBG"),
                CreateSection("KGX")
            ]);
    }

    private static RailBoardScene.RailStationSection CreateSection(string station)
    {
        return new RailBoardScene.RailStationSection(
            station,
            [
                new RailBoardScene.RailServiceRow("09:58", "KGX", "P1", new Rgba32(180, 230, 170), DateTimeOffset.Parse("2026-03-16T09:58:00+00:00")),
                new RailBoardScene.RailServiceRow("10:04", "LST", "+5", new Rgba32(255, 210, 120), DateTimeOffset.Parse("2026-03-16T10:04:00+00:00"))
            ],
            false);
    }
}
