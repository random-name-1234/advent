using System.Net.Http.Headers;
using System.Text.Json;

namespace advent.Data.Home;

internal sealed class HomeSnapshotStore : IHomeSnapshotSource, IBackgroundRefreshService, IDisposable
{
    private readonly HttpClient client;
    private readonly Uri? endpoint;
    private readonly TimeProvider clock;
    private HomeSnapshot? latest;

    public HomeSnapshotStore(Uri? endpoint, string? token = null, TimeProvider? clock = null, HttpMessageHandler? handler = null)
    {
        this.endpoint = endpoint;
        this.clock = clock ?? TimeProvider.System;
        client = handler is null ? new HttpClient() : new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(5);
        client.MaxResponseContentBufferSize = 128 * 1024;
        if (!string.IsNullOrWhiteSpace(token)) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    internal static HomeSnapshotStore FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("ADVENT_HOME_DASHBOARD_URL");
        if (string.IsNullOrWhiteSpace(url)) return new HomeSnapshotStore(null);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("ADVENT_HOME_DASHBOARD_URL must be an HTTP(S) URL without embedded credentials.");
        return new HomeSnapshotStore(uri, Environment.GetEnvironmentVariable("ADVENT_HOME_DASHBOARD_TOKEN"));
    }

    public bool TryGetSnapshot(out HomeSnapshot snapshot)
    {
        snapshot = Volatile.Read(ref latest)!;
        return snapshot is not null && snapshot.IsFresh(clock.GetUtcNow());
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (endpoint is null) return;
        await RefreshOnceAsync(cancellationToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            await RefreshOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    internal async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        if (endpoint is null) return;
        try
        {
            using var response = await client.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
            // A successful unavailable/demo/version-mismatch response invalidates
            // the old snapshot; transport errors keep it only until its expiry.
            var next = HomeSnapshot.Parse(json.RootElement);
            var previous = Volatile.Read(ref latest);
            var now = clock.GetUtcNow();
            if (next is not null && previous is not null && next.PetsReady(now) && previous.PetsReady(now))
                next = next with { Cats = next.Cats.Select(cat => TrackMovement(cat, previous.Cats, now)).ToArray() };
            Volatile.Write(ref latest, next);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            Console.WriteLine($"Home dashboard refresh unavailable ({ex.GetType().Name}).");
        }
    }

    public void Dispose() => client.Dispose();

    internal static HomeCat TrackMovement(HomeCat cat, IReadOnlyList<HomeCat> previous, DateTimeOffset now)
    {
        var old = previous.FirstOrDefault(c => c.Id == cat.Id);
        if (old is null || old.Location == CatLocation.Unknown || cat.Location == CatLocation.Unknown ||
            cat.ChangedAt is not { } changed || changed > now || now - changed > TimeSpan.FromMinutes(2)) return cat;
        if (old.Location != cat.Location && (old.ChangedAt is null || changed > old.ChangedAt))
            return cat with { PreviousLocation = old.Location };
        return old.ChangedAt == cat.ChangedAt ? cat with { PreviousLocation = old.PreviousLocation } : cat;
    }
}
