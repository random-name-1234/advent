using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace advent;

internal sealed class ClockFadingScene : ISpecialScene, IClockTransitionScene
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(1);

    private readonly ISpecialScene mainScene;
    private readonly IDeferredActivationScene? deferredMainScene;
    private TimeSpan elapsedInPhase;
    private Phase phase;

    public ClockFadingScene(ISpecialScene mainScene)
    {
        this.mainScene = mainScene ?? throw new ArgumentNullException(nameof(mainScene));
        deferredMainScene = mainScene as IDeferredActivationScene;
    }

    public bool IsActive { get; private set; }
    public bool HidesTime => IsActive && phase == Phase.Main && mainScene.HidesTime;
    public bool RainbowSnow => IsActive && phase == Phase.Main && mainScene.RainbowSnow;
    public string Name => mainScene.Name;
    public float ClockOpacity => GetClockOpacity();

    public void Activate()
    {
        IsActive = true;
        elapsedInPhase = TimeSpan.Zero;

        if (deferredMainScene is not null)
        {
            deferredMainScene.Prepare();
            if (deferredMainScene.ShouldSkipActivation)
            {
                IsActive = false;
                phase = Phase.Pending;
                return;
            }

            if (!deferredMainScene.IsReadyToActivate)
            {
                phase = Phase.Pending;
                return;
            }
        }

        BeginClockFadeOut();
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive)
            return;

        var remaining = timeSpan;
        while (IsActive && remaining > TimeSpan.Zero)
        {
            switch (phase)
            {
                case Phase.Pending:
                    if (deferredMainScene is null)
                    {
                        BeginClockFadeOut();
                        continue;
                    }

                    deferredMainScene.AdvancePreparation(remaining);
                    remaining = TimeSpan.Zero;

                    if (deferredMainScene.ShouldSkipActivation)
                    {
                        IsActive = false;
                        break;
                    }

                    if (deferredMainScene.IsReadyToActivate)
                        BeginClockFadeOut();

                    break;

                case Phase.FadeOutClock:
                    remaining = AdvanceFade(remaining, BeginMainScene);
                    break;

                case Phase.Main:
                    mainScene.Elapsed(remaining);
                    remaining = TimeSpan.Zero;
                    if (!mainScene.IsActive)
                    {
                        phase = Phase.FadeInClock;
                        elapsedInPhase = TimeSpan.Zero;
                    }

                    break;

                case Phase.FadeInClock:
                    remaining = AdvanceFade(remaining, CompleteScene);
                    break;
            }
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive || phase != Phase.Main)
            return;

        mainScene.Draw(img);
    }

    private void BeginClockFadeOut()
    {
        phase = Phase.FadeOutClock;
        elapsedInPhase = TimeSpan.Zero;
    }

    private void BeginMainScene()
    {
        mainScene.Activate();
        if (!mainScene.IsActive)
        {
            phase = Phase.FadeInClock;
            elapsedInPhase = TimeSpan.Zero;
            return;
        }

        phase = Phase.Main;
        elapsedInPhase = TimeSpan.Zero;
    }

    private void CompleteScene()
    {
        IsActive = false;
        elapsedInPhase = FadeDuration;
    }

    private TimeSpan AdvanceFade(TimeSpan remaining, Action nextPhase)
    {
        var phaseRemaining = FadeDuration - elapsedInPhase;
        var step = remaining <= phaseRemaining ? remaining : phaseRemaining;
        elapsedInPhase += step;
        remaining -= step;

        if (elapsedInPhase < FadeDuration)
            return remaining;

        nextPhase();
        return remaining;
    }

    private float GetClockOpacity()
    {
        var progress = Math.Clamp(elapsedInPhase.TotalMilliseconds / FadeDuration.TotalMilliseconds, 0d, 1d);
        return phase switch
        {
            Phase.Pending => 1f,
            Phase.FadeOutClock => (float)(1d - progress),
            Phase.Main => 0f,
            Phase.FadeInClock => (float)progress,
            _ => 1f
        };
    }

    private enum Phase
    {
        Pending,
        FadeOutClock,
        Main,
        FadeInClock
    }
}
