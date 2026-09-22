using System.Net;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public sealed class FrameApiTests
{
    [Theory]
    [InlineData(64, 32)]
    [InlineData(128, 64)]
    public async Task PreviewKeepsAuthenticatedMetadataAndExactPixelScaling(int width, int height)
    {
        var directory = Directory.CreateTempSubdirectory("advent-frame-api-");
        try
        {
            var selector = new SceneSelector(9, imageSceneDirectory: directory.FullName);
            var control = new SceneControlService(new ScenePlaybackEngine(), selector, false);
            using var renderer = new SceneRenderer();
            renderer.Img[7, 9] = new Rgba32(203, 91, 32);
            var host = new AdventHostOptions(width, height, 20, 66, false, 9, directory.FullName);
            await using var app = ControlWebHost.Build(control, renderer,
                new WebControlOptions(true, "127.0.0.1", 0, "test-token"), host);
            await app.StartAsync();
            try
            {
                using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
                using var denied = await client.GetAsync("/api/frame/meta");
                Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
                client.DefaultRequestHeaders.Add("X-Advent-Token", "test-token");
                using var metadata = JsonDocument.Parse(await client.GetStringAsync("/api/frame/meta"));
                var root = metadata.RootElement;
                Assert.Equal(64, root.GetProperty("logicalWidth").GetInt32());
                Assert.Equal(32, root.GetProperty("logicalHeight").GetInt32());
                Assert.Equal(width, root.GetProperty("physicalWidth").GetInt32());
                Assert.Equal(height, root.GetProperty("physicalHeight").GetInt32());
                Assert.Equal(width / 64, root.GetProperty("horizontalScale").GetInt32());
                Assert.Equal(height / 32, root.GetProperty("verticalScale").GetInt32());
                using var image = Image.Load<Rgba32>(await client.GetByteArrayAsync("/api/frame"));
                Assert.Equal(width, image.Width);
                Assert.Equal(height, image.Height);
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    Assert.Equal(renderer.Img[x / (width / 64), y / (height / 32)], image[x, y]);
            }
            finally
            {
                await app.StopAsync();
            }
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
