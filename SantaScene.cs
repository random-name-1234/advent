using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;


namespace advent
{
    public class SantaScene : ISpecialScene
    {
        private const string SantaFileName = "santa-256x32.png";
        private const string Legs1FileName = "santa-256x32-legs1.png";
        private const string Legs2FileName = "santa-256x32-legs2.png";
        public bool IsActive { get; private set; }

        public bool HidesTime { get; private set; }

        public bool RainbowSnow => false;
        public string Name => "Santa";

        private TimeSpan elapsedThisScene;
        private Image<Rgba32> santa;
        private Image<Rgba32> legs1;
        private Image<Rgba32> legs2;

        private const int StartXOffset = 63;
        private const int TotalMovementDistance = 318;
        private static TimeSpan timeToMove = TimeSpan.FromSeconds(8);
        private static TimeSpan moveLegsInterval = TimeSpan.FromSeconds(0.6);

        public SantaScene()
        {
            IsActive = false;
            HidesTime = false;
            santa = Image.Load<Rgba32>(SantaFileName);
            legs1 = Image.Load<Rgba32>(Legs1FileName);
            legs2 = Image.Load<Rgba32>(Legs2FileName);
        }

        public void Activate()
        {
            elapsedThisScene = TimeSpan.Zero;
            IsActive = true;
            HidesTime = true;
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            elapsedThisScene += timeSpan;
            if (elapsedThisScene > timeToMove)
            {
                IsActive = false;
                HidesTime = false;
            }
        }

        public void Draw(Image<Rgba32> img)
        {
            var x = (elapsedThisScene.TotalMilliseconds / timeToMove.TotalMilliseconds) * TotalMovementDistance;
            var position = new Point(StartXOffset - (int)x, 0);
            bool switchLegs = (int)(elapsedThisScene.TotalSeconds / moveLegsInterval.TotalSeconds) % 2 == 0;
            if (IsActive)
            {
                img.Mutate(x => x.DrawImage(santa, position, 1f));
                if (switchLegs)
                    img.Mutate(x => x.DrawImage(legs1, position, 1f));
                else
                    img.Mutate(x => x.DrawImage(legs2, position, 1f));
            }
        }
    }
}
