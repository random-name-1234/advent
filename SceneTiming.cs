using System;

namespace advent;

internal static class SceneTiming
{
    public static readonly TimeSpan MaxSceneDuration = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan RandomSceneWindow = TimeSpan.FromMinutes(1);
    public const int MaxRandomSceneRequestsPerWindow = 2;
}
