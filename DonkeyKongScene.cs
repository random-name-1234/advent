using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class DonkeyKongScene : ISpecialScene
{
    private const int PlayLeft = 4;
    private const int PlayRight = 60;
    private const int TopLevel = 3;
    private const int ApeX = 6;
    private const int GoalX = 56;

    private const float SimulationStepSeconds = 1f / 30f;
    private const float PlayerRunSpeed = 11.4f;
    private const float PlayerClimbSpeed = 8.1f;
    private const float PlayerJumpDuration = 0.34f;
    private const float BarrelRollSpeed = 10.2f;
    private const float BarrelFallSpeed = 15.4f;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

    private static readonly Girder[] Girders =
    [
        new Girder(28f, 26f),
        new Girder(22f, 24f),
        new Girder(18f, 16f),
        new Girder(12f, 14f)
    ];

    private static readonly Ladder[] Ladders =
    [
        new Ladder(15, 0, 1),
        new Ladder(44, 0, 1),
        new Ladder(23, 1, 2),
        new Ladder(52, 1, 2),
        new Ladder(12, 2, 3),
        new Ladder(36, 2, 3),
        new Ladder(50, 2, 3)
    ];

    private static readonly string[] ApeIdleSpriteA =
    [
        "........",
        "..AAAA..",
        ".AASSAA.",
        "AAAAAAAA",
        "AA.AA.AA",
        "AAAAAAAA",
        "A.A..A.A",
        ".A....A."
    ];

    private static readonly string[] ApeIdleSpriteB =
    [
        "........",
        "..AAAA..",
        ".AASSAA.",
        "AAAAAAAA",
        "AA.AA.AA",
        "AAAA.AAA",
        "A.AAA..A",
        ".A....A."
    ];

    private static readonly string[] ApeThrowSprite =
    [
        "........",
        "..AAAA..",
        ".AASSAAA",
        "AAAAAAAA",
        "AA.A...A",
        "AAAAAAAA",
        "A.A..A.A",
        ".A....A."
    ];

    private static readonly string[] GoalSpriteA =
    [
        ".PP.",
        "PPPP",
        ".SS.",
        ".DD.",
        "DYYD",
        ".DD.",
        "D..D"
    ];

    private static readonly string[] GoalSpriteB =
    [
        ".PP.",
        "PPPP",
        ".SS.",
        ".DD.",
        "DY.D",
        ".DD.",
        "D..D"
    ];

    private static readonly string[] PlayerRunSpriteA =
    [
        "..HH..",
        ".HSSH.",
        "..RR..",
        ".RBBR.",
        ".RBBR.",
        "..RR..",
        ".B..B.",
        "B....B"
    ];

    private static readonly string[] PlayerRunSpriteB =
    [
        "..HH..",
        ".HSSH.",
        "..RR..",
        ".RBBR.",
        "..RR..",
        ".R..R.",
        ".B..B.",
        "..BB.."
    ];

    private static readonly string[] PlayerClimbSpriteA =
    [
        "..HH..",
        ".HSSH.",
        ".R..R.",
        "..BB..",
        ".RBBR.",
        "..RR..",
        ".B..B.",
        ".B..B."
    ];

    private static readonly string[] PlayerClimbSpriteB =
    [
        "..HH..",
        ".HSSH.",
        "..RR..",
        ".RBBR.",
        "..BB..",
        ".R..R.",
        ".B..B.",
        ".B..B."
    ];

    private static readonly string[] PlayerJumpSprite =
    [
        "..HH..",
        ".HSSH.",
        "..RR..",
        ".RBBR.",
        ".RBBR.",
        "..RR..",
        "..BB..",
        ".B..B."
    ];

    private static readonly string[] PlayerDeadSprite =
    [
        "..HH..",
        ".HXXH.",
        "..RR..",
        ".RBBR.",
        "..RR..",
        ".R..R.",
        "..BB..",
        "..BB.."
    ];

    private static readonly string[] PlayerCelebrateSprite =
    [
        ".H..H.",
        ".HSSH.",
        "..RR..",
        ".RBBR.",
        "..RR..",
        ".R..R.",
        ".B..B.",
        "B....B"
    ];

    private static readonly string[] BarrelSpriteA =
    [
        ".OO.",
        "OooO",
        "OooO",
        ".OO."
    ];

    private static readonly string[] BarrelSpriteB =
    [
        ".OO.",
        "OoOo",
        "oOOo",
        ".OO."
    ];

    private static readonly Rgba32 BackgroundTop = new(6, 8, 20);
    private static readonly Rgba32 BackgroundBottom = new(16, 8, 14);
    private static readonly Rgba32 GirderTop = new(246, 90, 82);
    private static readonly Rgba32 GirderBody = new(131, 34, 38);
    private static readonly Rgba32 GirderRivet = new(255, 196, 128);
    private static readonly Rgba32 LadderRail = new(74, 171, 255);
    private static readonly Rgba32 LadderRung = new(210, 238, 255);

    private readonly Random random = new();
    private readonly List<BarrelActor> barrels = new(16);

    private TimeSpan elapsedThisScene;
    private float simulationAccumulator;
    private float simulationClock;
    private float runElapsedSeconds;
    private float runLimitSeconds;
    private float resetBeatSeconds;
    private float routeRightBias;
    private float hesitationChancePerSecond;
    private float jumpConfidence;
    private float barrelIntervalMin;
    private float barrelIntervalMax;
    private bool doomedRun;
    private bool luckyRun;
    private readonly ApeActor ape = new() { X = ApeX };
    private readonly GoalActor goal = new() { X = GoalX };
    private PlayerActor player = new();

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Donkey Kong";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        simulationAccumulator = 0f;
        simulationClock = 0f;
        IsActive = true;
        HidesTime = true;
        ResetRun(true);
    }

    public void Elapsed(TimeSpan timeSpan)
    {
        if (!IsActive) return;

        elapsedThisScene += timeSpan;
        if (elapsedThisScene > SceneDuration)
        {
            IsActive = false;
            HidesTime = false;
            return;
        }

        simulationAccumulator += Math.Clamp((float)timeSpan.TotalSeconds, 0f, 0.25f);
        while (simulationAccumulator >= SimulationStepSeconds)
        {
            simulationAccumulator -= SimulationStepSeconds;
            UpdateSimulation(SimulationStepSeconds);
        }
    }

    public void Draw(Image<Rgba32> img)
    {
        if (!IsActive) return;

        DrawBackground(img);
        DrawGirders(img);
        DrawLadders(img);
        DrawGoal(img);
        DrawApe(img);
        DrawBarrels(img);
        DrawPlayer(img);
    }

    private void UpdateSimulation(float dt)
    {
        simulationClock += dt;

        if (resetBeatSeconds > 0f)
        {
            resetBeatSeconds = MathF.Max(0f, resetBeatSeconds - dt);
            if (resetBeatSeconds <= 0f) ResetRun(false);
            return;
        }

        runElapsedSeconds += dt;
        UpdateApe(dt);
        UpdateBarrels(dt);
        UpdatePlayer(dt);
        ResolvePlayerBarrelCollisions();

        if (player.State == PlayerState.Dead)
        {
            player.StateTime += dt;
            if (player.StateTime >= 0.82f) resetBeatSeconds = 0.28f;
            return;
        }

        if (player.State == PlayerState.Celebrating)
        {
            player.StateTime += dt;
            if (player.StateTime >= 0.95f) resetBeatSeconds = 0.32f;
            return;
        }

        if (runElapsedSeconds >= runLimitSeconds)
        {
            if (player.Level == TopLevel && MathF.Abs(player.X - goal.X) < 10f && random.NextDouble() < 0.65)
                TriggerSuccess();
            else TriggerDeath();
        }
    }

    private void UpdateApe(float dt)
    {
        ape.ThrowPoseSeconds = MathF.Max(0f, ape.ThrowPoseSeconds - dt);
        if (player.State is PlayerState.Dead or PlayerState.Celebrating) return;

        ape.ThrowCooldown -= dt;
        if (ape.ThrowCooldown > 0f) return;

        SpawnBarrel();
        ape.ThrowPoseSeconds = 0.34f;
        ape.ThrowCooldown = RandomBetween(barrelIntervalMin, barrelIntervalMax);
    }

    private void UpdateBarrels(float dt)
    {
        for (var i = barrels.Count - 1; i >= 0; i--)
        {
            var barrel = barrels[i];
            barrel.SpinClock += dt;

            if (barrel.IsFalling)
            {
                barrel.Y += BarrelFallSpeed * dt;
                var landingY = FloorY(barrel.Level, barrel.X) - 1f;
                if (barrel.Y >= landingY)
                {
                    barrel.Y = landingY;
                    barrel.IsFalling = false;
                    barrel.Direction = DownhillDirection(barrel.Level);
                }

                barrels[i] = barrel;
                continue;
            }

            barrel.X += barrel.Direction * BarrelRollSpeed * dt;
            barrel.Y = FloorY(barrel.Level, barrel.X) - 1f;

            var lowEdgeX = GetLowEdgeX(barrel.Level);
            var reachedLowEdge = barrel.Direction > 0
                ? barrel.X >= lowEdgeX - 0.05f
                : barrel.X <= lowEdgeX + 0.05f;

            if (reachedLowEdge)
            {
                barrel.X = lowEdgeX;
                if (barrel.Level > 0)
                {
                    barrel.Level--;
                    barrel.IsFalling = true;
                    barrel.Direction = DownhillDirection(barrel.Level);
                }
                else
                {
                    barrels.RemoveAt(i);
                    continue;
                }
            }

            if (barrel.X < PlayLeft - 5 || barrel.X > PlayRight + 5)
            {
                barrels.RemoveAt(i);
                continue;
            }

            barrels[i] = barrel;
        }
    }

    private void UpdatePlayer(float dt)
    {
        switch (player.State)
        {
            case PlayerState.Running:
                UpdateRunningPlayer(dt);
                break;
            case PlayerState.Climbing:
                UpdateClimbingPlayer(dt);
                break;
            case PlayerState.Waiting:
                UpdateWaitingPlayer(dt);
                break;
            case PlayerState.Jumping:
                UpdateJumpingPlayer(dt);
                break;
            case PlayerState.Celebrating:
            {
                var floor = FloorY(player.Level, player.X) - 1f;
                var bounce = MathF.Max(0f, MathF.Sin(player.StateTime * 10f)) * 1.2f;
                player.Y = floor - bounce;
                break;
            }
            case PlayerState.Dead:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void UpdateRunningPlayer(float dt)
    {
        player.StateTime += dt;
        player.AnimationClock += dt;
        player.HesitationCooldown = MathF.Max(0f, player.HesitationCooldown - dt);
        player.Y = FloorY(player.Level, player.X) - 1f;

        if (TryStartJumpFromHazard())
            return;

        if (player.Level == TopLevel)
        {
            player.Direction = goal.X >= player.X ? 1 : -1;
            player.X = Math.Clamp(player.X + player.Direction * PlayerRunSpeed * dt, PlayLeft, PlayRight);
            player.Y = FloorY(player.Level, player.X) - 1f;

            if (MathF.Abs(player.X - goal.X) <= 1.9f)
            {
                TriggerSuccess();
                return;
            }

            if (player.HesitationCooldown <= 0f &&
                random.NextDouble() < hesitationChancePerSecond * dt * 0.35f)
            {
                StartWaiting(RandomBetween(0.08f, 0.22f));
                return;
            }

            return;
        }

        if (player.TargetLadderIndex < 0)
            player.TargetLadderIndex = ChooseTargetLadder(player.Level);

        if (player.TargetLadderIndex >= 0)
        {
            var ladder = Ladders[player.TargetLadderIndex];
            var dx = ladder.X - player.X;
            player.Direction = dx >= 0f ? 1 : -1;

            if (MathF.Abs(dx) <= 0.68f)
            {
                if (random.NextDouble() < 0.84)
                {
                    StartClimbing(player.TargetLadderIndex);
                    return;
                }

                player.TargetLadderIndex = -1;
                StartWaiting(RandomBetween(0.08f, 0.24f));
                return;
            }
        }

        if (player.HesitationCooldown <= 0f &&
            random.NextDouble() < hesitationChancePerSecond * dt)
        {
            StartWaiting(RandomBetween(0.07f, 0.26f));
            player.HesitationCooldown = RandomBetween(0.28f, 0.55f);
            return;
        }

        var speedScale = doomedRun ? 0.93f : 1f;
        player.X += player.Direction * PlayerRunSpeed * speedScale * dt;
        if (player.X <= PlayLeft + 0.1f || player.X >= PlayRight - 0.1f)
        {
            player.X = Math.Clamp(player.X, PlayLeft, PlayRight);
            player.Direction *= -1;
            player.TargetLadderIndex = -1;
        }

        player.Y = FloorY(player.Level, player.X) - 1f;
    }

    private void UpdateWaitingPlayer(float dt)
    {
        player.StateTime += dt;
        player.AnimationClock += dt;
        player.Y = FloorY(player.Level, player.X) - 1f;
        if (player.StateTime >= player.StateDuration)
        {
            player.State = PlayerState.Running;
            player.StateTime = 0f;
        }
    }

    private void UpdateClimbingPlayer(float dt)
    {
        if (player.TargetLadderIndex < 0)
        {
            player.State = PlayerState.Running;
            player.StateTime = 0f;
            return;
        }

        var ladder = Ladders[player.TargetLadderIndex];
        player.AnimationClock += dt;
        player.X = ladder.X;
        player.Y -= PlayerClimbSpeed * dt;

        var topY = FloorY(ladder.UpperLevel, ladder.X) - 1f;
        if (player.Y > topY) return;

        player.Level = ladder.UpperLevel;
        player.Y = topY;
        player.State = PlayerState.Running;
        player.StateTime = 0f;
        player.TargetLadderIndex = -1;
        player.Direction = random.NextDouble() < routeRightBias ? 1 : -1;
        player.HesitationCooldown = RandomBetween(0.18f, 0.38f);
    }

    private void UpdateJumpingPlayer(float dt)
    {
        player.StateTime += dt;
        player.AnimationClock += dt;

        var progress = Math.Clamp(player.StateTime / PlayerJumpDuration, 0f, 1f);
        player.X += player.Direction * PlayerRunSpeed * 1.08f * dt;
        player.X = Math.Clamp(player.X, PlayLeft, PlayRight);

        var floor = FloorY(player.Level, player.X) - 1f;
        var arc = MathF.Sin(progress * MathF.PI) * 3.35f;
        player.Y = floor - arc;

        if (progress < 1f) return;

        player.State = PlayerState.Running;
        player.StateTime = 0f;
        player.Y = floor;
        player.HesitationCooldown = RandomBetween(0.15f, 0.33f);
    }

    private bool TryStartJumpFromHazard()
    {
        if (player.State != PlayerState.Running || player.HesitationCooldown > 0f)
            return false;

        var nearestDistance = float.MaxValue;
        var hasHazard = false;

        for (var i = 0; i < barrels.Count; i++)
        {
            var barrel = barrels[i];
            if (barrel.Level != player.Level || barrel.IsFalling) continue;

            var deltaX = barrel.X - player.X;
            if (player.Direction > 0 && deltaX < 0f) continue;
            if (player.Direction < 0 && deltaX > 0f) continue;

            var distance = MathF.Abs(deltaX);
            if (distance < 0.7f || distance > 5.1f) continue;

            nearestDistance = MathF.Min(nearestDistance, distance);
            hasHazard = true;
        }

        if (!hasHazard) return false;

        var urgency = 1f - Math.Clamp((nearestDistance - 1.1f) / 3.2f, 0f, 1f);
        var jumpChance = jumpConfidence * (0.58f + urgency * 0.37f);
        if (random.NextDouble() < jumpChance)
        {
            player.State = PlayerState.Jumping;
            player.StateTime = 0f;
            return true;
        }

        if (nearestDistance < 1.65f && random.NextDouble() < (doomedRun ? 0.33 : 0.16))
        {
            StartWaiting(RandomBetween(0.14f, 0.28f));
            return true;
        }

        return false;
    }

    private void ResolvePlayerBarrelCollisions()
    {
        if (player.State is PlayerState.Dead or PlayerState.Celebrating)
            return;

        var playerCenterY = player.Y - 3.2f;
        var isJumping = player.State == PlayerState.Jumping;
        var jumpClearance = FloorY(player.Level, player.X) - 1f - player.Y;

        for (var i = 0; i < barrels.Count; i++)
        {
            var barrel = barrels[i];
            if (!barrel.IsFalling && barrel.Level != player.Level) continue;

            var barrelCenterY = barrel.Y - 1.3f;
            var dx = MathF.Abs(barrel.X - player.X);
            var dy = MathF.Abs(barrelCenterY - playerCenterY);
            if (dx > 2.1f || dy > 3.0f) continue;

            if (isJumping && jumpClearance >= 1.75f && dx <= 1.95f) continue;

            TriggerDeath();
            return;
        }
    }

    private void SpawnBarrel()
    {
        var spawnX = ApeX + 4f;
        var level = TopLevel;
        barrels.Add(new BarrelActor
        {
            X = spawnX,
            Y = FloorY(level, spawnX) - 1f,
            Level = level,
            Direction = DownhillDirection(level),
            IsFalling = false,
            SpinClock = (float)random.NextDouble()
        });
    }

    private void StartWaiting(float durationSeconds)
    {
        player.State = PlayerState.Waiting;
        player.StateTime = 0f;
        player.StateDuration = durationSeconds;
    }

    private void StartClimbing(int ladderIndex)
    {
        player.State = PlayerState.Climbing;
        player.StateTime = 0f;
        player.TargetLadderIndex = ladderIndex;
        player.X = Ladders[ladderIndex].X;
    }

    private int ChooseTargetLadder(int level)
    {
        Span<int> options = stackalloc int[8];
        Span<float> weights = stackalloc float[8];
        var optionCount = 0;

        for (var i = 0; i < Ladders.Length; i++)
        {
            if (Ladders[i].LowerLevel != level) continue;
            options[optionCount++] = i;
        }

        if (optionCount == 0) return -1;
        if (optionCount == 1) return options[0];
        if (random.NextDouble() < 0.12) return options[random.Next(optionCount)];

        var totalWeight = 0f;
        for (var i = 0; i < optionCount; i++)
        {
            var ladder = Ladders[options[i]];
            var normalizedX = (ladder.X - PlayLeft) / (float)(PlayRight - PlayLeft);
            var progressWeight = 0.6f + normalizedX * routeRightBias;
            var distanceWeight = 1.24f - MathF.Min(1f, MathF.Abs(player.X - ladder.X) / 31f) * 0.48f;
            var weight = MathF.Max(0.05f, progressWeight * distanceWeight);

            if (doomedRun && random.NextDouble() < 0.18) weight *= 0.55f;
            if (luckyRun && ladder.X > 40) weight *= 1.16f;

            weights[i] = weight;
            totalWeight += weight;
        }

        var pick = (float)random.NextDouble() * totalWeight;
        var cumulative = 0f;
        for (var i = 0; i < optionCount; i++)
        {
            cumulative += weights[i];
            if (pick <= cumulative) return options[i];
        }

        return options[optionCount - 1];
    }

    private void TriggerDeath()
    {
        if (player.State is PlayerState.Dead or PlayerState.Celebrating) return;

        player.State = PlayerState.Dead;
        player.StateTime = 0f;
        player.TargetLadderIndex = -1;
        if (random.NextDouble() < 0.32) barrels.Clear();
    }

    private void TriggerSuccess()
    {
        if (player.State is PlayerState.Dead or PlayerState.Celebrating) return;

        player.State = PlayerState.Celebrating;
        player.StateTime = 0f;
        player.TargetLadderIndex = -1;
        barrels.Clear();
    }

    private void ResetRun(bool firstRun)
    {
        barrels.Clear();

        runElapsedSeconds = 0f;
        resetBeatSeconds = 0f;
        runLimitSeconds = RandomBetween(13.2f, 15.8f);

        luckyRun = random.NextDouble() < 0.24;
        doomedRun = !luckyRun && random.NextDouble() < 0.38;

        routeRightBias = luckyRun
            ? RandomBetween(0.76f, 0.92f)
            : doomedRun
                ? RandomBetween(0.58f, 0.73f)
                : RandomBetween(0.66f, 0.86f);

        hesitationChancePerSecond = luckyRun
            ? RandomBetween(0.03f, 0.08f)
            : doomedRun
                ? RandomBetween(0.08f, 0.16f)
                : RandomBetween(0.05f, 0.11f);

        jumpConfidence = luckyRun
            ? RandomBetween(0.86f, 0.95f)
            : doomedRun
                ? RandomBetween(0.59f, 0.77f)
                : RandomBetween(0.72f, 0.89f);

        barrelIntervalMin = luckyRun ? 1.65f : doomedRun ? 1.05f : 1.25f;
        barrelIntervalMax = luckyRun ? 2.3f : doomedRun ? 1.75f : 2.0f;
        ape.ThrowCooldown = firstRun ? 0.6f : RandomBetween(0.45f, 0.95f);
        ape.ThrowPoseSeconds = 0f;
        goal.BlinkClock = 0;

        player = new PlayerActor
        {
            X = PlayLeft + 3f,
            Level = 0,
            Direction = 1,
            State = PlayerState.Running,
            StateTime = 0f,
            StateDuration = 0f,
            TargetLadderIndex = -1,
            AnimationClock = 0f,
            HesitationCooldown = RandomBetween(0.06f, 0.18f)
        };
        player.Y = FloorY(player.Level, player.X) - 1f;
    }

    private void DrawBackground(Image<Rgba32> img)
    {
        for (var y = 0; y < Height; y++)
        {
            var t = y / (float)(Height - 1);
            var row = Lerp(BackgroundTop, BackgroundBottom, t);
            for (var x = 0; x < Width; x++)
            {
                var edgeDarken = 1f - MathF.Abs((x - Width / 2f) / (Width / 2f)) * 0.18f;
                img[x, y] = Scale(row, edgeDarken);
            }
        }
    }

    private void DrawGirders(Image<Rgba32> img)
    {
        for (var level = 0; level < Girders.Length; level++)
        {
            for (var x = PlayLeft; x <= PlayRight; x++)
            {
                var y = (int)MathF.Round(FloorY(level, x));
                SetPixel(img, x, y, GirderTop);
                SetPixel(img, x, y + 1, GirderBody);

                if (((x + level) % 7) == 0)
                    SetPixel(img, x, y, GirderRivet);
            }
        }
    }

    private void DrawLadders(Image<Rgba32> img)
    {
        for (var i = 0; i < Ladders.Length; i++)
        {
            var ladder = Ladders[i];
            var yLower = (int)MathF.Round(FloorY(ladder.LowerLevel, ladder.X));
            var yUpper = (int)MathF.Round(FloorY(ladder.UpperLevel, ladder.X));

            for (var y = yUpper; y <= yLower; y++)
            {
                SetPixel(img, ladder.X - 1, y, LadderRail);
                SetPixel(img, ladder.X + 1, y, LadderRail);
                if (((y - yUpper) & 1) == 0) SetPixel(img, ladder.X, y, LadderRung);
            }
        }
    }

    private void DrawApe(Image<Rgba32> img)
    {
        var floor = FloorY(TopLevel, ape.X + 3f) - 1f;
        var spriteX = (int)MathF.Round(ape.X) - 2;
        var spriteY = (int)MathF.Round(floor) - 8;
        var sprite = ape.ThrowPoseSeconds > 0.02f
            ? ApeThrowSprite
            : (((int)(simulationClock * 2.8f) & 1) == 0 ? ApeIdleSpriteA : ApeIdleSpriteB);
        DrawSprite(img, spriteX, spriteY, sprite);
    }

    private void DrawGoal(Image<Rgba32> img)
    {
        var floor = FloorY(TopLevel, goal.X) - 1f;
        var spriteX = (int)MathF.Round(goal.X) - 2;
        var spriteY = (int)MathF.Round(floor) - 7;
        goal.BlinkClock++;
        var sprite = ((goal.BlinkClock / 5) & 1) == 0 ? GoalSpriteA : GoalSpriteB;
        DrawSprite(img, spriteX, spriteY, sprite);
    }

    private void DrawBarrels(Image<Rgba32> img)
    {
        for (var i = 0; i < barrels.Count; i++)
        {
            var barrel = barrels[i];
            var sprite = ((int)(barrel.SpinClock * 11f) & 1) == 0 ? BarrelSpriteA : BarrelSpriteB;
            var x = (int)MathF.Round(barrel.X) - 2;
            var y = (int)MathF.Round(barrel.Y) - 3;
            DrawSprite(img, x, y, sprite);

            if (barrel.IsFalling)
                SetPixel(img, (int)MathF.Round(barrel.X), (int)MathF.Round(barrel.Y) + 1, new Rgba32(255, 186, 84));
        }
    }

    private void DrawPlayer(Image<Rgba32> img)
    {
        string[] sprite;
        switch (player.State)
        {
            case PlayerState.Running:
                sprite = ((int)(player.AnimationClock * 9f) & 1) == 0 ? PlayerRunSpriteA : PlayerRunSpriteB;
                break;
            case PlayerState.Climbing:
                sprite = ((int)(player.AnimationClock * 8f) & 1) == 0 ? PlayerClimbSpriteA : PlayerClimbSpriteB;
                break;
            case PlayerState.Waiting:
                sprite = PlayerRunSpriteA;
                break;
            case PlayerState.Jumping:
                sprite = PlayerJumpSprite;
                break;
            case PlayerState.Dead:
                if (((int)(player.StateTime * 12f) & 1) == 1) return;
                sprite = PlayerDeadSprite;
                break;
            case PlayerState.Celebrating:
                sprite = ((int)(player.AnimationClock * 10f) & 1) == 0 ? PlayerCelebrateSprite : PlayerRunSpriteB;
                break;
            default:
                return;
        }

        var x = (int)MathF.Round(player.X) - 3;
        var y = (int)MathF.Round(player.Y) - 7;
        DrawSprite(img, x, y, sprite);

        if (player.State == PlayerState.Celebrating)
        {
            SetPixel(img, x + 1, y - 1, new Rgba32(255, 232, 120));
            SetPixel(img, x + 4, y - 2, new Rgba32(255, 232, 120));
        }
    }

    private static void DrawSprite(Image<Rgba32> img, int x, int y, string[] sprite)
    {
        for (var row = 0; row < sprite.Length; row++)
        {
            var line = sprite[row];
            for (var col = 0; col < line.Length; col++)
            {
                var color = MapSpriteColor(line[col]);
                if (!color.HasValue) continue;
                SetPixel(img, x + col, y + row, color.Value);
            }
        }
    }

    private static Rgba32? MapSpriteColor(char token)
    {
        return token switch
        {
            '.' => null,
            'A' => new Rgba32(160, 92, 42),
            'S' => new Rgba32(244, 203, 146),
            'H' => new Rgba32(238, 64, 62),
            'R' => new Rgba32(226, 58, 56),
            'B' => new Rgba32(80, 140, 255),
            'G' => new Rgba32(52, 82, 132),
            'X' => new Rgba32(255, 220, 200),
            'P' => new Rgba32(255, 126, 182),
            'D' => new Rgba32(228, 104, 164),
            'Y' => new Rgba32(255, 228, 125),
            'O' => new Rgba32(226, 140, 54),
            'o' => new Rgba32(124, 70, 24),
            _ => null
        };
    }

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x >= Width || (uint)y >= Height) return;
        img[x, y] = color;
    }

    private static float FloorY(int level, float x)
    {
        var girder = Girders[level];
        var t = Math.Clamp((x - PlayLeft) / (PlayRight - PlayLeft), 0f, 1f);
        return girder.LeftY + (girder.RightY - girder.LeftY) * t;
    }

    private static int DownhillDirection(int level)
    {
        var girder = Girders[level];
        return girder.RightY > girder.LeftY ? 1 : -1;
    }

    private static float GetLowEdgeX(int level)
    {
        var girder = Girders[level];
        return girder.LeftY > girder.RightY ? PlayLeft : PlayRight;
    }

    private float RandomBetween(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static Rgba32 Lerp(Rgba32 a, Rgba32 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Rgba32(
            ToByte(a.R + (b.R - a.R) * t),
            ToByte(a.G + (b.G - a.G) * t),
            ToByte(a.B + (b.B - a.B) * t));
    }

    private static Rgba32 Scale(Rgba32 color, float intensity)
    {
        intensity = Math.Clamp(intensity, 0f, 1f);
        return new Rgba32(
            ToByte(color.R * intensity),
            ToByte(color.G * intensity),
            ToByte(color.B * intensity));
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private readonly record struct Girder(float LeftY, float RightY);

    private readonly record struct Ladder(int X, int LowerLevel, int UpperLevel);

    private sealed class PlayerActor
    {
        public float X;
        public float Y;
        public int Level;
        public int Direction;
        public PlayerState State;
        public float StateTime;
        public float StateDuration;
        public int TargetLadderIndex;
        public float AnimationClock;
        public float HesitationCooldown;
    }

    private struct BarrelActor
    {
        public float X;
        public float Y;
        public int Level;
        public int Direction;
        public bool IsFalling;
        public float SpinClock;
    }

    private sealed class ApeActor
    {
        public float X;
        public float ThrowCooldown;
        public float ThrowPoseSeconds;
    }

    private sealed class GoalActor
    {
        public float X;
        public int BlinkClock;
    }

    private enum PlayerState
    {
        Running,
        Climbing,
        Waiting,
        Jumping,
        Dead,
        Celebrating
    }
}
