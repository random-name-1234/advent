using System;
using System.Collections.Concurrent;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace advent
{
    public class Scene
    {
        private const int Width = 64;
        private const int Height = 32;
        private static readonly Rgba32 BlackBackground = new Rgba32(0, 0, 0, 255);

        private TimeSpan timeToNextRandomScene;
        private bool hasPendingSceneRequest;

        public Image<Rgba32> Img { get; set; }

        private readonly SnowMachine snowMachine = new SnowMachine();
        private ISpecialScene specialScene;
        private readonly Font font;
        private readonly Image<Rgba32> timeOverlay;
        private string lastRenderedTime = string.Empty;
        private readonly bool drawSnow;
        public bool ContinuousSceneRequests { get; set; }

        public event EventHandler NewSceneWanted;

        /// <summary>
        /// Gets the special scenes queue.
        /// </summary>
        public ConcurrentQueue<ISpecialScene> SpecialScenes { get; }

        public Scene()
        {
            timeToNextRandomScene = TimeSpan.FromMinutes(Random.Shared.NextDouble());
            hasPendingSceneRequest = false;
            SpecialScenes = new ConcurrentQueue<ISpecialScene>();
            specialScene = null;
            ContinuousSceneRequests = false;

            Img = new Image<Rgba32>(Width, Height);
            timeOverlay = new Image<Rgba32>(Width, Height);
            var fontFamily = SixLabors.Fonts.SystemFonts.Get("Arial");
            font = new Font(fontFamily, 16);

            if (DateTime.Now.Month == 12 || DateTime.Now.Month == 6)
            {
                System.Console.WriteLine("Snow!");
                drawSnow = true;
            }
            else
            {
                System.Console.WriteLine("No Snow!");
                drawSnow = false;
            }
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            snowMachine.Elapsed(timeSpan);
            var hidesTime = false;
            var now = DateTime.Now;

            ClearImage();

            // Prepare special scene.
            if (specialScene == null)
            {
                if (SpecialScenes.TryDequeue(out var queuedScene))
                {
                    Console.WriteLine("Found special scene on queue");
                    specialScene = queuedScene;
                    hasPendingSceneRequest = false;
                    specialScene.Activate();
                }
            }

            if (specialScene != null)
            {
                specialScene.Elapsed(timeSpan);
                if (!specialScene.IsActive)
                {
                    specialScene = null;
                }
                else
                {
                    hidesTime = specialScene.HidesTime;
                }
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
                    timeToNextRandomScene = TimeSpan.FromMinutes(Random.Shared.NextDouble() * 2.0);
                    NewSceneWanted?.Invoke(this, EventArgs.Empty);
                }
            }

            if (!hidesTime)
            {
                DrawClock(now);
            }

            if (specialScene != null)
            {
                specialScene.Draw(Img);

            }

            if (drawSnow)
            {
                snowMachine.RainbowSnow = now.Month == 6;
                foreach (Flake flake in snowMachine.Flakes)
                {
                    DrawFlake(flake);
                }
            }
        }

        private void RequestSceneIfNeeded()
        {
            if (specialScene != null || !SpecialScenes.IsEmpty)
            {
                hasPendingSceneRequest = false;
                return;
            }

            if (hasPendingSceneRequest)
            {
                return;
            }

            hasPendingSceneRequest = true;
            NewSceneWanted?.Invoke(this, EventArgs.Empty);
        }

        private void ClearImage()
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    Img[x, y] = BlackBackground;
                }
            }
        }

        private void DrawClock(DateTime now)
        {
            var timeToDisplay = now.TimeOfDay.ToString("hh\\:mm\\:ss");
            if (!string.Equals(lastRenderedTime, timeToDisplay, StringComparison.Ordinal))
            {
                lastRenderedTime = timeToDisplay;
                timeOverlay.Mutate(x => x.Clear(Color.Transparent));
                timeOverlay.Mutate(x => x.DrawText(timeToDisplay, font, Color.Aqua, new Point(0, 0)));
            }

            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var pixel = timeOverlay[x, y];
                    if (pixel.A != 0)
                    {
                        Img[x, y] = pixel;
                    }
                }
            }
        }

        private void DrawFlake(Flake flake)
        {
            var radius = Math.Max(1, (int)Math.Ceiling(flake.Width));
            var centerX = (int)Math.Round(flake.Position.X);
            var centerY = (int)Math.Round(Height - flake.Position.Y);
            var radiusSquared = radius * radius;

            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                if (y < 0 || y >= Height)
                {
                    continue;
                }

                var dy = y - centerY;
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    if (x < 0 || x >= Width)
                    {
                        continue;
                    }

                    var dx = x - centerX;
                    if ((dx * dx) + (dy * dy) <= radiusSquared)
                    {
                        Img[x, y] = flake.Color;
                    }
                }
            }
        }
    }
}
