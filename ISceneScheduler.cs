using System.Collections.Generic;

namespace advent;

internal interface ISceneScheduler
{
    IReadOnlyList<string> AvailableSceneNames { get; }
    IReadOnlyList<string> AllSceneNames { get; }
    IReadOnlyList<string> KnownSceneNames { get; }
    ISpecialScene GetScene();
    ISpecialScene GetNextSceneInCycle();
    string GetNextSceneNameInCycle();
    SceneSelection SelectSceneByName(string? sceneName);
}
