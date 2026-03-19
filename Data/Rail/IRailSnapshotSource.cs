namespace advent.Data.Rail;

internal interface IRailSnapshotSource
{
    bool TryGetSnapshot(out RailSceneSnapshot snapshot);
}
