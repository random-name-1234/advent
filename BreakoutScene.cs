using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.PixelArt;

namespace advent;

internal sealed class BreakoutScene() : PixelStoryScene("Breakout")
{
    private const double Step = 1.0 / 240;
    private readonly bool[,] bricks = new bool[4, 8];
    private double accumulator;
    private double simulationTime;
    private double pause;
    private double paddle;
    private double x, y, vx, vy;
    private int returns;
    internal int DestroyedBricks { get; private set; }
    internal int Misses { get; private set; }
    internal (double X, double Y) Ball => (x, y);

    public override void Activate()
    {
        base.Activate();
        accumulator = simulationTime = pause = 0;
        DestroyedBricks = Misses = returns = 0;
        paddle = 31;
        FillBricks();
        Serve();
    }

    public override void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive || timeSpan <= TimeSpan.Zero) return;
        accumulator += Math.Min(timeSpan.TotalSeconds, 20 - Seconds);
        while (accumulator + 1e-10 >= Step)
        {
            Tick();
            accumulator -= Step;
        }
        base.Elapsed(timeSpan);
    }

    private void FillBricks()
    {
        for (var row = 0; row < 4; row++)
        for (var col = 0; col < 8; col++) bricks[row, col] = true;
    }

    private void Serve()
    {
        x = paddle;
        y = 25;
        vx = Misses % 2 == 0 ? 12 : -13;
        vy = -22;
        pause = .7;
    }

    private void Tick()
    {
        simulationTime += Step;
        // Track with a speed-limited paddle; every fifth return is deliberately
        // misjudged, rather than teleporting the ball or scripting a fake loss.
        var target = vy > 0 && returns % 5 == 4 ? 63 - x : x + Math.Sin(simulationTime * 1.7) * 1.2;
        paddle = Math.Clamp(paddle + Math.Clamp(target - paddle, -29 * Step, 29 * Step), 5, 58);
        if (pause > 0)
        {
            pause -= Step;
            x = paddle;
            return;
        }

        var oldX = x;
        var oldY = y;
        x += vx * Step;
        y += vy * Step;
        if (x < 2) { x = 4 - x; vx = Math.Abs(vx); }
        if (x > 61) { x = 122 - x; vx = -Math.Abs(vx); }
        if (y < 2) { y = 4 - y; vy = Math.Abs(vy); }

        var hit = false;
        for (var row = 0; row < 4 && !hit; row++)
        for (var col = 0; col < 8 && !hit; col++)
        {
            if (!bricks[row, col]) continue;
            var left = 4 + col * 7 - .6;
            var right = left + 6.2;
            var top = 4 + row * 3 - .6;
            var bottom = top + 2.2;
            if (x < left || x > right || y < top || y > bottom) continue;
            bricks[row, col] = false;
            DestroyedBricks++;
            if (oldY <= top || oldY >= bottom)
            {
                y = oldY <= top ? top - .01 : bottom + .01;
                vy = -vy;
            }
            else
            {
                x = oldX <= left ? left - .01 : right + .01;
                vx = -vx;
            }
            hit = true;
        }

        if (vy > 0 && oldY < 27 && y >= 27 && Math.Abs(x - paddle) <= 5.5)
        {
            var offset = Math.Clamp((x - paddle) / 5.5, -1, 1);
            vx = offset * 20 + 2 * Math.Sin(returns + 1);
            vy = -Math.Sqrt(27 * 27 - vx * vx);
            y = 27 - (y - 27);
            returns++;
        }
        if (y > 32)
        {
            Misses++;
            returns++;
            Serve();
        }
        if (DestroyedBricks > 0 && DestroyedBricks % 32 == 0 && hit) FillBricks();
    }

    protected override void Render(Image<Rgba32> image)
    {
        Box(image, 0, 0, 64, 32, Color(1, 3, 7));
        Box(image, 0, 0, 64, 1, Color(51, 67, 83));
        Box(image, 0, 0, 1, 32, Color(51, 67, 83));
        Box(image, 63, 0, 1, 32, Color(51, 67, 83));
        Rgba32[] colors = [Color(224, 68, 55), Color(233, 148, 56), Color(182, 193, 68), Color(59, 165, 145)];
        for (var row = 0; row < 4; row++)
        for (var col = 0; col < 8; col++)
            if (bricks[row, col]) Box(image, 4 + col * 7, 4 + row * 3, 5, 2, colors[row]);
        Box(image, (int)Math.Round(paddle) - 5, 28, 11, 2, Color(162, 199, 215));
        Dot(image, (int)Math.Round(x), (int)Math.Round(y), Color(255, 246, 211));
    }
}
