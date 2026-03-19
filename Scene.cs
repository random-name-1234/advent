using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class Scene
{
    private const string WidestClockSample = "88:88:88";
    private const float PreferredClockFontSize = 16f;
    private const float MinimumClockFontSize = 8f;
    private const float ClockMaxWidth = 62f;

    private readonly bool drawSnow;
    private readonly Action<Image<Rgba32>> drawClockOverlay;
    private readonly Font font;
    private readonly Queue<TimeSpan> recentRandomSceneRequests = new();
    private readonly Image<Rgba32> specialSceneLayer;

    private readonly SnowMachine snowMachine = new();
    private bool hasPendingSceneRequest;
    private ISpecialScene? specialScene;
    private TimeSpan elapsedSinceStartup;
    private TimeSpan timeToNextRandomScene;

    public Scene(Action<Image<Rgba32>>? clockOverlayRenderer = null)
    {
        timeToNextRandomScene = TimeSpan.FromMinutes(Random.Shared.NextDouble());
        hasPendingSceneRequest = false;
        SpecialScenes = new ConcurrentQueue<ISpecialScene>();
        ContinuousSceneRequests = false;

        Img = new Image<Rgba32>(64, 32);
        specialSceneLayer = new Image<Rgba32>(64, 32);
        drawClockOverlay = clockOverlayRenderer ?? DrawClockOverlay;
        font = AppFonts.CreateFitting(WidestClockSample, PreferredClockFontSize, MinimumClockFontSize, ClockMaxWidth);

        if (DateTime.Now.Month == 12 || DateTime.Now.Month == 6)
        {
            Console.WriteLine("Snow!");
            drawSnow = true;
        }
        else
        {
            Console.WriteLine("No Snow!");
            drawSnow = false;
        }
    }

    public Image<Rgba32> Img { get; set; }
    public bool ContinuousSceneRequests { get; set; }

    /// <summary>
    ///     Gets the special scenes queue.
    /// </summary>
    public ConcurrentQueue<ISpecialScene> SpecialScenes { get; }

    public event EventHandler? NewSceneWanted;

    public void Elapsed(TimeSpan timeSpan)
    {
        elapsedSinceStartup += timeSpan;
        snowMachine.Elapsed(timeSpan);
        var hidesTime = false;
        var clockHandledByTransition = false;

        Img.Mutate(x =>
            x.FillPolygon(Color.Black, new PointF(0, 0), new PointF(64, 0), new PointF(64, 32), new PointF(0, 32)));

        // Prepare special scene.
        if (specialScene == null && SpecialScenes.TryDequeue(out var queuedScene))
        {
            Console.WriteLine("Found special scene on queue");
            specialScene = queuedScene;
            hasPendingSceneRequest = false;
            specialScene.Activate();
        }

        if (specialScene != null)
        {
            specialScene.Elapsed(timeSpan);
            if (!specialScene.IsActive)
                specialScene = null;
            else
                hidesTime = specialScene.HidesTime;
        }

        if (ContinuousSceneRequests)
        {
            RequestSceneIfNeeded();
        }
        else
        {
            timeToNextRandomScene -= timeSpan;
            if (timeToNextRandomScene < TimeSpan.Zero)
            {
                TriggerRandomSceneRequestIfAllowed();
            }
        }

        if (specialScene != null)
        {
            if (specialScene is FadingScene fadingScene &&
                fadingScene.CrossfadesClock &&
                fadingScene.Opacity > 0f &&
                fadingScene.Opacity < 1f)
            {
                drawClockOverlay(Img);
                ClearSpecialSceneLayer();
                specialScene.Draw(specialSceneLayer);
                Img.Mutate(ctx => ctx.DrawImage(specialSceneLayer, new Point(0, 0), fadingScene.Opacity));
                clockHandledByTransition = true;
                hidesTime = false;
            }
            else
            {
                specialScene.Draw(Img);
            }
        }

        if (drawSnow)
        {
            var month = DateTime.Now.Month;
            if (month == 6) snowMachine.RainbowSnow = true;
            if (month == 12) snowMachine.RainbowSnow = false;
            foreach (var flake in snowMachine.Flakes)
                Img.Mutate(x => x.Fill(flake.Color,
                    new EllipsePolygon(flake.Position.X, 32f - flake.Position.Y, flake.Width, flake.Width)));
        }

        if (!hidesTime && !clockHandledByTransition)
            drawClockOverlay(Img);
    }

    private void ClearSpecialSceneLayer()
    {
        for (var y = 0; y < specialSceneLayer.Height; y++)
        for (var x = 0; x < specialSceneLayer.Width; x++)
            specialSceneLayer[x, y] = Color.Black;
    }

    private void RequestSceneIfNeeded()
    {
        if (specialScene != null || !SpecialScenes.IsEmpty)
        {
            hasPendingSceneRequest = false;
            return;
        }

        if (hasPendingSceneRequest) return;

        hasPendingSceneRequest = true;
        NewSceneWanted?.Invoke(this, EventArgs.Empty);
    }

    private void TriggerRandomSceneRequestIfAllowed()
    {
        while (recentRandomSceneRequests.Count > 0 &&
               elapsedSinceStartup - recentRandomSceneRequests.Peek() >= SceneTiming.RandomSceneWindow)
            recentRandomSceneRequests.Dequeue();

        if (recentRandomSceneRequests.Count >= SceneTiming.MaxRandomSceneRequestsPerWindow)
        {
            var nextAvailableAt = recentRandomSceneRequests.Peek() + SceneTiming.RandomSceneWindow;
            timeToNextRandomScene = nextAvailableAt - elapsedSinceStartup;
            if (timeToNextRandomScene < TimeSpan.FromMilliseconds(1))
                timeToNextRandomScene = TimeSpan.FromMilliseconds(1);
            return;
        }

        recentRandomSceneRequests.Enqueue(elapsedSinceStartup);
        timeToNextRandomScene = TimeSpan.FromMinutes(Random.Shared.NextDouble() * 2);
        NewSceneWanted?.Invoke(this, EventArgs.Empty);
    }

    private void DrawClockOverlay(Image<Rgba32> img)
    {
        var timeToDisplay = DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss");
        img.Mutate(x => x.DrawText(timeToDisplay, font, Color.Aqua, new Point(0, 0)));
    }
}
