using System.Collections.Generic;

namespace advent;

internal sealed class ManualSceneModule : ISceneModule
{
    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        return
        [
            new SceneCatalogRegistration(
                "Legibility Lab",
                static () => new LegibilityLabScene(),
                IncludedInCycle: false,
                MaxDuration: LegibilityLabScene.MaxSceneDuration)
        ];
    }
}
