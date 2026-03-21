using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using static advent.MatrixConstants;

namespace advent;

public class SpaceInvadersScene : ISpecialScene
{
    private const int FormationRows = 3;
    private const int FormationCols = 7;
    private const int FormationSpacingX = 7;
    private const int FormationSpacingY = 5;
    private const int PlayerY = 28;
    private const float SimulationStepSeconds = 1f / 30f;
    private const float PlayerSpeed = 22f;

    private static readonly TimeSpan SceneDuration = TimeSpan.FromSeconds(18);

    private static readonly string[] InvaderTopFrameA =
    [
        ".X.X.",
        "XXXXX",
        "X.X.X",
        ".X.X."
    ];

    private static readonly string[] InvaderTopFrameB =
    [
        ".X.X.",
        "XXXXX",
        ".XXX.",
        "X...X"
    ];

    private static readonly string[] InvaderMidFrameA =
    [
        "..X..",
        ".XXX.",
        "XXXXX",
        "X.X.X"
    ];

    private static readonly string[] InvaderMidFrameB =
    [
        "..X..",
        "XXXXX",
        ".XXX.",
        "X.X.X"
    ];

    private static readonly string[] InvaderLowFrameA =
    [
        ".XXX.",
        "X.X.X",
        "XXXXX",
        "..X.."
    ];

    private static readonly string[] InvaderLowFrameB =
    [
        ".XXX.",
        "XXXXX",
        "X.X.X",
        ".X.X."
    ];

    private static readonly string[] PlayerSprite =
    [
        "...X...",
        "..XXX..",
        ".XX.XX.",
        "XXXXXXX"
    ];

    private static readonly string[] ExplosionSprite =
    [
        "X...X",
        ".X.X.",
        "..X..",
        ".X.X.",
        "X...X"
    ];

    private static readonly string[] ShieldMask =
    [
        ".XXXXXX.",
        "XXXXXXXX",
        "XXX..XXX",
        "XX....XX"
    ];

    private static readonly Rgba32[] RowColors =
    [
        new(124, 255, 210),
        new(255, 164, 112),
        new(196, 146, 255)
    ];

    private readonly Random random = new();
    private readonly List<InvaderActor> invaders = new(FormationRows * FormationCols);
    private readonly List<BoltActor> bolts = new(24);
    private readonly bool[,] shields = new bool[Width, Height];

    private int preferredAttackColumn;
    private TimeSpan elapsedThisScene;
    private float animationClock;
    private float enemyFireCooldown;
    private float formationDirection;
    private float formationStepCooldown;
    private float formationX;
    private float formationY;
    private float playerDecisionCooldown;
    private float playerExplosionSeconds;
    private float playerFireCooldown;
    private float playerTargetX;
    private float playerX;
    private float resetBeatSeconds;
    private float simulationAccumulator;
    private int waveNumber;

    public bool IsActive { get; private set; }
    public bool HidesTime { get; private set; }
    public bool RainbowSnow => false;
    public string Name => "Space Invaders";

    public void Activate()
    {
        elapsedThisScene = TimeSpan.Zero;
        simulationAccumulator = 0f;
        animationClock = 0f;
        waveNumber = 0;
        preferredAttackColumn = -1;
        IsActive = true;
        HidesTime = true;
        ResetWave(true);
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
        DrawShields(img);
        DrawInvaders(img);
        DrawBolts(img);
        DrawPlayer(img);
    }

    private void UpdateSimulation(float dt)
    {
        animationClock += dt;

        if (resetBeatSeconds > 0f)
        {
            resetBeatSeconds = MathF.Max(0f, resetBeatSeconds - dt);
            if (resetBeatSeconds <= 0f)
                ResetWave(false);
            return;
        }

        UpdateFormation(dt);
        UpdateEnemyFire(dt);
        UpdatePlayer(dt);
        UpdateBolts(dt);
        UpdateInvaderExplosions(dt);

        if (CountAliveInvaders() == 0)
            resetBeatSeconds = 0.42f;
    }

    private void UpdateFormation(float dt)
    {
        formationStepCooldown -= dt;
        if (formationStepCooldown > 0f)
            return;

        formationStepCooldown += ComputeFormationStepInterval();

        var nextX = formationX + formationDirection;
        var left = nextX;
        var right = nextX + (FormationCols - 1) * FormationSpacingX + 4f;
        if (left < 6f || right > Width - 7f)
        {
            formationDirection *= -1f;
            formationY += 2f;
        }
        else
        {
            formationX = nextX;
        }

        var formationBottom = formationY + (FormationRows - 1) * FormationSpacingY + 3f;
        if (formationBottom >= 22f && playerExplosionSeconds <= 0f)
            TriggerPlayerHit();
    }

    private void UpdateEnemyFire(float dt)
    {
        enemyFireCooldown -= dt;
        if (enemyFireCooldown > 0f || CountAliveInvaders() == 0)
            return;

        enemyFireCooldown = MathF.Max(0.42f, 0.92f - waveNumber * 0.07f) + (float)random.NextDouble() * 0.2f;
        SpawnEnemyBolt();
    }

    private void UpdatePlayer(float dt)
    {
        if (playerExplosionSeconds > 0f)
        {
            playerExplosionSeconds = MathF.Max(0f, playerExplosionSeconds - dt);
            if (playerExplosionSeconds <= 0f)
                resetBeatSeconds = 0.32f;
            return;
        }

        playerDecisionCooldown = MathF.Max(0f, playerDecisionCooldown - dt);
        playerFireCooldown = MathF.Max(0f, playerFireCooldown - dt);

        var preferredAttackX = ChooseAttackTargetX();
        if (TryChooseDodgeTarget(preferredAttackX))
        {
            playerDecisionCooldown = 0.1f;
        }
        else if (playerDecisionCooldown <= 0f)
        {
            playerTargetX = preferredAttackX;
            playerDecisionCooldown = 0.28f + (float)random.NextDouble() * 0.08f;
        }

        var delta = playerTargetX - playerX;
        var maxStep = PlayerSpeed * dt;
        if (MathF.Abs(delta) <= maxStep)
            playerX = playerTargetX;
        else
            playerX += MathF.Sign(delta) * maxStep;

        if (playerFireCooldown <= 0f && !HasActivePlayerBolt() && CanTakeShot(playerX))
        {
            bolts.Add(new BoltActor
            {
                X = playerX,
                Y = PlayerY - 1,
                VelocityY = -24f,
                IsPlayer = true,
                Color = new Rgba32(138, 255, 184)
            });

            playerFireCooldown = 0.22f + (float)random.NextDouble() * 0.08f;
        }
    }

    private bool TryChooseDodgeTarget(float preferredAttackX)
    {
        var currentSafety = EvaluateLaneSafety(playerX);
        if (currentSafety > -0.3f)
            return false;

        var bestX = playerX;
        var bestScore = float.NegativeInfinity;
        for (var candidateX = 4f; candidateX <= 59f; candidateX += 1f)
        {
            var safety = EvaluateLaneSafety(candidateX);
            var score = safety
                        - MathF.Abs(candidateX - preferredAttackX) * 0.11f
                        - MathF.Abs(candidateX - playerX) * 0.035f;

            if (IsShotLaneClear(candidateX))
                score += 0.45f;

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestX = candidateX;
        }

        if (bestScore <= currentSafety + 0.1f)
            return false;

        playerTargetX = bestX;
        return true;
    }

    private float ChooseAttackTargetX()
    {
        var bestX = Width / 2f;
        var bestScore = float.NegativeInfinity;

        for (var col = 0; col < FormationCols; col++)
        {
            var invader = GetLowestAliveInvaderInColumn(col);
            if (invader is null)
                continue;

            var (x, _) = GetInvaderPosition(invader.Value.Row, invader.Value.Col);
            var score = invader.Value.Row * 4.5f
                        - MathF.Abs(playerX - x) * 0.34f
                        + (preferredAttackColumn == col ? 2.5f : 0f)
                        + (IsShotLaneClear(x) ? 3.5f : -1.8f);

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestX = x;
            preferredAttackColumn = col;
        }

        return bestX;
    }

    private bool CanTakeShot(float shotX)
    {
        if (!IsShotLaneClear(shotX))
            return false;

        for (var i = 0; i < invaders.Count; i++)
        {
            var invader = invaders[i];
            if (!invader.IsAlive)
                continue;

            var (x, y) = GetInvaderPosition(invader.Row, invader.Col);
            if (MathF.Abs(x - shotX) > 0.9f || y >= PlayerY)
                continue;

            return true;
        }

        return false;
    }

    private void UpdateBolts(float dt)
    {
        for (var i = bolts.Count - 1; i >= 0; i--)
        {
            var bolt = bolts[i];
            var oldY = bolt.Y;
            bolt.Y += bolt.VelocityY * dt;

            var startY = (int)MathF.Round(oldY);
            var endY = (int)MathF.Round(bolt.Y);
            var step = endY >= startY ? 1 : -1;
            var currentY = startY;
            var hitSomething = false;

            while (true)
            {
                if (!HandleBoltAt(bolt, (int)MathF.Round(bolt.X), currentY))
                {
                    hitSomething = true;
                    break;
                }

                if (currentY == endY)
                    break;

                currentY += step;
            }

            if (hitSomething || bolt.Y < -2f || bolt.Y > Height + 2f)
            {
                bolts.RemoveAt(i);
                continue;
            }

            bolts[i] = bolt;
        }
    }

    private bool HandleBoltAt(BoltActor bolt, int x, int y)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return true;

        if (shields[x, y])
        {
            DamageShield(x, y);
            return false;
        }

        if (bolt.IsPlayer)
        {
            for (var i = 0; i < invaders.Count; i++)
            {
                var invader = invaders[i];
                if (!invader.IsAlive)
                    continue;

                var (ix, iy) = GetInvaderPosition(invader.Row, invader.Col);
                if (x < ix - 2 || x > ix + 2 || y < iy - 1 || y > iy + 2)
                    continue;

                invader.IsAlive = false;
                invader.ExplosionTime = 0.24f;
                invaders[i] = invader;
                return false;
            }

            return true;
        }

        if (playerExplosionSeconds > 0f)
            return true;

        var playerCenterX = (int)MathF.Round(playerX);
        if (x >= playerCenterX - 3 && x <= playerCenterX + 3 && y >= PlayerY - 2 && y <= PlayerY + 1)
        {
            TriggerPlayerHit();
            return false;
        }

        return true;
    }

    private void UpdateInvaderExplosions(float dt)
    {
        for (var i = 0; i < invaders.Count; i++)
        {
            var invader = invaders[i];
            if (invader.IsAlive || invader.ExplosionTime <= 0f)
                continue;

            invader.ExplosionTime = MathF.Max(0f, invader.ExplosionTime - dt);
            invaders[i] = invader;
        }
    }

    private void SpawnEnemyBolt()
    {
        var candidates = new List<InvaderActor>(FormationCols);
        for (var col = 0; col < FormationCols; col++)
        {
            var invader = GetLowestAliveInvaderInColumn(col);
            if (invader is { } candidate)
                candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return;

        InvaderActor shooter;
        if (random.NextDouble() < 0.62)
        {
            shooter = candidates[0];
            var bestDistance = float.MaxValue;
            for (var i = 0; i < candidates.Count; i++)
            {
                var (x, _) = GetInvaderPosition(candidates[i].Row, candidates[i].Col);
                var distance = MathF.Abs(x - playerX);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                shooter = candidates[i];
            }
        }
        else
        {
            shooter = candidates[random.Next(candidates.Count)];
        }

        var (sx, sy) = GetInvaderPosition(shooter.Row, shooter.Col);
        bolts.Add(new BoltActor
        {
            X = sx,
            Y = sy + 3,
            VelocityY = 12f + waveNumber * 0.65f,
            IsPlayer = false,
            Color = new Rgba32(255, 118, 126)
        });
    }

    private int CountAliveInvaders()
    {
        var count = 0;
        for (var i = 0; i < invaders.Count; i++)
            if (invaders[i].IsAlive)
                count++;

        return count;
    }

    private float ComputeFormationStepInterval()
    {
        var alive = CountAliveInvaders();
        var removed = FormationRows * FormationCols - alive;
        var baseInterval = 0.62f - waveNumber * 0.025f - removed * 0.013f;
        return MathF.Max(0.18f, baseInterval);
    }

    private bool HasActivePlayerBolt()
    {
        for (var i = 0; i < bolts.Count; i++)
            if (bolts[i].IsPlayer)
                return true;

        return false;
    }

    private InvaderActor? GetLowestAliveInvaderInColumn(int col)
    {
        for (var row = FormationRows - 1; row >= 0; row--)
        {
            var index = row * FormationCols + col;
            if (index < 0 || index >= invaders.Count)
                continue;

            var invader = invaders[index];
            if (invader.IsAlive)
                return invader;
        }

        return null;
    }

    private (float X, float Y) GetInvaderPosition(int row, int col)
    {
        return (formationX + col * FormationSpacingX, formationY + row * FormationSpacingY);
    }

    private void TriggerPlayerHit()
    {
        if (playerExplosionSeconds > 0f)
            return;

        playerExplosionSeconds = 0.48f;
    }

    private void ResetWave(bool firstWave)
    {
        waveNumber = firstWave ? 1 : waveNumber + 1;
        bolts.Clear();
        invaders.Clear();
        resetBeatSeconds = 0f;
        preferredAttackColumn = -1;

        formationX = 10f;
        formationY = 4f;
        formationDirection = 1f;
        formationStepCooldown = 0.24f;
        enemyFireCooldown = firstWave ? 0.78f : 0.56f;

        playerX = Width / 2f;
        playerTargetX = playerX;
        playerDecisionCooldown = 0f;
        playerFireCooldown = 0.16f;
        playerExplosionSeconds = 0f;

        ClearShields();

        for (var row = 0; row < FormationRows; row++)
        for (var col = 0; col < FormationCols; col++)
            invaders.Add(new InvaderActor
            {
                Row = row,
                Col = col,
                IsAlive = true,
                ExplosionTime = 0f
            });
    }

    private void ClearShields()
    {
        Array.Clear(shields, 0, shields.Length);

        CreateShield(9);
        CreateShield(28);
        CreateShield(47);
    }

    private void CreateShield(int startX)
    {
        const int shieldTop = 22;
        for (var row = 0; row < ShieldMask.Length; row++)
        {
            var line = ShieldMask[row];
            for (var col = 0; col < line.Length; col++)
            {
                if (line[col] == '.')
                    continue;

                var x = startX + col;
                var y = shieldTop + row;
                if ((uint)x < Width && (uint)y < Height)
                    shields[x, y] = true;
            }
        }
    }

    private void DamageShield(int centerX, int centerY)
    {
        for (var y = centerY - 1; y <= centerY + 1; y++)
        for (var x = centerX - 1; x <= centerX + 1; x++)
        {
            if ((uint)x >= Width || (uint)y >= Height)
                continue;

            if (Math.Abs(x - centerX) + Math.Abs(y - centerY) <= 2)
                shields[x, y] = false;
        }
    }

    private void DrawBackground(Image<Rgba32> img)
    {
        var time = (float)elapsedThisScene.TotalSeconds;
        for (var y = 0; y < Height; y++)
        {
            var depth = y / (float)(Height - 1);
            var row = new Rgba32(
                ToByte(4f + depth * 2f),
                ToByte(6f + depth * 4f),
                ToByte(14f + depth * 12f));

            for (var x = 0; x < Width; x++)
                img[x, y] = row;
        }

        for (var i = 0; i < 12; i++)
        {
            var x = (Hash(i, 17) % Width + Width) % Width;
            var y = (Hash(i, 31) % 13 + 13) % 13 + 1;
            var twinkle = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(time * 2.8f + i));
            img[x, y] = Scale(new Rgba32(204, 219, 255), twinkle);
        }

        for (var x = 0; x < Width; x++)
            img[x, Height - 1] = new Rgba32(42, 58, 84);
    }

    private void DrawShields(Image<Rgba32> img)
    {
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            if (!shields[x, y])
                continue;

            var topEdge = y > 0 && !shields[x, y - 1];
            img[x, y] = topEdge ? new Rgba32(188, 255, 212) : new Rgba32(92, 224, 146);
        }
    }

    private void DrawInvaders(Image<Rgba32> img)
    {
        var useAltFrame = ((int)(animationClock * 6f) & 1) == 1;
        for (var i = 0; i < invaders.Count; i++)
        {
            var invader = invaders[i];
            var (x, y) = GetInvaderPosition(invader.Row, invader.Col);

            if (invader.IsAlive)
            {
                var sprite = GetInvaderSprite(invader.Row, useAltFrame);
                DrawSprite(img, (int)MathF.Round(x) - 2, (int)MathF.Round(y) - 1, sprite, RowColors[invader.Row]);
            }
            else if (invader.ExplosionTime > 0f)
            {
                var intensity = invader.ExplosionTime / 0.24f;
                DrawSprite(
                    img,
                    (int)MathF.Round(x) - 2,
                    (int)MathF.Round(y) - 2,
                    ExplosionSprite,
                    Scale(new Rgba32(255, 222, 140), intensity));
            }
        }
    }

    private static string[] GetInvaderSprite(int row, bool useAltFrame)
    {
        return row switch
        {
            0 => useAltFrame ? InvaderTopFrameB : InvaderTopFrameA,
            1 => useAltFrame ? InvaderMidFrameB : InvaderMidFrameA,
            _ => useAltFrame ? InvaderLowFrameB : InvaderLowFrameA
        };
    }

    private void DrawBolts(Image<Rgba32> img)
    {
        for (var i = 0; i < bolts.Count; i++)
        {
            var bolt = bolts[i];
            var x = (int)MathF.Round(bolt.X);
            var y = (int)MathF.Round(bolt.Y);
            SetPixel(img, x, y - 1, Scale(bolt.Color, 0.55f));
            SetPixel(img, x, y, bolt.Color);
            SetPixel(img, x, y + 1, Scale(bolt.Color, 0.55f));
        }
    }

    private void DrawPlayer(Image<Rgba32> img)
    {
        var x = (int)MathF.Round(playerX) - 3;
        if (playerExplosionSeconds > 0f)
        {
            var intensity = playerExplosionSeconds / 0.48f;
            DrawSprite(img, x, PlayerY - 2, ExplosionSprite, Scale(new Rgba32(255, 176, 126), intensity));
            return;
        }

        DrawSprite(img, x, PlayerY - 2, PlayerSprite, new Rgba32(178, 236, 255));
    }

    private float EvaluateLaneSafety(float candidateX)
    {
        var safety = 0f;

        for (var i = 0; i < bolts.Count; i++)
        {
            var bolt = bolts[i];
            if (bolt.IsPlayer || bolt.VelocityY <= 0f)
                continue;

            var dy = PlayerY - bolt.Y;
            if (dy < -1f || dy > 15f)
                continue;

            var dx = MathF.Abs(bolt.X - candidateX);
            if (dx > 3.4f)
                continue;

            var timeToImpact = dy / MathF.Max(1f, bolt.VelocityY);
            if (timeToImpact < 0f || timeToImpact > 1.1f)
                continue;

            safety -= (3.5f - dx) * 2.5f;
            safety -= (1.1f - timeToImpact) * 7.5f;
        }

        return safety;
    }

    private bool IsShotLaneClear(float laneX)
    {
        var x = (int)MathF.Round(laneX);
        if ((uint)x >= Width)
            return false;

        for (var y = 0; y < PlayerY; y++)
        {
            if (shields[x, y])
                return false;
        }

        return true;
    }

    private static void DrawSprite(Image<Rgba32> img, int x, int y, string[] sprite, Rgba32 color)
    {
        for (var row = 0; row < sprite.Length; row++)
        {
            var line = sprite[row];
            for (var col = 0; col < line.Length; col++)
            {
                if (line[col] != 'X')
                    continue;

                SetPixel(img, x + col, y + row, color);
            }
        }
    }

    private static void SetPixel(Image<Rgba32> img, int x, int y, Rgba32 color)
    {
        if ((uint)x >= Width || (uint)y >= Height)
            return;

        img[x, y] = color;
    }

    private static int Hash(int seed, int salt)
    {
        unchecked
        {
            var h = seed * 374761393 + salt * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }

    private static Rgba32 Scale(Rgba32 color, float factor)
    {
        var clamped = Math.Clamp(factor, 0f, 1f);
        return new Rgba32(
            ToByte(color.R * clamped),
            ToByte(color.G * clamped),
            ToByte(color.B * clamped));
    }

    private static byte ToByte(float value)
    {
        return (byte)Math.Clamp((int)MathF.Round(value), 0, 255);
    }

    private struct InvaderActor
    {
        public int Row;
        public int Col;
        public bool IsAlive;
        public float ExplosionTime;
    }

    private struct BoltActor
    {
        public float X;
        public float Y;
        public float VelocityY;
        public bool IsPlayer;
        public Rgba32 Color;
    }
}
