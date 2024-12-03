using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace advent
{
    public class SceneSelector
    {
        private List<ISpecialScene> specialScenes;
        private ISpecialScene cat;
        private ISpecialScene santa;
        private ISpecialScene rainbow;
        private ISpecialScene spaceInvaders;
        private ISpecialScene bsod;
        private ISpecialScene ctmLogo;
        private ISpecialScene ctmBanner;
        private Random random;

        public SceneSelector()
        {
            random = new Random();
            specialScenes = new List<ISpecialScene>();
            if (DateTime.Now.Month == 12)
            {
                specialScenes.Add(santa = new FadingScene(new SantaScene()));
                specialScenes.Add(santa = new FadingScene(new AnimatedGifScene("christmas-1.gif")));
                specialScenes.Add(santa = new FadingScene(new AnimatedGifScene("christmas-2.gif")));
                specialScenes.Add(santa = new FadingScene(new AnimatedGifScene("christmas-3.gif")));
                specialScenes.Add(santa = new FadingScene(new AnimatedGifScene("christmas-4.gif")));
                specialScenes.Add(santa = new FadingScene(new AnimatedGifScene("christmas-5.gif")));
                specialScenes.Add(santa = new FadingScene(new AnimatedGifScene("christmas-6.gif")));
                specialScenes.Add(santa = new FadingScene(new AnimatedGifScene("christmas-7.gif")));
            }
            else
            {
                specialScenes.Add(cat = new CatScene());
                specialScenes.Add(rainbow = new RainbowSnowScene());
                specialScenes.Add(bsod = new FadingScene(new ErrorScene()));
                specialScenes.Add(spaceInvaders = new FadingScene(new SpaceInvadersScene()));
                specialScenes.Add(ctmLogo = new FadingScene(new CtmLogoScene()));
                specialScenes.Add(ctmBanner = new FadingScene(new CtmBannerScene()));  
            }
        }

        public ISpecialScene GetScene()
        {
                int index = random.Next(0, specialScenes.Count);
                return specialScenes[index];
        }
    }
}
