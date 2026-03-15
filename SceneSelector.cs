using System;
using System.Collections.Generic;
using System.Linq;

namespace advent
{
    public sealed class SceneSelector
    {
        private static readonly IReadOnlyList<SceneDefinition> DefaultSceneDefinitions = new[]
        {
            new SceneDefinition("Cat", static () => new CatScene()),
            new SceneDefinition("Rainbow", static () => new RainbowSnowScene()),
            new SceneDefinition("Game of Life", static () => new FadingScene(new GameOfLifeScene())),
            new SceneDefinition("Starfield Parallax", static () => new FadingScene(new StarfieldParallaxScene())),
            new SceneDefinition("Plasma SDF", static () => new FadingScene(new PlasmaSdfScene())),
            new SceneDefinition("Matrix Rain", static () => new FadingScene(new MatrixRainScene())),
            new SceneDefinition("Synthwave Grid", static () => new FadingScene(new SynthwaveGridScene())),
            new SceneDefinition("Orbital", static () => new FadingScene(new OrbitalScene())),
            new SceneDefinition("Warp Core", static () => new FadingScene(new WarpCoreScene())),
            new SceneDefinition("Error", static () => new FadingScene(new ErrorScene())),
            new SceneDefinition("Space Invaders", static () => new FadingScene(new SpaceInvadersScene())),
            new SceneDefinition("CTM Logo", static () => new FadingScene(new CtmLogoScene())),
            new SceneDefinition("CTM Banner", static () => new FadingScene(new CtmBannerScene()))
        };

        private static readonly IReadOnlyList<SceneDefinition> ChristmasSceneDefinitions = new[]
        {
            new SceneDefinition("Santa", static () => new FadingScene(new SantaScene())),
            new SceneDefinition("Animated GIF", static () => new FadingScene(new AnimatedGifScene("christmas-1.gif"))),
            new SceneDefinition("Animated GIF", static () => new FadingScene(new AnimatedGifScene("christmas-2.gif"))),
            new SceneDefinition("Animated GIF", static () => new FadingScene(new AnimatedGifScene("christmas-3.gif"))),
            new SceneDefinition("Animated GIF", static () => new FadingScene(new AnimatedGifScene("christmas-4.gif"))),
            new SceneDefinition("Animated GIF", static () => new FadingScene(new AnimatedGifScene("christmas-5.gif"))),
            new SceneDefinition("Animated GIF", static () => new FadingScene(new AnimatedGifScene("christmas-6.gif"))),
            new SceneDefinition("Animated GIF", static () => new FadingScene(new AnimatedGifScene("christmas-7.gif")))
        };

        private readonly IReadOnlyList<SceneDefinition> sceneDefinitions;
        private readonly IReadOnlyList<SceneDefinition> cycleSceneDefinitions;
        private readonly Func<int, int> nextIndex;
        private int cycleIndex;

        public SceneSelector()
            : this(DateTime.Now.Month)
        {
        }

        public SceneSelector(int month, Func<int, int> nextIndex = null)
        {
            if (month is < 1 or > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(month), month, "Month must be in the range 1-12.");
            }

            sceneDefinitions = month == 12
                ? ChristmasSceneDefinitions
                : DefaultSceneDefinitions;
            cycleSceneDefinitions = DefaultSceneDefinitions.Concat(ChristmasSceneDefinitions).ToArray();

            this.nextIndex = nextIndex ?? Random.Shared.Next;
            AvailableSceneNames = sceneDefinitions.Select(static x => x.Name).ToArray();
            AllSceneNames = cycleSceneDefinitions.Select(static x => x.Name).ToArray();
            cycleIndex = 0;
        }

        public IReadOnlyList<string> AvailableSceneNames { get; }
        public IReadOnlyList<string> AllSceneNames { get; }

        public ISpecialScene GetScene()
        {
            var index = nextIndex(sceneDefinitions.Count);
            if ((uint)index >= (uint)sceneDefinitions.Count)
            {
                throw new InvalidOperationException(
                    $"Scene index provider returned {index}, but valid range is 0..{sceneDefinitions.Count - 1}.");
            }

            return sceneDefinitions[index].Create();
        }

        public string GetNextSceneNameInCycle()
        {
            var scene = GetNextSceneDefinitionInCycle();
            return scene.Name;
        }

        public ISpecialScene GetNextSceneInCycle()
        {
            var scene = GetNextSceneDefinitionInCycle();
            return scene.Create();
        }

        private SceneDefinition GetNextSceneDefinitionInCycle()
        {
            if (cycleSceneDefinitions.Count == 0)
            {
                throw new InvalidOperationException("No scenes are available for cycling.");
            }

            var selectedScene = cycleSceneDefinitions[cycleIndex];
            cycleIndex = (cycleIndex + 1) % cycleSceneDefinitions.Count;
            return selectedScene;
        }

        private sealed record SceneDefinition(string Name, Func<ISpecialScene> Create);
    }
}
