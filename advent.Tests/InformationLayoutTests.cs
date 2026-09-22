using advent.Data.Weather;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace advent.Tests;

public class InformationLayoutTests
{
    [Fact]
    public void MessagesPreferSentenceBoundariesWithoutAddingPagesOrDroppingLines()
    {
        string[] lines = ["FIRST", "SENTENCE.", "ANOTHER", "LONG", "SENTENCE."];
        var pages = MessageLayout.BalancePages(lines);
        Assert.Equal(2, pages.Length);
        Assert.Equal(new[] { "FIRST", "SENTENCE." }, pages[0]);
        Assert.Equal(lines, pages.SelectMany(page => page));
        for (var count = 1; count <= 30; count++)
        {
            var input = Enumerable.Range(0, count).Select(i => i % 2 == 0 ? "END." : "WORDS").ToArray();
            pages = MessageLayout.BalancePages(input);
            Assert.Equal((count + 2) / 3, pages.Length);
            Assert.All(pages, page => Assert.InRange(page.Length, 1, 3));
            Assert.Equal(input, pages.SelectMany(page => page));
        }
    }

    [Fact]
    public void Weather_AllConditionsAndExtremeValues_HaveDisjointMeasuredRegions()
    {
        foreach (var code in new[] { 0, 1, 2, 3, 45, 48, 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, 99, -1 })
        foreach (var value in new[] { -99f, -12, 0, 12, 99, float.NaN, float.PositiveInfinity })
        foreach (var panel in new[] { 0, 1, 2 })
        {
            var weather = WeatherFixture(value, code, 100, 999);
            var layout = WeatherScene.BuildPanelLayout(weather, panel, TimeSpan.FromSeconds(2));
            var bounds = layout.Text.Select(run => run.Bounds).Append(layout.IconBounds).Append(layout.TemperatureBounds).ToArray();
            AssertFits(bounds);
            foreach (var run in layout.Text)
                Assert.All(run.Text, character => Assert.True(RailDmiText.HasGlyph(character), $"Missing {character} in {run.Text}"));
        }
    }

    [Fact]
    public void Weather_KeepsFullConditionAndMargins_AndShowsUnknownValuesHonestly()
    {
        var weather = WeatherFixture();
        var layout = WeatherScene.BuildPanelLayout(weather, 0, TimeSpan.Zero);
        Assert.Contains(layout.Text, run => run.Text == "PARTLY");
        Assert.Contains(layout.Text, run => run.Text == "20%");
        Assert.Equal("12\u00b0C", layout.Temperature);
        var night = WeatherScene.BuildPanelLayout(weather with { IsDay = false }, 0, TimeSpan.Zero);
        Assert.Contains(night.Text, run => run.Text == "PARTLY");
        Assert.DoesNotContain(night.Text, run => run.Text.Contains("SUN"));

        using var image = new Image<Rgba32>(64, 32, Color.White);
        WeatherScene.DrawPanel(image, weather, 0, TimeSpan.Zero);
        for (var x = 0; x < 64; x++)
        {
            Assert.Equal(new Rgba32(0, 0, 0), image[x, 0]);
            Assert.Equal(new Rgba32(0, 0, 0), image[x, 31]);
        }
        var missing = WeatherScene.BuildPanelLayout(WeatherFixture(float.NaN, -1, -1, float.NaN), 0, TimeSpan.Zero);
        Assert.Equal("--\u00b0C", missing.Temperature);
        Assert.Contains(missing.Text, run => run.Text == "--%");
        Assert.Contains(missing.Text, run => run.Text == "--MPH");
    }

    [Theory]
    [InlineData("HI")]
    [InlineData("Hello Alan")]
    [InlineData("Good morning everyone")]
    public void ShortMessages_AreVisibleImmediately_AndDoNotMove(string text)
    {
        var scene = new MessageScene(text);
        scene.Activate();
        using var first = new Image<Rgba32>(64, 32);
        using var later = new Image<Rgba32>(64, 32);
        scene.Draw(first);
        scene.Elapsed(TimeSpan.FromSeconds(2));
        scene.Draw(later);
        Assert.Contains(Enumerable.Range(0, 64 * 32), index => first[index % 64, index / 64].R != 0);
        Assert.Equal(Pixels(first), Pixels(later));
        AssertFits(scene.Layout.TextRuns(TimeSpan.Zero, Color.White).Select(run => run.Bounds).ToArray());
    }

    [Fact]
    public void Messages_PageWithoutLosingWords_AndHoldForAtLeastFourSeconds()
    {
        const string text = "Train to London Kings Cross is delayed by twenty minutes. Please check the platform before boarding.";
        var scene = new MessageScene(text);
        var layout = scene.Layout;
        Assert.True(layout.Pages.Count > 1);
        Assert.True(layout.Duration.TotalSeconds / layout.Pages.Count >= 4);
        Assert.True(layout.Duration <= TimeSpan.FromSeconds(20));
        Assert.Equal(MatrixTextLayout.Normalize(text), string.Join(' ', layout.Pages.SelectMany(page => page)));
        for (var page = 0; page < layout.Pages.Count; page++)
        {
            var time = TimeSpan.FromSeconds(page * 4);
            Assert.Equal(page, layout.PageAt(time));
            AssertFits(layout.TextRuns(time, Color.White).Select(run => run.Bounds).ToArray());
        }
        scene.Activate();
        scene.Elapsed(layout.Duration);
        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);
        scene.Activate();
        Assert.True(scene.IsActive);
    }

    [Fact]
    public void Messages_DoNotSplitWordsJustToUseLargerLetters()
    {
        Assert.Equal(2, MessageLayout.Create("HELLO ALAN", null).Scale);
        Assert.Equal(1, MessageLayout.Create("MORNING", null).Scale);
        var text = new string('W', 120);
        var layout = MessageLayout.Create(text, null);
        Assert.Equal(text, string.Concat(layout.Pages.SelectMany(page => page)));
        foreach (var page in layout.Pages)
            Assert.All(page, line => Assert.InRange(RailDmiText.MeasureWidth(line), 1, 60));
    }

    [Fact]
    public void Messages_RejectUnreadableDurationsAndOverflow_InsteadOfSpeedingUp()
    {
        const string text = "The train to London Kings Cross has been cancelled. Please check before travelling.";
        Assert.False(MessageLayout.TryCreate(text, TimeSpan.FromSeconds(2), out _, out var error));
        Assert.Contains("at least", error);
        Assert.False(MessageLayout.TryCreate(string.Join(' ', Enumerable.Repeat("WWWWWW", 17)), null, out _, out error));
        Assert.Contains("split", error);
        Assert.True(MessageLayout.TryCreate("HI", TimeSpan.FromSeconds(1), out _, out _));
        Assert.False(MessageLayout.TryCreate("HI", TimeSpan.FromMilliseconds(500), out _, out _));
        Assert.False(MessageLayout.TryCreate(new string('X', 121), null, out _, out _));
        Assert.False(MessageLayout.TryCreate("  ", null, out _, out _));
    }

    [Fact]
    public void Normalization_HandlesAccentsSymbolsAndUnknownCharactersExplicitly()
    {
        Assert.Equal("CAFE & TEA 20% = 2\u00b0C ?", MatrixTextLayout.Normalize("Caf\u00e9 & tea 20% = 2\u00b0C \U0001f682"));
        Assert.Equal("ALAN'S TRAIN - LATE!", MatrixTextLayout.Normalize("Alan\u2019s train \u2014 late!"));
        Assert.Equal("ONE TWO THREE", MatrixTextLayout.Normalize("One\r\nTwo\tThree"));
    }

    [Fact]
    public void PercentGlyph_IsNotAQuestionMark_EvenWithoutTheFontAsset()
    {
        using var percent = new Image<Rgba32>(64, 32);
        using var question = new Image<Rgba32>(64, 32);
        using var fallback = new Image<Rgba32>(64, 32);
        RailDmiText.Draw(percent, "100%", 2, 2, Color.White);
        RailDmiText.Draw(question, "100?", 2, 2, Color.White);
        RailDmiText.CreateFallbackFont().Draw(fallback, "100%", 2, 2, Color.White);
        Assert.NotEqual(Pixels(percent), Pixels(question));
        Assert.Equal(Pixels(percent), Pixels(fallback));
    }

    [Fact]
    public void ScaledText_IsExactlyTwoByTwoPixels_WithoutSoftEdges()
    {
        using var small = new Image<Rgba32>(30, 5);
        using var large = new Image<Rgba32>(60, 10);
        RailDmiText.Draw(small, "HELLO", 0, 0, Color.White);
        RailDmiText.Draw(large, "HELLO", 0, 0, Color.White, 2);
        for (var y = 0; y < 10; y++)
        for (var x = 0; x < 60; x++)
            Assert.Equal(small[x / 2, y / 2], large[x, y]);
    }

    [Fact]
    public void Lab_RendersActualComponents_WithoutExtraOverlays()
    {
        using var actual = new Image<Rgba32>(64, 32, Color.Black);
        using var expected = new Image<Rgba32>(64, 32, Color.Black);
        LegibilityLabScene.DrawSample(actual, 3, TimeSpan.Zero);
        RailCardRenderer.Draw(expected, new RailDepartureCard("CBG", 1, "18:12", "KINGS CROSS", "10", "ON TIME"), TimeSpan.Zero);
        Assert.Equal(Pixels(expected), Pixels(actual));
        LegibilityLabScene.DrawSample(actual, 5, TimeSpan.Zero);
        var message = new MessageScene("Hello Alan");
        message.Activate();
        message.Draw(expected);
        Assert.Equal(Pixels(expected), Pixels(actual));
        var scene = new LegibilityLabScene();
        scene.Activate();
        scene.Elapsed(LegibilityLabScene.SampleDuration * LegibilityLabScene.SampleCount - TimeSpan.FromSeconds(1));
        Assert.True(scene.IsActive);
        scene.Elapsed(TimeSpan.FromSeconds(1));
        Assert.False(scene.IsActive);
    }

    internal static WeatherSnapshot WeatherFixture(float temperature = 12, int code = 1, int rain = 20, float wind = 8) =>
        new(temperature, temperature, wind, code, true,
            [new("TODAY", code, temperature, temperature, rain, wind),
                new("TOM", code, temperature, temperature, rain, wind), new("WED", code, temperature, temperature, rain, wind)]);

    internal static byte[] Pixels(Image<Rgba32> image)
    {
        var pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);
        return pixels;
    }

    private static void AssertFits(IReadOnlyList<Rectangle> bounds)
    {
        foreach (var rect in bounds)
        {
            Assert.InRange(rect.Left, 1, 62);
            Assert.InRange(rect.Top, 1, 30);
            Assert.InRange(rect.Right, 2, 63);
            Assert.InRange(rect.Bottom, 2, 31);
        }
        for (var first = 0; first < bounds.Count; first++)
        for (var second = first + 1; second < bounds.Count; second++)
            Assert.False(bounds[first].IntersectsWith(bounds[second]), $"Overlapping regions: {bounds[first]} and {bounds[second]}");
    }
}
