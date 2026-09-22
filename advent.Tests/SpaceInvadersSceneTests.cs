using System.Collections;
using System.Reflection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;
using Xunit.Abstractions;

namespace advent.Tests;

public sealed class SpaceInvadersSceneTests(ITestOutputHelper log)
{
    [Theory]
    [InlineData(6, "win")]
    [InlineData(2, "close-loss")]
    public void CaptureCompleteRoundWhenRequested(int seed, string name)
    {
        var output = Environment.GetEnvironmentVariable("ADVENT_INVADERS_CAPTURE_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Directory.CreateDirectory(output);
        var scene = new SpaceInvadersScene(seed);
        scene.Activate();
        using var animation = new Image<Rgba32>(64, 32);
        using var sheet = new Image<Rgba32>(68 * 6, 36, new Rgba32(18, 18, 18));
        var capturedResult = false;
        for (var frame = 0; frame < 900 && scene.IsActive; frame++)
        {
            scene.Elapsed(TimeSpan.FromMilliseconds(50));
            if (!scene.IsActive) break;
            using var image = new Image<Rgba32>(64, 32);
            scene.Draw(image);
            image.Frames.RootFrame.Metadata.GetGifMetadata().FrameDelay = 5;
            animation.Frames.AddFrame(image.Frames.RootFrame);
            var column = frame < 400 && frame % 80 == 0 ? frame / 80 : -1;
            if (!capturedResult && scene.Result != SpaceInvadersScene.RoundResult.Playing &&
                frame * .05 > scene.ResultAtSeconds + .6)
            {
                column = 5;
                capturedResult = true;
            }
            if (column >= 0)
                sheet.Mutate(context => context.DrawImage(image, new Point(column * 68 + 2, 2), 1));
        }
        Assert.True(capturedResult);
        animation.Frames.RemoveFrame(0);
        animation.SaveAsGif(Path.Combine(output, $"{name}.gif"));
        sheet.Mutate(context => context.Resize(sheet.Width * 3, sheet.Height * 3, KnownResamplers.NearestNeighbor));
        sheet.SaveAsPng(Path.Combine(output, $"{name}-sheet.png"));
    }

    [Fact]
    public void PlayerEarnsWinsButStillLosesAcrossSeededRounds()
    {
        var wins = 0;
        var losses = 0;
        for (var seed = 0; seed < 1000; seed++)
        {
            var scene = Play(seed, [20]);
            Assert.False(scene.IsActive);
            Assert.False(scene.HidesTime);
            Assert.InRange(scene.ResultAtSeconds, 1, 42.1);
            if (scene.Result == SpaceInvadersScene.RoundResult.Won)
            {
                wins++;
                Assert.Equal(0, scene.AliveInvaders);
                Assert.True(scene.ShotsFired >= 21);
            }
            else
            {
                Assert.Equal(SpaceInvadersScene.RoundResult.Lost, scene.Result);
                Assert.InRange(scene.AliveInvaders, 1, 21);
                losses++;
            }
        }
        // Broad guardrails, not a preselected outcome or an exact random sequence.
        log.WriteLine($"Seeds 0-999: {wins} wins, {losses} losses.");
        Assert.InRange(wins, 500, 800);
        Assert.InRange(losses, 200, 500);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(360)]
    public void OutcomeDoesNotDependOnElapsedChunkSize(int seed)
    {
        var expected = Play(seed, [20]);
        foreach (var cadence in new[] { new[] { 100 }, new[] { 7, 93, 250, 16 }, new[] { 45000 } })
        {
            var actual = Play(seed, cadence);
            Assert.Equal(expected.Result, actual.Result);
            Assert.Equal(expected.ResultAtSeconds, actual.ResultAtSeconds);
            Assert.Equal(expected.AliveInvaders, actual.AliveInvaders);
            Assert.Equal(expected.ShotsFired, actual.ShotsFired);
        }
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(6, true)]
    public void RoundHoldsItsResultWithoutRestartingAndCanReactivate(int seed, bool won)
    {
        var scene = new SpaceInvadersScene(seed);
        scene.Activate();
        for (var tick = 0; tick < 2101 && scene.Result == SpaceInvadersScene.RoundResult.Playing; tick++)
            scene.Elapsed(TimeSpan.FromMilliseconds(20));
        Assert.Equal(won ? SpaceInvadersScene.RoundResult.Won : SpaceInvadersScene.RoundResult.Lost, scene.Result);
        var remaining = scene.AliveInvaders;
        var shots = scene.ShotsFired;
        scene.Elapsed(TimeSpan.FromSeconds(2));
        Assert.True(scene.IsActive);
        Assert.True(scene.HidesTime);
        Assert.Equal(remaining, scene.AliveInvaders);
        Assert.Equal(shots, scene.ShotsFired);
        using var frame = new Image<Rgba32>(64, 32);
        scene.Draw(frame);
        var textColor = won ? new Rgba32(138, 255, 184) : new Rgba32(255, 176, 126);
        Assert.Contains(Enumerable.Range(11 * 64, 5 * 64), i => frame[i % 64, i / 64] == textColor);
        Assert.InRange(RailDmiText.MeasureWidth(won ? "WAVE CLEAR" : "GAME OVER"), 1, 60);
        scene.Elapsed(TimeSpan.FromSeconds(1));
        Assert.False(scene.IsActive);
        Assert.False(scene.HidesTime);

        scene.Activate();
        Assert.True(scene.IsActive);
        Assert.Equal(SpaceInvadersScene.RoundResult.Playing, scene.Result);
        Assert.Equal(21, scene.AliveInvaders);
        Assert.Equal(0, scene.ShotsFired);
        Assert.Equal(0, scene.ResultAtSeconds);
    }

    [Fact]
    public void NonPositiveElapsedDoesNotAdvanceSimulation()
    {
        var scene = new SpaceInvadersScene(6);
        scene.Activate();
        scene.Elapsed(TimeSpan.FromSeconds(-10));
        scene.Elapsed(TimeSpan.Zero);
        Assert.Equal(21, scene.AliveInvaders);
        Assert.Equal(0, scene.ShotsFired);
        Assert.Equal(0d, Get<double>(scene, "simulationSeconds"));
    }

    [Fact]
    public void OnlyLivingRowsTriggerInvasionAtTheShipNotTheShields()
    {
        var scene = new SpaceInvadersScene(6);
        scene.Activate();
        KeepOneInvader(scene, row: 0, col: 0);
        Set(scene, "formationY", 21f);
        Set(scene, "formationStepCooldown", 0f);
        Invoke(scene, "UpdateFormation", 0f);
        Assert.Equal(SpaceInvadersScene.RoundResult.Playing, scene.Result);
        // A real survivor at ship height still ends the game.
        Set(scene, "formationY", 24f);
        Set(scene, "formationStepCooldown", 0f);
        Invoke(scene, "UpdateFormation", 0f);
        Assert.Equal(SpaceInvadersScene.RoundResult.Lost, scene.Result);
    }

    [Fact]
    public void DestroyedColumnsNoLongerCausePhantomEdgeBounces()
    {
        var scene = new SpaceInvadersScene(6);
        scene.Activate();
        KeepOneInvader(scene, row: 0, col: 0);
        Set(scene, "formationX", 11f);
        Set(scene, "formationStepCooldown", 0f);
        Invoke(scene, "UpdateFormation", 0f);
        Assert.Equal(12f, Get<float>(scene, "formationX"));
        Assert.Equal(4f, Get<float>(scene, "formationY"));
        Set(scene, "formationX", 57f);
        Set(scene, "formationStepCooldown", 0f);
        Invoke(scene, "UpdateFormation", 0f);
        Assert.Equal(-1f, Get<float>(scene, "formationDirection"));
        Assert.Equal(6f, Get<float>(scene, "formationY"));
    }

    [Fact]
    public void PlayerLeadsMovingTargetsAndCanDrillBlockedShotLanes()
    {
        var scene = new SpaceInvadersScene(6);
        scene.Activate();
        KeepOneInvader(scene, row: 0, col: 0);
        Set(scene, "formationX", 19f);
        var invader = Get<IList>(scene, "invaders")[0]!;
        var predicted = (float)Invoke(scene, "PredictTargetX", invader)!;
        Assert.True(predicted > 19);
        Assert.True((bool)Invoke(scene, "CanTakeShot", predicted)!);
        Assert.False((bool)Invoke(scene, "CanTakeShot", 19f)!);
        var shields = Get<bool[,]>(scene, "shields");
        var x = (int)MathF.Round(predicted);
        shields[x, 24] = true;
        Set(scene, "playerX", predicted);
        Set(scene, "playerTargetX", predicted);
        Set(scene, "playerDecisionCooldown", 1f);
        Set(scene, "playerFireCooldown", 0f);
        Invoke(scene, "UpdatePlayer", 0f);
        Assert.Equal(1, scene.ShotsFired);
        for (var i = 0; i < 5; i++) Invoke(scene, "UpdateBolts", 1f / 30);
        Assert.False(shields[x, 24]);
        Assert.Equal(1, scene.AliveInvaders);
    }

    [Fact]
    public void DodgeChecksThePathAndDoesNotWalkBackIntoABolt()
    {
        var scene = new SpaceInvadersScene(6);
        scene.Activate();
        Set(scene, "playerX", 20f);
        var bolt = Activator.CreateInstance(typeof(SpaceInvadersScene).GetNestedType("BoltActor", BindingFlags.NonPublic)!)!;
        Set(bolt, "X", 26f);
        Set(bolt, "Y", 24f);
        Set(bolt, "VelocityY", 12.65f);
        Get<IList>(scene, "bolts").Add(bolt);
        Assert.Equal(0f, (float)Invoke(scene, "EvaluateLaneSafety", 20f)!);
        // Destination x=40 is clear, but reaching it would cross the falling bolt.
        Assert.True((float)Invoke(scene, "EvaluateLaneSafety", 40f)! < -1);
        Assert.True((bool)Invoke(scene, "TryChooseDodgeTarget", 40f)!);
        Assert.True(Get<float>(scene, "playerTargetX") < 26);
    }

    [Fact]
    public void StalledRoundTimesOutAndLeavesTimeForItsResult()
    {
        var scene = new SpaceInvadersScene(6);
        scene.Activate();
        foreach (var field in new[] { "formationStepCooldown", "enemyFireCooldown", "playerFireCooldown" })
            Set(scene, field, 1000f);
        scene.Elapsed(TimeSpan.FromSeconds(43));
        Assert.Equal(SpaceInvadersScene.RoundResult.TimedOut, scene.Result);
        Assert.True(scene.IsActive);
        Assert.Equal(21, scene.AliveInvaders);
        Assert.InRange(RailDmiText.MeasureWidth("TIME UP"), 1, 60);
        scene.Elapsed(TimeSpan.FromSeconds(2));
        Assert.False(scene.IsActive);
    }

    private static SpaceInvadersScene Play(int seed, int[] cadence)
    {
        var scene = new SpaceInvadersScene(seed);
        scene.Activate();
        var elapsed = 0;
        for (var tick = 0; elapsed < 45000 && scene.IsActive; tick++)
        {
            var ms = Math.Min(cadence[tick % cadence.Length], 45000 - elapsed);
            elapsed += ms;
            scene.Elapsed(TimeSpan.FromMilliseconds(ms));
        }
        return scene;
    }

    private static void KeepOneInvader(SpaceInvadersScene scene, int row, int col)
    {
        var invaders = Get<IList>(scene, "invaders");
        for (var i = 0; i < invaders.Count; i++)
        {
            var invader = invaders[i]!;
            Set(invader, "IsAlive", Get<int>(invader, "Row") == row && Get<int>(invader, "Col") == col);
            invaders[i] = invader;
        }
    }

    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Fields)!.GetValue(target)!;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields)!.SetValue(target, value);
    private static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Fields)!.Invoke(target, args);
}
