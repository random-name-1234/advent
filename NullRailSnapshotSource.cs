using advent.Data.Rail;

namespace advent;

internal sealed class NullRailSnapshotSource : IRailSnapshotSource
{
    public static readonly NullRailSnapshotSource Instance = new();

    public bool TryGetSnapshot(out RailSceneSnapshot snapshot)
    {
        snapshot = default!;
        return false;
    }
}
