using System.Collections.Generic;
using advent.Data.Rail;

namespace advent;

internal sealed class RailSceneModule : ISceneModule
{
    public IEnumerable<SceneCatalogRegistration> RegisterScenes(SceneModuleContext context)
    {
        return
        [
            new SceneCatalogRegistration(
                "UK Rail Board",
                context.RailConfigured
                    ? () => context.RailSnapshotSource is null
                        ? new RailBoardScene()
                        : new RailBoardScene(context.RailSnapshotSource)
                    : null,
                IncludedInCycle: context.RailConfigured,
                MaxDuration: RailBoardScene.MaxSceneDuration,
                IsReady: () => context.RailSnapshotSource is not null &&
                               context.RailSnapshotSource.TryGetSnapshot(out _))
        ];
    }
}
