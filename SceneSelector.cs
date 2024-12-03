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
            specialScenes.Add(cat = new CatScene());
            specialScenes.Add(rainbow = new RainbowSnowScene());
            specialScenes.Add(bsod = new FadingScene(new ErrorScene()));
            specialScenes.Add(spaceInvaders = new FadingScene(new SpaceInvadersScene()));
            specialScenes.Add(ctmLogo = new FadingScene(new CtmLogoScene()));
            specialScenes.Add(ctmBanner = new FadingScene(new CtmBannerScene()));

            if (DateTime.Now.Month == 12)
            {
                specialScenes.Add(santa = new FadingScene(new SantaScene()));
                specialScenes.Add(santa = new AnimatedGifScene("advent-3.gif"));
            }
        }

        public ISpecialScene GetScene()
        {
                int index = random.Next(0, specialScenes.Count);
                return specialScenes[index];
        }
    }
}
