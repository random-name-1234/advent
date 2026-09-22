using advent.Data.Rail;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

public sealed class RailBoardScene : ISpecialScene, IDeferredActivationScene
{
    public static readonly TimeSpan MaxSceneDuration = TimeSpan.FromSeconds(130);
    internal static readonly TimeSpan OverviewDuration = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan DepartureDuration = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(20);

    private readonly IRailSnapshotSource snapshotSource;
    private readonly TimeProvider timeProvider;
    private readonly HashSet<string> shownServices = [];
    private int slot;
    private TimeSpan elapsed;
    private TimeSpan elapsedOnCard;

    public RailBoardScene() : this(EmptyRailSnapshotSource.Instance) { }

    internal RailBoardScene(IRailSnapshotSource snapshotSource, TimeProvider? timeProvider = null)
    {
        this.snapshotSource = snapshotSource ?? throw new ArgumentNullException(nameof(snapshotSource));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsActive { get; private set; }
    public bool HidesTime => IsActive;
    public bool RainbowSnow => false;
    public string Name => "UK Rail Board";
    public bool IsReadyToActivate { get; private set; }
    public bool ShouldSkipActivation { get; private set; }
    internal RailCard? CurrentCard { get; private set; }

    public static bool IsConfiguredFromEnvironment() => RailBoardOptions.TryFromEnvironment() is not null;

    public void Prepare()
    {
        IsReadyToActivate = snapshotSource.TryGetSnapshot(out _);
        ShouldSkipActivation = !IsReadyToActivate;
    }

    public void AdvancePreparation(TimeSpan timeSpan) => Prepare();

    public void Activate()
    {
        shownServices.Clear();
        slot = 0;
        elapsed = elapsedOnCard = TimeSpan.Zero;
        IsActive = SelectNextCard();
        IsReadyToActivate = IsActive;
        ShouldSkipActivation = !IsActive;
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive || timeSpan <= TimeSpan.Zero)
            return;

        elapsed += timeSpan;
        if (elapsed >= MaxSceneDuration)
        {
            Finish();
            return;
        }

        elapsedOnCard += timeSpan;
        while (IsActive && CurrentCard is { } card && elapsedOnCard >= card.Duration)
        {
            elapsedOnCard -= card.Duration;
            if (!SelectNextCard())
                Finish();
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (IsActive && CurrentCard is { } card)
            RailCardRenderer.Draw(img, card, elapsedOnCard);
    }

    private bool SelectNextCard()
    {
        CurrentCard = null;
        // Slots, not frozen train lists: at each boundary choose from fresh cached data.
        if (slot >= 10 || !snapshotSource.TryGetSnapshot(out var snapshot))
            return false;

        while (slot < 10)
        {
            var stationIndex = slot / 5;
            var stationSlot = slot++ % 5;
            var station = stationIndex == 0 ? snapshot.Cambridge : snapshot.KingsCross;
            var services = RailCards.Upcoming(station, timeProvider.GetUtcNow());
            if (stationSlot == 0)
            {
                if (services.Count > 0)
                    continue;
                CurrentCard = RailCards.StationStatus(station);
                return true;
            }

            if (stationSlot == 4)
            {
                if (station.Alerts.Count == 0)
                    continue;
                CurrentCard = RailCards.Notice(station);
                return true;
            }

            var service = services.FirstOrDefault(service =>
                !shownServices.Contains($"{stationIndex}|{RailCards.ServiceKey(service)}"));
            if (service is null)
                continue;

            shownServices.Add($"{stationIndex}|{RailCards.ServiceKey(service)}");
            CurrentCard = RailCards.Departure(station, service, stationSlot);
            return true;
        }

        return false;
    }

    private void Finish()
    {
        IsActive = false;
        CurrentCard = null;
    }

    private sealed class EmptyRailSnapshotSource : IRailSnapshotSource
    {
        public static readonly EmptyRailSnapshotSource Instance = new();
        public bool TryGetSnapshot(out RailSceneSnapshot snapshot)
        {
            snapshot = null!;
            return false;
        }
    }
}
