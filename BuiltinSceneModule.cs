using System.Collections.Generic;

namespace advent;

internal sealed class BuiltinSceneModule : ISceneModule
{
    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        return
        [
            new SceneCatalogRegistration("Cat", static () => new CatScene()),
            new SceneCatalogRegistration("Starfield Parallax", static () => new StarfieldParallaxScene()),
            new SceneCatalogRegistration("Metaballs", static () => new MetaballsScene()),
            new SceneCatalogRegistration("Donkey Kong", static () => new DonkeyKongScene()),
            new SceneCatalogRegistration("Space Invaders", static () => new SpaceInvadersScene()),
            new SceneCatalogRegistration("Bonkers Parade", static () => new BonkersParadeScene()),
            new SceneCatalogRegistration("Synthwave Grid", static () => new SynthwaveGridScene()),
            new SceneCatalogRegistration("Orbital", static () => new OrbitalScene()),
            new SceneCatalogRegistration("Fireworks", static () => new FireworksScene()),
            new SceneCatalogRegistration("Error", static () => new ErrorScene())
        ];
    }
}
