using System.Collections.Generic;

namespace advent;

internal sealed class SeasonalSceneModule : ISceneModule
{
    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        if (context.Month != 12)
            return [];

        return
        [
            new SceneCatalogRegistration("Santa", static () => new SantaScene())
        ];
    }
}
