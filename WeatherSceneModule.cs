using System.Collections.Generic;

namespace advent;

internal sealed class WeatherSceneModule : ISceneModule
{
    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        return
        [
            new SceneCatalogRegistration(
                "Weather",
                () => context.WeatherSnapshotSource is null
                    ? new WeatherScene()
                    : new WeatherScene(context.WeatherSnapshotSource),
                IsReady: () => context.WeatherSnapshotSource is not null &&
                               context.WeatherSnapshotSource.TryGetSnapshot(out _))
        ];
    }
}
