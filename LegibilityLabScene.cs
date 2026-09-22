using advent.Data.Weather;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public sealed class LegibilityLabScene : ISpecialScene
{
    public static readonly TimeSpan MaxSceneDuration = TimeSpan.FromSeconds(60);
    internal static readonly TimeSpan SampleDuration = TimeSpan.FromSeconds(8);
    internal const int SampleCount = 7;
    private static readonly DateTime SampleClock = new(2026, 9, 21, 18, 12, 30);
    private static readonly ClockRenderer Clock = new();
    private static readonly WeatherSnapshot Weather = new(12, 10, 8, 1, true,
        [new("TODAY", 1, 15, 9, 20, 12), new("TOM", 61, 14, 7, 100, 22), new("WED", 3, 11, 5, 10, 8)]);
    private static readonly RailDepartureCard Train = new("CBG", 1, "18:12", "KINGS CROSS", "10", "ON TIME");
    private static readonly MessageLayout ShortMessage = MessageLayout.Create("Hello Alan", null);
    private static readonly MessageLayout PagedMessage = MessageLayout.Create("Pixel text. Three clear lines. Every word stays still.", null);
    private TimeSpan elapsed;

    public bool IsActive { get; private set; }
    public bool HidesTime => IsActive;
    public bool RainbowSnow => false;
    public string Name => "Legibility Lab";

    public void Activate()
    {
        elapsed = TimeSpan.Zero;
        IsActive = true;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive || timeSpan <= TimeSpan.Zero) return;
        elapsed += timeSpan;
        if (elapsed >= SampleDuration * SampleCount) IsActive = false;
    }

    public void Draw(Image<Rgba32> image)
    {
        if (!IsActive) return;
        var page = (int)(elapsed.TotalSeconds / SampleDuration.TotalSeconds);
        DrawSample(image, page, elapsed - SampleDuration * page);
    }

    internal static void DrawSample(Image<Rgba32> image, int page, TimeSpan time)
    {
        MatrixTextLayout.Clear(image);
        // Production renderers, with no lab headings or indicators over their pixels.
        switch (page)
        {
            case 0: Clock.DrawAt(image, SampleClock.Add(time)); break;
            case 1: WeatherScene.DrawPanel(image, Weather, 0, time); break;
            case 2: WeatherScene.DrawPanel(image, Weather, 1, time); break;
            case 3: RailCardRenderer.Draw(image, Train, time); break;
            case 4: RailCardRenderer.Draw(image, Train with { Number = 2, Time = "18:32", Status = "CANCELLED" }, time); break;
            case 5: MessageScene.DrawPage(image, ShortMessage, time, new Rgba32(220, 230, 255)); break;
            case 6: MessageScene.DrawPage(image, PagedMessage, time, new Rgba32(220, 230, 255)); break;
        }
    }
}
