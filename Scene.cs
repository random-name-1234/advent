using System;
using System.Collections.Concurrent;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace advent
{
    public class Scene
    {
        private TimeSpan timeToNextRandomScene;

        public Image<Rgba32> Img { get; set; }

        private SnowMachine snowMachine = new SnowMachine();
        private TimeSpan phaseDuration = TimeSpan.FromSeconds(8);
        private ISpecialScene specialScene;
        private FontFamily fontFamily = SixLabors.Fonts.SystemFonts.Get("Arial");
        private Font font;
        private String timeToDisplay;
        private bool drawSnow;

        public event EventHandler NewSceneWanted;

        /// <summary>
        /// Gets the special scenes queue.
        /// </summary>
        public ConcurrentQueue<ISpecialScene> SpecialScenes { get; }

        public Scene()
        {
            timeToNextRandomScene = TimeSpan.FromMinutes(new Random().NextDouble() * 1);
            SpecialScenes = new ConcurrentQueue<ISpecialScene>();
            specialScene = null;

            Img = new Image<Rgba32>(64, 32);
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
            bool hidesTime = false;

            Img.Mutate(x => x.FillPolygon(Color.Black, new PointF(0, 0), new PointF(64, 0), new PointF(64, 32), new PointF(0, 32)));

            // Prepare special scene.
            if (specialScene == null)
            {
                if (SpecialScenes.Count > 0)
                {
                    Console.WriteLine("Found special scene on queue");
                    SpecialScenes.TryDequeue(out specialScene);
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

            timeToNextRandomScene -= timeSpan;
            if (timeToNextRandomScene < TimeSpan.Zero)
            {
                timeToNextRandomScene = TimeSpan.FromMinutes(new Random().NextDouble() * 2);
                NewSceneWanted?.Invoke(this, EventArgs.Empty);
            }

            if (!hidesTime)
            {
                timeToDisplay = DateTime.Now.TimeOfDay.ToString("hh\\:mm\\:ss");
                Img.Mutate(x => x.DrawText(timeToDisplay, font, Color.Aqua, new Point(0, 0)));
            }

            if (specialScene != null)
            {
                specialScene.Draw(Img);

            }

            if (drawSnow)
            {
                if (DateTime.Now.Month == 6)
                {
                    snowMachine.RainbowSnow = true;
                }
                if (DateTime.Now.Month == 12)
                {
                    snowMachine.RainbowSnow = specialScene.RainbowSnow;
                }
                foreach (Flake flake in snowMachine.Flakes)
                {
                    Img.Mutate(x => x.Fill(flake.Color, new EllipsePolygon(flake.Position.X, 32f - flake.Position.Y, flake.Width, flake.Width)));
                }
            }
        }
    }
}
