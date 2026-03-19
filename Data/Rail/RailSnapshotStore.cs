namespace advent.Data.Rail;

internal sealed class RailSnapshotStore : IRailSnapshotSource, IBackgroundRefreshService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan MaxSnapshotAge = TimeSpan.FromSeconds(45);

    private readonly Lock gate = new();
    private readonly RailBoardOptions options;

    private RailSceneSnapshot? snapshot;
    private DateTimeOffset updatedAtUtc = DateTimeOffset.MinValue;

    public RailSnapshotStore(RailBoardOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public static RailSnapshotStore? TryCreateFromEnvironment()
        => RailBoardOptions.TryFromEnvironment() is { } options
            ? new RailSnapshotStore(options)
            : null;

    public bool TryGetSnapshot(out RailSceneSnapshot snapshot)
    {
        lock (gate)
        {
            if (this.snapshot is null || DateTimeOffset.UtcNow - updatedAtUtc > MaxSnapshotAge)
            {
                snapshot = default!;
                return false;
            }

            snapshot = this.snapshot;
            return true;
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await RefreshOnceAsync(cancellationToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            await RefreshOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var freshSnapshot = await DarwinRailSnapshotProvider
                .FetchSnapshotAsync(options, DateTimeOffset.UtcNow, cancellationToken)
                .ConfigureAwait(false);

            lock (gate)
            {
                snapshot = freshSnapshot;
                updatedAtUtc = DateTimeOffset.UtcNow;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Rail refresh failed: {ex.Message}");
        }
    }
}
