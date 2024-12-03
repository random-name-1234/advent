using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Gif;

namespace advent
{
    public class AnimatedGifScene : ISpecialScene
    {
        public bool IsActive { get; private set; }

        public bool HidesTime { get; private set; }

        public bool RainbowSnow => false;
        public string Name => "Animated GIF";

        private TimeSpan elapsedThisScene;
        private TimeSpan totalDuration;
        private Image<Rgba32> gifImage;
        private List<TimeSpan> frameDurations;
        private int currentFrameIndex;
        private TimeSpan currentFrameElapsed;

        private static TimeSpan sceneDuration = TimeSpan.FromSeconds(10);

        public AnimatedGifScene(string gifFilePath)
        {
            IsActive = false;
            HidesTime = false;

            // Load the animated GIF
            gifImage = Image.Load<Rgba32>(gifFilePath);

            // Extract frame durations
            frameDurations = new List<TimeSpan>();
            foreach (var frame in gifImage.Frames)
            {
                var metadata = frame.Metadata.GetGifMetadata();
                var frameDelay = metadata.FrameDelay;
                // FrameDelay is in hundredths of a second
                var delay = TimeSpan.FromMilliseconds(frameDelay * 10);
                frameDurations.Add(delay);
                totalDuration += delay;
            }
        }

        public void Activate()
        {
            elapsedThisScene = TimeSpan.Zero;
            currentFrameIndex = 0;
            currentFrameElapsed = TimeSpan.Zero;
            IsActive = true;
            HidesTime = true;
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            if (!IsActive)
                return;

            elapsedThisScene += timeSpan;
            currentFrameElapsed += timeSpan;

            if (elapsedThisScene > sceneDuration)
            {
                IsActive = false;
                HidesTime = false;
                return;
            }

            // Advance to the next frame if necessary
            if (currentFrameElapsed >= frameDurations[currentFrameIndex])
            {
                currentFrameElapsed -= frameDurations[currentFrameIndex];
                currentFrameIndex = (currentFrameIndex + 1) % gifImage.Frames.Count;
            }
        }

        public void Draw(Image<Rgba32> img)
        {
            if (!IsActive)
                return;

            // Get the current frame
            var frame = gifImage.Frames.CloneFrame(currentFrameIndex);

            // Resize the frame to fit the display if necessary
            if (frame.Width != img.Width || frame.Height != img.Height)
            {
                frame.Mutate(x => x.Resize(img.Width, img.Height));
            }

            // Draw the frame onto the provided image
            img.Mutate(x => x.DrawImage(frame, new Point(0, 0), 1f));
        }
    }
}
