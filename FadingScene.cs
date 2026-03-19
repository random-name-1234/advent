using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent;

public class FadingScene : ISpecialScene
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(1);

    private readonly ISpecialScene mainScene;
    private bool clockHiddenDuringMain;
    private Image<Rgba32>? frameBuffer;
    private Phase phase;
    private TimeSpan elapsedInPhase;

    public FadingScene(ISpecialScene mainScene)
    {
        this.mainScene = mainScene ?? throw new ArgumentNullException(nameof(mainScene));
    }

    public bool IsActive { get; private set; }

    public bool HidesTime => IsActive && (phase == Phase.Main ? mainScene.HidesTime : CrossfadesClock);

    public bool RainbowSnow => IsActive && mainScene.RainbowSnow;
    public string Name => mainScene.Name;
    public float Opacity => GetOpacity();
    public bool CrossfadesClock => clockHiddenDuringMain || mainScene.HidesTime;

    public void Activate()
    {
        mainScene.Activate();
        clockHiddenDuringMain = mainScene.HidesTime;
        phase = Phase.FadeIn;
        elapsedInPhase = TimeSpan.Zero;
        IsActive = true;
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive)
            return;

        EnsureFrameBuffer(img.Width, img.Height);

        if (phase != Phase.FadeOut)
            RenderMainSceneToFrameBuffer();

        var opacity = GetOpacity();
        if (opacity <= 0f || frameBuffer is null)
            return;

        img.Mutate(ctx => ctx.DrawImage(frameBuffer, new Point(0, 0), opacity));
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
                case Phase.FadeIn:
                    remaining = AdvanceFade(remaining, Phase.Main);
                    break;

                case Phase.Main:
                    clockHiddenDuringMain |= mainScene.HidesTime;
                    mainScene.Elapsed(remaining);
                    clockHiddenDuringMain |= mainScene.HidesTime;
                    remaining = TimeSpan.Zero;
                    if (!mainScene.IsActive)
                    {
                        phase = Phase.FadeOut;
                        elapsedInPhase = TimeSpan.Zero;
                    }

                    break;

                case Phase.FadeOut:
                    remaining = AdvanceFade(remaining, nextPhase: null);
                    break;
            }
        }
    }

    private float GetOpacity()
    {
        var progress = Math.Clamp(elapsedInPhase.TotalMilliseconds / FadeDuration.TotalMilliseconds, 0d, 1d);
        return phase switch
        {
            Phase.FadeIn => (float)progress,
            Phase.Main => 1f,
            Phase.FadeOut => (float)(1d - progress),
            _ => 1f
        };
    }

    private TimeSpan AdvanceFade(TimeSpan remaining, Phase? nextPhase)
    {
        var phaseRemaining = FadeDuration - elapsedInPhase;
        var step = remaining <= phaseRemaining ? remaining : phaseRemaining;
        elapsedInPhase += step;
        remaining -= step;

        if (elapsedInPhase < FadeDuration)
            return remaining;

        if (nextPhase is null)
        {
            IsActive = false;
            elapsedInPhase = FadeDuration;
            return TimeSpan.Zero;
        }

        phase = nextPhase.Value;
        elapsedInPhase = TimeSpan.Zero;
        return remaining;
    }

    private void EnsureFrameBuffer(int width, int height)
    {
        if (frameBuffer is not null && frameBuffer.Width == width && frameBuffer.Height == height)
            return;

        frameBuffer?.Dispose();
        frameBuffer = new Image<Rgba32>(width, height);
    }

    private void RenderMainSceneToFrameBuffer()
    {
        if (frameBuffer is null)
            return;

        for (var y = 0; y < frameBuffer.Height; y++)
        for (var x = 0; x < frameBuffer.Width; x++)
            frameBuffer[x, y] = Color.Black;

        mainScene.Draw(frameBuffer);
    }

    private enum Phase
    {
        FadeIn,
        Main,
        FadeOut
    }
}
