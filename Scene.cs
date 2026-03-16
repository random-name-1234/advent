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
    private readonly bool drawSnow;
    private readonly Font font;
    private readonly Queue<TimeSpan> recentRandomSceneRequests = new();

    private readonly SnowMachine snowMachine = new();
    private bool hasPendingSceneRequest;
    private ISpecialScene? specialScene;
    private TimeSpan elapsedSinceStartup;
    private TimeSpan timeToNextRandomScene;

    public Scene()
    {
        timeToNextRandomScene = TimeSpan.FromMinutes(Random.Shared.NextDouble());
        hasPendingSceneRequest = false;
        SpecialScenes = new ConcurrentQueue<ISpecialScene>();
        ContinuousSceneRequests = false;

        Img = new Image<Rgba32>(64, 32);
        font = AppFonts.Create(16);

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

        if (!hidesTime)
        {
            var timeToDisplay = DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss");
            Img.Mutate(x => x.DrawText(timeToDisplay, font, Color.Aqua, new Point(0, 0)));
        }

        if (specialScene != null) specialScene.Draw(Img);

        if (drawSnow)
        {
            var month = DateTime.Now.Month;
            if (month == 6) snowMachine.RainbowSnow = true;
            if (month == 12) snowMachine.RainbowSnow = false;
            foreach (var flake in snowMachine.Flakes)
                Img.Mutate(x => x.Fill(flake.Color,
                    new EllipsePolygon(flake.Position.X, 32f - flake.Position.Y, flake.Width, flake.Width)));
        }
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
}
