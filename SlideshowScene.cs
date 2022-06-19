using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent
{
    public class SlideshowScene : ISpecialScene
    {
        private Point topLeft = new Point(0, 0);
        public bool IsActive { get; private set; }

        public bool HidesTime { get; private set; }

        public bool RainbowSnow => false;
        public string Name => "Santa";

        private TimeSpan elapsedThisScene;
        private List<Image<Rgba32>> slides;
        private int slideIndex;

        private static TimeSpan sceneDuration = TimeSpan.FromSeconds(6);
        private static TimeSpan fadeDuration = TimeSpan.FromSeconds(0.5);

        public SlideshowScene()
        {
            IsActive = false;
            HidesTime = false;
            slides = new List<Image<Rgba32>>();
            slideIndex = 0;

            if (Directory.Exists("slides"))
            {
                foreach (var filename in Directory.GetFiles("slides", "*.png"))
                {
                    slides.Add(Image.Load<Rgba32>(filename));
                }
            }
        }

        public void Activate()
        {
            if (slides.Any())
            {
                slideIndex++;
                if (slideIndex >= slides.Count - 1)
                {
                    slideIndex = 0;
                }

                elapsedThisScene = TimeSpan.Zero;
                IsActive = true;
                HidesTime = true;
            }
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            elapsedThisScene += timeSpan;
            if (elapsedThisScene > sceneDuration)
            {
                IsActive = false;
                HidesTime = false;
            }
        }

        public void Draw(Image<Rgba32> img)
        {
            double fraction;
            if (elapsedThisScene < fadeDuration)
            {
                fraction = elapsedThisScene.TotalMilliseconds / fadeDuration.TotalMilliseconds;
            }
            else if (elapsedThisScene < sceneDuration - fadeDuration)
            {
                fraction = 1.0;
            }
            else
            {
                fraction = (sceneDuration - elapsedThisScene).TotalMilliseconds / fadeDuration.TotalMilliseconds;
            }

            fraction = Math.Min(1.0, Math.Max(0.0, fraction));
            if (IsActive)
            {
                img.Mutate(x => x.DrawImage(slides[slideIndex], topLeft, (float)fraction));
            }
        }
    }
}
