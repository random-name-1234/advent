using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent
{
    public class FadingScene : ISpecialScene
    {
        private ISpecialScene fadeOut;
        private ISpecialScene mainScene;
        private ISpecialScene fadeIn;
        private ISpecialScene currentScene;
        private Queue<ISpecialScene> scenes;

        public bool IsActive => currentScene != null;

        public bool HidesTime => currentScene?.HidesTime ?? false;

        public bool RainbowSnow => currentScene?.RainbowSnow ?? false;
        public string Name => mainScene.Name;

        public FadingScene(ISpecialScene mainScene)
        {
            fadeOut = new FadeInOutScene(Fade.Out);
            this.mainScene = mainScene;
            fadeIn = new FadeInOutScene(Fade.In);
            currentScene = null;
            scenes = new Queue<ISpecialScene>();
        }

        public void Activate()
        {
            scenes.Enqueue(fadeOut);
            scenes.Enqueue(mainScene);
            scenes.Enqueue(fadeIn);
        }

        public void Draw(Image<Rgba32> img)
        {
            currentScene?.Draw(img);
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            if (currentScene == null && scenes.Count != 0)
            {
                currentScene = scenes.Dequeue();
                currentScene.Activate();
            }

            if (currentScene != null) {
                currentScene.Elapsed(timeSpan);

                if (!currentScene.IsActive)
                {
                    if (scenes.Count != 0)
                    {
                        currentScene = scenes.Dequeue();
                        currentScene.Activate();
                    }
                    else
                    {
                        currentScene = null;
                    }
                }
            }
        }
    }
}
