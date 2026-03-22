using System.Collections.Generic;

namespace advent;

internal sealed class BuiltinSceneModule : ISceneModule
{
    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        return
        [
            new SceneCatalogRegistration("Cat", static () => new CatScene(), TransitionStyle: SceneTransitionStyle.CutWithClockFade),
            new SceneCatalogRegistration("Starfield Parallax", static () => new StarfieldParallaxScene()),
            new SceneCatalogRegistration("Metaballs", static () => new MetaballsScene()),
            new SceneCatalogRegistration("Donkey Kong", static () => new DonkeyKongScene()),
            new SceneCatalogRegistration("Space Invaders", static () => new SpaceInvadersScene()),
            new SceneCatalogRegistration("Bonkers Parade", static () => new BonkersParadeScene()),
            new SceneCatalogRegistration("Synthwave Grid", static () => new SynthwaveGridScene()),
            new SceneCatalogRegistration("Orbital", static () => new OrbitalScene()),
            new SceneCatalogRegistration("Fireworks", static () => new FireworksScene()),
            new SceneCatalogRegistration("Boids", static () => new BoidsScene()),
            new SceneCatalogRegistration("Tetris", static () => new TetrisScene()),
            new SceneCatalogRegistration("Sunrise Sunset", () => new SunriseSunsetScene(context.Latitude, context.Longitude)),
            new SceneCatalogRegistration("Error", static () => new ErrorScene())
        ];
    }
}
