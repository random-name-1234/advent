namespace advent.Data.Weather;

internal sealed class WeatherSnapshotStore : IWeatherSnapshotSource, IBackgroundRefreshService
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxSnapshotAge = TimeSpan.FromMinutes(10);

    private readonly Lock gate = new();
    private readonly WeatherOptions options;

    private WeatherSnapshot? snapshot;
    private DateTimeOffset updatedAtUtc = DateTimeOffset.MinValue;

    public WeatherSnapshotStore(WeatherOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool TryGetSnapshot(out WeatherSnapshot snapshot)
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
            var freshSnapshot = await OpenMeteoWeatherClient
                .FetchSnapshotAsync(options, forecastCount: 3, cancellationToken)
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
            Console.WriteLine($"Weather refresh failed: {ex.Message}");
        }
    }
}
